using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using BrightstarDB;
using BrightstarDB.Client;
using NLog;
using VDS.RDF.Parsing;

namespace TripleStore
{
    /// <summary>
    /// Implements a triple store, persisted in a embedded file.
    /// This triple store is based on Brightstar DB, which implements
    /// a RDF store to store triples as defined in
    /// https://www.w3.org/TR/2004/REC-rdf-concepts-20040210/#section-triples
    /// In other words, an RDF triple contains three components:
    /// * the subject, which is an RDF URI reference or a blank node
    /// * the predicate, which is an RDF URI reference
    /// * the object, which is an RDF URI reference, a literal or a blank node
    /// </summary>
    /// <remarks>
    /// This class is threadsafe. <see cref="Dispose"/> should be called by the
    /// owner.
    /// </remarks>
    public sealed class TripleStore : ITripleStore, IDisposable
    {
        /// <summary>
        /// Default store name.
        /// This is used if no other store name is provided in constructor.
        /// </summary>
        private const string DefaultStoreName = "Emma";

        /// <summary>
        /// Wildcard used for certain queries in BrighstarDb.
        /// </summary>
        private const string Wildcard = "<http://www.brightstardb.com/.well-known/model/wildcard>";

        /// <summary>Logging instance.</summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Absolute path to the store directory.
        /// </summary>
        private readonly string storeDirectory;

        /// <summary>
        /// A lock for transactions
        /// </summary>
        private readonly object transactionLock = new object();

        private readonly object clientLock = new object();

        /// <summary>
        /// Store name.
        /// Currently we support a single store.
        /// </summary>
        private readonly string storeName;

        /// <summary>
        /// Instance of the store.
        /// </summary>
        private readonly IBrightstarService client;

        #region store initialization

        /// <summary>
        /// Gets the connection string to the triple store.
        /// </summary>
        private string ConnectionString
        {
            get
            {
                return string.Format(
                    @"Type=embedded;StoresDirectory={0};StoreName={1}",
                                  this.storeDirectory,
                                  this.storeName);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TripleStore"/> class.
        /// </summary>
        /// <param name="storeDirectory"> Directory where the store will be located</param>
        /// <param name="storeName">Name of the store</param>
        /// <param name="clearStore">True if the store needs to be cleared when opened</param>
        public TripleStore(string storeDirectory, string storeName = null, bool clearStore = false)
        {
            Contract.Requires<ArgumentNullException>(storeDirectory != null, "Store directory cannot be null.");
            Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(storeDirectory), "Store directory cannot be empty.");

            this.storeDirectory = storeDirectory;
            this.storeName = storeName ?? DefaultStoreName;

            this.client = BrightstarService.GetClient(this.ConnectionString);

            if (this.client.DoesStoreExist(this.storeName))
            {
                // clear store if necessary
                if (clearStore)
                {
                    this.ClearStore();
                }
            }
            else
            {
                // create store if does not exists
                Logger.Info("Creating triple store {0} at {1}", this.storeName, this.storeDirectory);
                this.client.CreateStore(this.storeName);
            }
        }

        #endregion

        #region Public API, ITripleStore implementation

        /// <inheritdoc/>
        public ulong Count
        {
            get
            {
                return (ulong)this.All().Count;
            }
        }

        /// <inheritdoc/>
        public bool Add(string id, string predicate, string obj)
        {
            return this.Add(new Triple(id, predicate, obj));
        }

        /// <inheritdoc/>
        public bool Add(Triple triple)
        {
            return this.ExecuteTransaction(new UpdateTransactionData { InsertData = triple.ToDbString() });
        }

        /// <inheritdoc/>
        public bool Contains(Triple triple)
        {
            var bindings = this.Query(new List<Triple> { triple });
            return bindings.Count == 1 && bindings[0].Count == 0;
        }

        /// <inheritdoc/>
        public List<Bindings> Query(string queryString)
        {
            return this.Query(Triple.ParseQueryString(queryString));
        }

        /// <inheritdoc/>
        public List<Bindings> Query(List<Triple> triples)
        {
            var variables = this.GetQueryVariables(triples);
            var query = string.Format(
                            "SELECT {0} WHERE {{ {1} }}",
                            variables.Count == 0 ? "*" : string.Join(" ", variables.Select(x => Triple.Var(x))),
                            string.Join(" ", triples.Select(x => x.ToDbString())));

            return this.ExecuteQuery(query, variables);
        }

        /// <inheritdoc/>
        public List<Bindings> Query(Triple triple)
        {
            var variables = this.GetQueryVariables(triple);
            var query = string.Format(
                            "SELECT {0} WHERE {{ {1} }}",
                            variables.Count == 0 ? "*" : string.Join(" ", variables.Select(x => Triple.Var(x))),
                            string.Join(" ", triple.ToDbString()));

            return this.ExecuteQuery(query, variables);
        }

        /// <inheritdoc/>
        public List<Bindings> Query(string id, string predicate, string obj)
        {
            return this.Query(new Triple(id, predicate, obj));
        }

        /// <inheritdoc/>
        public HashSet<Triple> Remove(string queryString)
        {
            return this.Remove(Triple.ParseQueryString(queryString));
        }

        /// <inheritdoc/>
        public HashSet<Triple> Remove(List<Triple> queries)
        {
            if (queries.Count == 1)
            {
                // If there is only one query, and it has no variables, then we can just directly remove it
                // from the storage if it exists.
                return this.Remove(queries[0]);
            }

            var matches = new HashSet<Triple>();

            if (queries.Count > 0)
            {
                // Need to find the binding set and then apply them to the queries to form complete triples. These
                // will then be removed from the storage.
                var bindingsList = this.Query(queries);
                foreach (var bindings in bindingsList)
                {
                    foreach (var query in queries)
                    {
                        var triple = query.ApplyBindingSet(bindings);
                        if (this.RemoveTripleLiteral(triple))
                        {
                            matches.Add(triple);
                        }
                    }
                }
            }
            return matches;
        }

        /// <inheritdoc/>
        public HashSet<Triple> Remove(Triple query)
        {
            var matches = new HashSet<Triple>();

            if (!query.IsQuery && this.RemoveTripleLiteral(query))
            {
                matches.Add(query);
            }
            else if (Triple.IsVariable(query.Id) && Triple.IsVariable(query.Predicate) && Triple.IsVariable(query.Object))
            {
                matches = this.All();
                this.ClearStore();
            }
            else
            {
                var bindingsList = this.Query(query);
                foreach (var bindings in bindingsList)
                {
                    var triple = query.ApplyBindingSet(bindings);
                    if (this.RemoveTripleLiteral(triple))
                    {
                        matches.Add(triple);
                    }
                }
            }
            return matches;
        }

        /// <inheritdoc/>
        public HashSet<Triple> Remove(string id, string predicate, string obj)
        {
            return this.Remove(new Triple(id, predicate, obj));
        }

        /// <inheritdoc/>
        public HashSet<Triple> All()
        {
            var result = new HashSet<Triple>();
            var query = new Triple(Triple.Var("a"), Triple.Var("b"), Triple.Var("c"));
            var bindingsList = this.Query(new List<Triple> { query });
            foreach (var bindings in bindingsList)
            {
                var triple = query.ApplyBindingSet(bindings);
                result.Add(triple);
            }
            return result;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Gets a list of variables from the input triples with the ? prefix removed.
        /// </summary>
        /// <param name="triples">a list of triples</param>
        /// <returns>a list of variable names</returns>
        private HashSet<string> GetQueryVariables(List<Triple> triples)
        {
            var variables = new HashSet<string>();
            foreach (var triple in triples)
            {
                this.GetQueryVariables(triple, variables);
            }
            return variables;
        }

        /// <summary>
        /// Gets a list of variables from a single triple with the ? prefix removed.
        /// </summary>
        /// <param name="triple">The triple to extract the variables from.</param>
        /// <param name="variables">An optional set to append the found variables to.</param>
        /// <returns>A set of variable names. If an existing variables set was provided, then the return value will point to the same set.</returns>
        private HashSet<string> GetQueryVariables(Triple triple, HashSet<string> variables = null)
        {
            if (variables == null)
            {
                variables = new HashSet<string>();
            }

            if (Triple.IsVariable(triple.Id))
            {
                variables.Add(triple.Id.Substring(1));
            }
            if (Triple.IsVariable(triple.Predicate))
            {
                variables.Add(triple.Predicate.Substring(1));
            }
            if (Triple.IsVariable(triple.Object))
            {
                variables.Add(triple.Object.Substring(1));
            }

            return variables;
        }

        /// <summary>
        /// For default uris, it will remove the start urn and return
        /// the original value
        /// </summary>
        /// <param name="uri">a Uri</param>
        /// <returns>a sanitized value</returns>
        internal static string SanitizeUri(string uri)
        {
            return uri.StartsWith(TripleExtensions.DefaultUrnStart) ?
                uri.Substring(TripleExtensions.DefaultUrnStart.Length) :
                uri;
        }

        /// <summary>
        /// Removes a triple with no variables on it from the store.
        /// </summary>
        /// <param name="triple">A complete triple</param>
        /// <returns>true if the triple is in the store and it is deleted</returns>
        private bool RemoveTripleLiteral(Triple triple)
        {
            if (this.Contains(triple))
            {
                return this.ExecuteTransaction(new UpdateTransactionData { DeletePatterns = triple.ToDbString() });
            }

            return false;
        }

        /// <summary>
        /// Clears the store for this instance.
        /// </summary>
        /// <returns>True if succeeded</returns>
        internal bool ClearStore()
        {
            var deletePatternsData = new StringBuilder();
            deletePatternsData.AppendLine(string.Format("{0} {0} {0} .", Wildcard));
            return this.ExecuteTransaction(new UpdateTransactionData { DeletePatterns = deletePatternsData.ToString() });
        }

        /// <summary>
        /// Executes a transaction into the DB
        /// </summary>
        /// <param name="transactionData">data to be added/deleted</param>
        /// <returns>true if the transaction completed</returns>
        private bool ExecuteTransaction(UpdateTransactionData transactionData)
        {
            IJobInfo jobInfo;
            lock (this.clientLock)
            {
                this.ThrowIfDisposed();

                // execute a transaction to insert the data into the store
                jobInfo = this.client.ExecuteTransaction(this.storeName, transactionData);
            }

            if (jobInfo.ExceptionInfo != null)
            {
                Logger.Error("Failed when executing transaction {0}: {1}", transactionData, jobInfo.ExceptionInfo.Message);
            }
            return jobInfo.JobCompletedOk;
        }

        /// <summary>
        /// Executes a SPARQL query and returns a list of bindings using the provided query string and set of variables.
        /// </summary>
        /// <param name="query">The query string to execute.</param>
        /// <param name="variables">The query variables.</param>
        /// <returns>A list of matching bindings.</returns>
        private List<Bindings> ExecuteQuery(string query, HashSet<string> variables)
        {
            var result = new List<Bindings>();

            Logger.Debug("Executing SPARQL query: {0}", query);

            // Create an XDocument from the SPARQL Result XML.
            // See http://www.w3.org/TR/rdf-sparql-XMLres/ for the XML format returned.
            try
            {
                Stream unicodeXmlResult;
                lock (this.clientLock)
                {
                    this.ThrowIfDisposed();
                    unicodeXmlResult = this.client.ExecuteQuery(this.storeName, query, null, SparqlResultsFormat.Xml.WithEncoding(Encoding.Unicode));
                }

                var queryResult = XDocument.Load(unicodeXmlResult);
                foreach (var sparqlResultRow in queryResult.SparqlResultRows())
                {
                    var bindingsSet = new HashSet<Bindings.Binding>();
                    foreach (var variable in variables)
                    {
                        var value = sparqlResultRow.GetColumnValue(variable);
                        if (value != null)
                        {
                            var binding = new Bindings.Binding(variable, SanitizeUri(value.ToString()));
                            bindingsSet.Add(binding);
                        }
                    }
                    result.Add(new Bindings(bindingsSet));
                }
            }
            catch (RdfParseException ex)
            {
                Logger.Error("Error querying the triple store, the query {0} is badformed: {1}", query, ex.Message);
            }
            catch (BrightstarClientException ex)
            {
                // this is expected on queries with no variables, where the triple is not present on the store
                if (variables.Count != 0)
                {
                    // else we log the error
                    Logger.Error("Error querying the triple store with query {0}: {1}", query, ex?.Message);
                }
            }
            return result;
        }

        /// <summary>
        /// Throws an exception if the object has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (this.disposedValue)
            {
                throw new ObjectDisposedException("Long-term triple store is disposed.");
            }
        }

        #endregion

        #region Code Contracts Invariants

        [ContractInvariantMethod]
        private void ObjectInvariants()
        {
            Contract.Invariant(this.storeDirectory != null);
            Contract.Invariant(this.storeName != null);
        }

        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public void Dispose(bool disposing)
        {
            lock (this.clientLock)
            {
                if (!this.disposedValue)
                {
                    if (disposing)
                    {
                        // Dispose managed state (managed objects).
                        // Shutdown Brightstar processing threads
                        BrightstarService.Shutdown();
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    this.disposedValue = true;
                }
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);

            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
