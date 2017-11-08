using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace TripleStore
{
    /// <summary>
    /// This a set of bindings that bind a value to a specific variable.
    /// </summary>
    public sealed class Bindings : IEnumerable<Bindings.Binding>
    {
        /// <summary>
        /// A single binding associating a value with a variable.
        /// </summary>
        /// <remarks>This is an immutable class.</remarks>
        public sealed class Binding
        {
            /// <summary>
            /// Gets the variable name.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Gets the value of the variable.
            /// </summary>
            public string Value { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="Binding"/> class.
            /// </summary>
            /// <param name="name">The name of the binding.</param>
            /// <param name="value">The value of the binding.</param>
            public Binding(string name, string value)
            {
                Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(name), "Name must be non-null and non-empty.");
                Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(value), "Value must be non-null and non-empty.");

                this.Name = CheckVariableName(name);
                this.Value = value;
            }

            /// <inheritdoc/>
            public override string ToString()
            {
                return string.Format("{0} = {1}", this.Name, this.Value);
            }
        }

        /// <summary>
        /// Utility method to check if the variable name starts with the variable prefix. If not,
        /// it attaches it to the front of the name.
        /// </summary>
        /// <param name="variableName">The variable name to check.</param>
        /// <returns>The processed variable name.</returns>
        private static string CheckVariableName(string variableName)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(variableName), "Variable name must be non-null and non-empty.");
            Contract.Ensures(!string.IsNullOrWhiteSpace(Contract.Result<string>()));

            variableName = variableName.Trim();
            if (!variableName.StartsWith(Triple.VariablePrefix))
            {
                variableName = Triple.VariablePrefix + variableName;
            }

            return variableName;
        }

        /// <summary>
        /// Dictionary holding the bindings for fast lookup.
        /// </summary>
        private readonly Dictionary<string, Binding> bindings;

        /// <summary>
        /// Index operator for retrieving a specific binding by name.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <returns>The binding associated with the provided variable name or null if one could not be found.</returns>
        public string this[string name]
        {
            get
            {
                Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(name), "Key must be a non-null, non-empty string.");

                Binding binding = null;
                this.bindings.TryGetValue(CheckVariableName(name), out binding);

                return binding?.Value;
            }
        }

        /// <summary>
        /// Gets the number of bindings stored in this set.
        /// </summary>
        public int Count
        {
            get { return this.bindings.Count; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bindings"/> class.
        /// </summary>
        /// <param name="bindings">Bindings to add to this set.</param>
        public Bindings(ICollection<Binding> bindings = null)
        {
            this.bindings = new Dictionary<string, Binding>();
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    if (!this.bindings.ContainsKey(binding.Name))
                    {
                        this.bindings.Add(binding.Name, binding);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bindings"/> class. Copies the contents of an existing
        /// binding set into this set. Also adds any additional bindings that are provided.
        /// </summary>
        /// <param name="bindingSet">The binding set to copy from.</param>
        /// <param name="bindings">Additional bindings to add to this set.</param>
        public Bindings(Bindings bindingSet, ICollection<Binding> bindings = null)
            : this(bindings)
        {
            Contract.Requires<ArgumentNullException>(bindingSet != null, "Binding set cannot be null.");

            foreach (var kvPair in bindingSet.bindings)
            {
                if (!this.bindings.ContainsKey(kvPair.Key))
                {
                    this.bindings.Add(kvPair.Key, kvPair.Value);
                }
            }
        }

        /// <summary>
        /// Checks to see if this variable existings in the binding set.
        /// </summary>
        /// <param name="variableName">The name of the variable.</param>
        /// <returns>True if this variable exists in the binding set and false otherwise.</returns>
        public bool Contains(string variableName)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrWhiteSpace(variableName), "Variable name must be non-null and non-empty.");
            return this.bindings.ContainsKey(CheckVariableName(variableName));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            Contract.Ensures(!string.IsNullOrWhiteSpace(Contract.Result<string>()));

            var builder = new StringBuilder("Binding = {");
            foreach (var pair in this.bindings)
            {
                builder.AppendFormat(" \"{0}\" ", pair.Value);
            }

            builder.Append("}");
            return builder.ToString();
        }

        #region IEnumerable Implementation

        /// <inheritdoc/>
        public IEnumerator<Binding> GetEnumerator()
        {
            return this.bindings.Values.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.bindings.GetEnumerator();
        }

        #endregion

        #region Code Contracts Invariants

        [ContractInvariantMethod]
        private void ObjectInvariants()
        {
            Contract.Invariant(this.bindings != null);
        }

        #endregion
    }
}
