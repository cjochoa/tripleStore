using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;

namespace TripleStore
{
    /// <summary>
    /// Represents a triple which has an ID, predicate, and object. A triple can also be a query if at least one
    /// of the ID, predicate, or object is a variable. A variable is a name which is prepended with the
    /// <see cref="Triple.VariablePrefix"/> string.
    /// </summary>
    public sealed class Triple
    {
        /// <summary>
        /// Overloaded equals operator that compares if two triples are equal. Two triples are considered
        /// equal if both triples have the same ID, predicate, and object strings.
        /// </summary>
        /// <param name="t1">First operand.</param>
        /// <param name="t2">Second operand.</param>
        /// <returns>True if the triples are equal.</returns>
        public static bool operator ==(Triple t1, Triple t2)
        {
            return object.ReferenceEquals(t1, t2) || t1.Equals(t2);
        }

        /// <summary>
        /// Overloaded not equals operator that compares if two triples are not equal. Two triples are considered
        /// not equal if one or more of the ID, predicate, or object strings do not match between the two.
        /// </summary>
        /// <param name="t1">First operand.</param>
        /// <param name="t2">Second operand.</param>
        /// <returns>True if the triples are not equal.</returns>
        public static bool operator !=(Triple t1, Triple t2)
        {
            return !(t1 == t2);
        }

        /// <summary>
        /// Factory method that constructs a collection of <see cref="Triple"/> objects based on the query string.
        /// </summary>
        /// <param name="queryString">The query string to parse.</param>
        /// <returns>A collection of query triple objects or an empty list if the query string didn't contain any triples.</returns>
        public static List<Triple> ParseQueryString(string queryString)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(queryString), "Query string must be non-empty and non-null.");
            Contract.Ensures(Contract.Result<List<Triple>>() != null);

            var queryList = new List<Triple>();

            string[] queries = queryString.ToLower().Split(QuerySeparator, StringSplitOptions.None);
            foreach (var query in queries)
            {
                queryList.Add(ParseQueryStringIntoPrimitives(query.Trim()));
            }

            return queryList;
        }

        /// <summary>
        /// Factory method that creates a query instance using the provided string.
        /// </summary>
        /// <param name="queryString">Query string with an ID, predicate, and object.</param>
        /// <returns>Query instance.</returns>
        public static Triple ParseQueryStringIntoPrimitives(string queryString)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(queryString), "Query string must be non-empty and non-null.");
            Contract.Ensures(Contract.Result<Triple>() != null);

            var queryTokens = TripleMatcher.Matches(queryString).Cast<Match>().Select(m => m.Value).ToArray();
            if (queryTokens.Length != 3)
            {
                throw new ArgumentException("Query string is malformed.");
            }

            return new Triple(queryTokens[0], queryTokens[1], queryTokens[2]);
        }

        /// <summary>
        /// Checks to see if the string is considered a query variable.
        /// </summary>
        /// <param name="variable">The variable string.</param>
        /// <returns>True if the string is considered a variable by the triple store.</returns>
        public static bool IsVariable(string variable)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(variable), "Variable must be non-empty and non-null.");
            return variable.Trim().StartsWith(VariablePrefix);
        }

        /// <summary>
        /// Returns a formatted representation of the provided name that is understood by the triple store
        /// implementation.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>Formatted representation of the variable name understood by the triple store.</returns>
        public static string Var(string variableName)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(variableName), "Variable name must be non-empty and non-null.");
            Contract.Ensures(Contract.Result<string>() != null);

            return VariablePrefix + variableName;
        }

        /// <summary>
        /// Regex matcher for invalid primitives.
        /// </summary>
        private static readonly Regex InvalidMatcher = new Regex("^\\W*$");

        /// <summary>
        /// Regex matcher for valid triple primitives.
        /// </summary>
        private static readonly Regex TripleMatcher = new Regex("([\"'][^\"']+[\"']|[^\\s\"]+)");

        /// <summary>
        /// Query separator for the query string.
        /// </summary>
        private static readonly string[] QuerySeparator = new string[] { " . " };

        /// <summary>
        /// Prefix to identify variables.
        /// </summary>
        public const string VariablePrefix = "?";

        /// <summary>
        /// Temporary data structure to store the values of variables when doing matching. This will only be
        /// initialized if the triple is a query.
        /// </summary>
        private readonly Dictionary<string, string> tempVariableStore;

        /// <summary>
        /// Gets the ID portion of this query.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the predicate portion of this query.
        /// </summary>
        public string Predicate { get; private set; }

        /// <summary>
        /// Gets the object portion of this query.
        /// </summary>
        public string Object { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not this triple is a query.
        /// </summary>
        public bool IsQuery { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Triple"/> class.
        /// <para>
        /// Each of the ID, predicate, and or object can be a variable or a value. A variable is denoted by prefixing the
        /// <see cref="VariablePrefix"/> string to it. Alternatively, you can use the <see cref="Var(string)"/> utility method
        /// to generate a suitable variable string.
        /// </para>
        /// <para>
        /// When making a query, the system attempts to generate bindings for any variables that are provided to this query
        /// instance.
        /// </para>
        /// </summary>
        /// <param name="id">ID portion of the query. Can be a variable or a value.</param>
        /// <param name="predicate">Predicate portion of the query. Can be a variable or a value.</param>
        /// <param name="obj">Object portion of the query. Can be a variable or a value.</param>
        public Triple(string id, string predicate, string obj)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(id), "ID must be a non-null and non-empty string.");
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(predicate), "Prediate must be a non-null and non-empty string.");
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(obj), "Object must be a non-null and non-empty string.");

            this.Id = this.SanitizePrimitive(id);
            this.Predicate = this.SanitizePrimitive(predicate);
            this.Object = this.SanitizePrimitive(obj);

            this.IsQuery = IsVariable(this.Id) || IsVariable(this.Predicate) || IsVariable(this.Object);

            if (this.IsQuery)
            {
                this.tempVariableStore = new Dictionary<string, string>(3);
            }
        }

        /// <summary>
        /// Creates a new query instance based off of this one except with some of the variables being set to
        /// the values present in the binding instance.
        /// </summary>
        /// <param name="binding">The binding instance to apply.</param>
        /// <returns>
        /// If no bindings could be applied, then this instance is returned. Otherwise, a new query instance
        /// is returned with some of the bindings applied to the new query.
        /// </returns>
        public Triple ApplyBindingSet(Bindings binding)
        {
            Contract.Requires<ArgumentNullException>(binding != null, "Binding cannot be null.");
            Contract.Ensures(Contract.Result<Triple>() != null);

            string id, predicate, obj;

            bool atLeastOneApplied = false;
            atLeastOneApplied |= this.ApplyBinding(binding, this.Id, out id);
            atLeastOneApplied |= this.ApplyBinding(binding, this.Predicate, out predicate);
            atLeastOneApplied |= this.ApplyBinding(binding, this.Object, out obj);

            return atLeastOneApplied ? new Triple(id, predicate, obj) : this;
        }

        /// <summary>
        /// Checks to see if the provided <see cref="Triple"/> instance matches this query. A match is said
        /// to have occurred if the ID, prediate, and object match the query representations. If the query
        /// representation of the triple value is a variable, then it is said to match. For example, if the
        /// query's predicate is a variable, then it is considered to match the triple's predicate. If the
        /// query's predicate was a value, however, then it matches the triple's predicate iff the values
        /// are the same.
        /// </summary>
        /// <param name="triple">The triple to check against.</param>
        /// <returns>True if the triple matches this query.</returns>
        public bool MatchesTriple(Triple triple)
        {
            Contract.Requires<ArgumentNullException>(triple != null, "Triple cannot be null.");

            Dictionary<string, string> tempVariableStore = this.tempVariableStore;
            if (!this.IsQuery && triple.IsQuery)
            {
                tempVariableStore = new Dictionary<string, string>(3);
            }

            if (tempVariableStore != null)
            {
                tempVariableStore.Clear();
            }

            return this.MatchesPrimitive(triple.Id, this.Id, tempVariableStore)
                && this.MatchesPrimitive(triple.Predicate, this.Predicate, tempVariableStore)
                && this.MatchesPrimitive(triple.Object, this.Object, tempVariableStore);
        }

        /// <summary>
        /// Attempts to create a new binding set by taking the variables of the query and setting them
        /// to the equivalent values in the provided triple. The new binding set takes in all of the
        /// old bindings as well as any new bindings that are found.
        /// </summary>
        /// <param name="triple">The triple to construct the bindings from.</param>
        /// <param name="oldBinding">The old binding set which will be subsumed by the new binding set.</param>
        /// <param name="newBinding">The new binding set.</param>
        /// <returns>True if the triple matches the query and a new binding could be generated.</returns>
        public bool TryMatchTriple(Triple triple, Bindings oldBinding, out Bindings newBinding)
        {
            Contract.Requires<ArgumentNullException>(triple != null, "Triple cannot be null.");
            Contract.Requires<ArgumentNullException>(oldBinding != null, "Binding cannot be null.");

            var additionalBindings = new List<Bindings.Binding>();

            newBinding = null;
            if (this.MatchesTriple(triple))
            {
                this.AddNewBinding(oldBinding, additionalBindings, triple.Id, this.Id);
                this.AddNewBinding(oldBinding, additionalBindings, triple.Predicate, this.Predicate);
                this.AddNewBinding(oldBinding, additionalBindings, triple.Object, this.Object);

                newBinding = new Bindings(oldBinding, additionalBindings);
            }

            return newBinding != null;
        }

        /// <summary>
        /// Helper method to create a new binding provided that the primitive is a variable and the binding
        /// doesn't already exist.
        /// </summary>
        /// <param name="oldBinding">The old binding set.</param>
        /// <param name="newBindings">The new bindings to add.</param>
        /// <param name="triplePrimitive">The triple value.</param>
        /// <param name="primitive">The query value.</param>
        private void AddNewBinding(Bindings oldBinding, List<Bindings.Binding> newBindings, string triplePrimitive, string primitive)
        {
            Contract.Requires<ArgumentNullException>(oldBinding != null, "Old binding cannot be null.");
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(triplePrimitive), "Triple primitive must be a non-null, non-empty string.");
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(primitive), "Primitive must be a non-null, non-empty string.");

            if (IsVariable(primitive) && !oldBinding.Contains(primitive))
            {
                newBindings.Add(new Bindings.Binding(primitive, triplePrimitive));
            }
        }

        /// <summary>
        /// Returns the value from the binding set if it exists. Otherwise, it uses the provided primitive
        /// value.
        /// </summary>
        /// <param name="binding">The binding set.</param>
        /// <param name="primitive">The value to check to see if it's in the binding set.</param>
        /// <param name="replacedPrimitive">The old primitive of the binding doesn't contain the variable. Otherwise, it contains the bindings value.</param>
        /// <returns>True if the binding value could be used.</returns>
        private bool ApplyBinding(Bindings binding, string primitive, out string replacedPrimitive)
        {
            Contract.Requires<ArgumentNullException>(binding != null, "Binding cannot be null.");
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(primitive), "Primitive must be a non-null, non-empty string.");

            var bindingValue = binding[primitive];
            replacedPrimitive = IsVariable(primitive) && bindingValue != null ? bindingValue : primitive;

            return !string.Equals(primitive, replacedPrimitive, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Indicates if the provided primitive is equal to the triple's equivalent primitive. Also returns
        /// true if the provided primitive is a variable.
        /// </summary>
        /// <param name="triplePrimitive">The primitive from the triple.</param>
        /// <param name="primitive">The equivalent primitive from this instance.</param>
        /// <param name="variableStore">A temporary variable store mostly to handle variablws that occur more than once (e.g., ?a friendof ?a).</param>
        /// <returns>True if the primitive is a variable or if the two primitives are equal.</returns>
        private bool MatchesPrimitive(string triplePrimitive, string primitive, Dictionary<string, string> variableStore)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(triplePrimitive), "Triple primitive must be a non-null, non-empty string.");
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(primitive), "Primitive must be a non-null, non-empty string.");

            if (IsVariable(primitive) && variableStore != null)
            {
                if (variableStore.ContainsKey(primitive) && !string.Equals(variableStore[primitive], triplePrimitive, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }

                variableStore[primitive] = triplePrimitive;
                return true;
            }

            return string.Equals(primitive, triplePrimitive, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Sanitizes a primitive so that opening and closing quotation marks are removed and that any leading
        /// or trailing whitespaces are removed. The string is also converted to lowercase.
        /// </summary>
        /// <param name="primitive">The string to sanitize.</param>
        /// <returns>Sanitized string.</returns>
        private string SanitizePrimitive(string primitive)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(primitive), "Primitive must be a non-null, non-empty string.");

            char firstChar = primitive[0];

            var sanitizedPrimitive = primitive.Trim().ToLowerInvariant();
            if (firstChar == '"' || firstChar == '\'')
            {
                if (sanitizedPrimitive.Length > 2 && sanitizedPrimitive[sanitizedPrimitive.Length - 1] == firstChar)
                {
                    sanitizedPrimitive = sanitizedPrimitive.Substring(1, sanitizedPrimitive.Length - 2).Trim();
                }
                else
                {
                    throw new ArgumentException("Primitive must have opening and closing quotes or must not be empty.");
                }
            }

            var invalidMatches = InvalidMatcher.Matches(sanitizedPrimitive).Cast<Match>().ToList();
            if (invalidMatches.Count == 1 && invalidMatches[0].Length == sanitizedPrimitive.Length)
            {
                throw new ArgumentException("Invalid triple primitive. Primitive must not contain only special characters. Primitive = " + primitive);
            }

            return sanitizedPrimitive;
        }

        /// <summary>
        /// Two triples are considered equal if they have the same ID, predicate, and object. This equality method
        /// is case insensitive.
        /// </summary>
        /// <param name="obj">The other triple to compare against.</param>
        /// <returns>True if both triples have the same ID, prediate, and object strings.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            var other = obj as Triple;
            return string.Equals(other.Id, this.Id, StringComparison.CurrentCultureIgnoreCase)
                && string.Equals(other.Predicate, this.Predicate, StringComparison.CurrentCultureIgnoreCase)
                && string.Equals(other.Object, this.Object, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 9949;
                hash = (hash * 12277) + this.Id.GetHashCode();
                hash = (hash * 12277) + this.Predicate.GetHashCode();
                hash = (hash * 12277) + this.Object.GetHashCode();

                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            Contract.Ensures(!string.IsNullOrWhiteSpace(Contract.Result<string>()));
            return string.Format("Triple = <{0}, {1}, {2}>", this.Id, this.Predicate, this.Object);
        }

        #region Code Contracts Invariants

        [ContractInvariantMethod]
        private void ObjectInvariants()
        {
            Contract.Invariant(!string.IsNullOrWhiteSpace(this.Id));
            Contract.Invariant(!string.IsNullOrWhiteSpace(this.Predicate));
            Contract.Invariant(!string.IsNullOrWhiteSpace(this.Object));
        }

        #endregion
    }
}
