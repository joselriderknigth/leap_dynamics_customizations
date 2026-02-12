using System;

namespace E2E.Api.Tests.Utils
{
    /// <summary>
    /// Represents an optional value that can be:
    /// - Not set
    /// - Explicitly set to null
    /// - Set to a value
    /// Usage:
    /// - Don't set the property: Optional<bool> is default (not set)
    /// - Set to null: SomeField = Optional<bool>.Null
    /// - Set to value: SomeField = Optional<bool>.Some(true)
    /// </summary>
    public readonly struct Optional<T>
    {
        private readonly T _value;
        private readonly bool _hasValue;
        private readonly bool _isExplicitNull;

        private Optional(T value, bool hasValue, bool isExplicitNull)
        {
            _value = value;
            _hasValue = hasValue;
            _isExplicitNull = isExplicitNull;
        }

        /// <summary>
        /// The value is explicitly set to null.
        /// </summary>
        public static Optional<T> Null => new(default!, false, true);

        /// <summary>
        /// The value is set to a specific value.
        /// </summary>
        public static Optional<T> Some(T value) => new(value, true, false);

        /// <summary>
        /// True if the value was explicitly set (either to null or a value).
        /// </summary>
        public bool IsDefined => _hasValue || _isExplicitNull;

        /// <summary>
        /// True if the value was explicitly set to null.
        /// </summary>
        public bool IsNull => _isExplicitNull;

        /// <summary>
        /// True if the value has an actual value (not null and not undefined).
        /// </summary>
        public bool HasValue => _hasValue;

        /// <summary>
        /// Gets the value. Throws if the value is null or not set.
        /// </summary>
        public T Value
        {
            get
            {
                if (!_hasValue)
                    throw new InvalidOperationException(
                        "Optional has no value. Check HasValue before accessing Value."
                    );
                return _value;
            }
        }

        /// <summary>
        /// Gets the value or null if not set or explicitly null.
        /// </summary>
        public T? ValueOrDefault => _hasValue ? _value : default;

        // Implicit conversion from T to Optional<T>
        public static implicit operator Optional<T>(T value) => Some(value);

        public override string ToString()
        {
            if (_isExplicitNull)
                return "Null";
            if (_hasValue)
                return $"Some({_value})";
            return "Undefined";
        }
    }
}
