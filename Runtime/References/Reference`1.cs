﻿namespace GenericScriptableArchitecture
{
    using System;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [Serializable]
    public class Reference<T> : ReferenceBase, IEquatable<Reference<T>>, IEquatable<Variable<T>>
    {
        [SerializeField] private T _constantValue;
        [SerializeField] private Variable<T> _variable;

        public Reference() { }

        public Reference(T value)
        {
            _useConstant = true;
            _constantValue = value;
        }

        public Reference(Variable<T> variable)
        {
            _useConstant = false;
            _variable = variable;
        }

        public T Value => _useConstant ? _constantValue : _variable.Value;

        public static implicit operator T(Reference<T> reference) => reference.Value;

        public static implicit operator Reference<T>(T value) => new Reference<T>(value);

        public override string ToString() => _useConstant ? _constantValue.ToString() : _variable.ToString();

        public bool Equals(Reference<T> other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Value.Equals(other.Value);
        }

        public bool Equals(Variable<T> other)
        {
            return other is { } && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (this.GetType() != obj.GetType())
                return false;

            return Equals((Reference<T>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Value?.GetHashCode() ?? 0;
                return hash;
            }
        }

        public static bool operator ==(Reference<T> lhs, Reference<T> rhs)
        {
            return lhs?.Equals(rhs) ?? ReferenceEquals(rhs, null);
        }

        public static bool operator !=(Reference<T> lhs, Reference<T> rhs)
        {
            return ! (lhs == rhs);
        }

        public static bool operator ==(Reference<T> lhs, Variable<T> rhs)
        {
            if (lhs is null)
            {
                return (Object)rhs == null;
            }

            return lhs.Equals(rhs);
        }

        public static bool operator !=(Reference<T> lhs, Variable<T> rhs)
        {
            return ! (lhs == rhs);
        }
    }
}