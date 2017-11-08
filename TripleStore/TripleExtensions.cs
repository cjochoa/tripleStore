using System;
using System.Diagnostics.Contracts;
using System.Text;

namespace TripleStore
{
    /// <summary>
    /// Represents a triple which has an ID, predicate, and object, that either
    /// can be persisted or contains a query.
    /// DB triples are compliant with the following W3C recommendation.
    ///  https://www.w3.org/TR/2004/REC-rdf-concepts-20040210/#section-triples
    /// In other words, an RDF triple contains three components:
    /// * the subject, which is an RDF URI reference or a blank node
    /// * the predicate, which is an RDF URI reference
    /// * the object, which is an RDF URI reference, a literal or a blank node
    /// </summary>
    internal static class TripleExtensions
    {
        /// <summary>
        /// Default urn start. We store URIs as described in RFC3986
        /// so using a urn instead of a url minimizes the extra space
        /// needed to store it.
        /// Using two characters to further minimize the extra space
        /// needed in the DB to store triples. It is not possible to use
        /// only one character.
        /// </summary>
        internal const string DefaultUrnStart = "em:";

        /// <summary>
        /// Converts this instance to a format as expected by BrighstarDB.
        /// </summary>
        /// <param name="triple">a triple</param>
        /// <returns>A string that can be persisted on a triple store</returns>
        public static string ToDbString(this Triple triple)
        {
            var data = new StringBuilder();
            data.Append(string.Format(
                "{0} {1} {2} .",
                Triple.IsVariable(triple.Id) ? triple.Id : EscapeString(triple.Id, true),
                Triple.IsVariable(triple.Predicate) ? triple.Predicate : EscapeString(triple.Predicate, true),
                Triple.IsVariable(triple.Object) ? triple.Object : EscapeString(triple.Object)));
            return data.ToString();
        }

        /// <summary>
        /// Transforms a string value to a Uri of the form em:value
        /// </summary>
        /// <param name="value">any string</param>
        /// <returns>a Uri</returns>
        private static Uri ToDefaultUri(string value)
        {
            return new Uri(string.Format("{0}{1}", DefaultUrnStart, value));
        }

        /// <summary>
        /// Surrounds a string with double quotes.
        /// </summary>
        /// <param name="str">any string</param>
        /// <param name="isUri">true if this needs to be transformed to uri format</param>
        /// <returns>a quoted string</returns>
        private static string EscapeString(string str, bool isUri = false)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(str), "String must be a non-null, non-empty string.");
            Uri uri;
            if (isUri)
            {
                if (!Uri.TryCreate(str, UriKind.Absolute, out uri))
                {
                    uri = ToDefaultUri(str);
                }
                return string.Format("<{0}>", uri.ToString());
            }

            return string.Format("\"{0}\"", str);
        }
    }
}