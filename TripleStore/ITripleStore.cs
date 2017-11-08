
using System.Collections.Generic;

namespace TripleStore
{
    /// <summary>
    /// Interface Triple Store operations.
    /// </summary>
    public interface ITripleStore
    {
        /// <summary>
        /// Gets the total number of stored triples.
        /// </summary>
        ulong Count { get; }

        /// <summary>
        /// Adds a <see cref="Triple"/> instance into the store.
        /// </summary>
        /// <param name="triple">The triple to add.</param>
        /// <returns>True if the triple was added and false if the triple is already in the store.</returns>
        bool Add(Triple triple);

        /// <summary>
        /// Adds a new triple into the store.
        /// </summary>
        /// <param name="id">Required ID string.</param>
        /// <param name="predicate">Required predicate string.</param>
        /// <param name="obj">Required object string.</param>
        /// <returns>True if the triple was added and false if the triple is already in the store.</returns>
        bool Add(string id, string predicate, string obj);

        /// <summary>
        /// Checks to see if the provided triple is contained in the storage.
        /// </summary>
        /// <param name="triple">The triple to check.</param>
        /// <returns>True if the triple is contained in the storage.</returns>
        bool Contains(Triple triple);

        /// <summary>
        /// Queries the triple store and returns a list of <see cref="Bindings"/> instances. Multiple queries
        /// are concatenated into an AND query.
        /// </summary>
        /// <param name="queries">The queries to use.</param>
        /// <returns>A list of result bindings. If there were no matches, then an empty list is returned.</returns>
        List<Bindings> Query(List<Triple> queries);

        /// <summary>
        /// Queries the triple store and returns a list of <see cref="Bindings"/> instances.
        /// </summary>
        /// <param name="query">The query to use. This triple can consist of no, some, or all variables.</param>
        /// <returns>A list of result bindings. If there were no matches, then an empty list is returned.</returns>
        List<Bindings> Query(Triple query);

        /// <summary>
        /// Queries the triple store and returns a list of <see cref="Bindings"/> instances.
        /// </summary>
        /// <param name="id">The ID parameter. Can be a value or a variable.</param>
        /// <param name="predicate">The predicate parameter. Can be a value or a variable.</param>
        /// <param name="obj">The object parameter. Can be a value or a variable.</param>
        /// <returns>A list of result bindings. If there were no matches, then an empty list is returned.</returns>
        List<Bindings> Query(string id, string predicate, string obj);

        /// <summary>
        /// Queries the triple store using the provided query string and returns a list of <see cref="Bindings"/>
        /// instances.
        /// </summary>
        /// <param name="queryString">The query string.</param>
        /// <returns>A list of result bindings. If there were no matches, then an empty list is returned.</returns>
        List<Bindings> Query(string queryString);

        /// <summary>
        /// Removes all triples from the store that match the provided query list. Multiple queries are concatenated
        /// into an AND query.
        /// </summary>
        /// <param name="queries">The queries to use.</param>
        /// <returns>A set of triples that were removed from the store.</returns>
        HashSet<Triple> Remove(List<Triple> queries);

        /// <summary>
        /// Removes all triples from the store that matches the provided query.
        /// </summary>
        /// <param name="query">The query to use. This triple can consist of no, some, or all variables.</param>
        /// <returns>A set of triples that were removed from the store.</returns>
        HashSet<Triple> Remove(Triple query);

        /// <summary>
        /// Removes all triples from the store that matches the provided query.
        /// </summary>
        /// <param name="id">The ID parameter. Can be a value or a variable.</param>
        /// <param name="predicate">The predicate parameter. Can be a value or a variable.</param>
        /// <param name="obj">The object parameter. Can be a value or a variable.</param>
        /// <returns>A set of triples that were removed from the store.</returns>
        HashSet<Triple> Remove(string id, string predicate, string obj);

        /// <summary>
        /// Removes all triples from the store that match the provided query string.
        /// </summary>
        /// <param name="queryString">The query string.</param>
        /// <returns>A set of triples that were removed from the store.</returns>
        HashSet<Triple> Remove(string queryString);

        /// <summary>
        /// Returns all triples in the store.
        /// </summary>
        /// <returns>all triples in the store</returns>
        HashSet<Triple> All();
    }
}