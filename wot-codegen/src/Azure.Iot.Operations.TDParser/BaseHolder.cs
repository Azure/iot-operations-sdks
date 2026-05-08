// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    using System;

    public class BaseHolder<T> : IEquatable<BaseHolder<T>>
        where T : IEquatable<T>
    {
        public required T Value { get; set; }

        public virtual bool Equals(BaseHolder<T>? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Value.Equals(other.Value);
            }
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(BaseHolder<T>? left, BaseHolder<T>? right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                return left.Equals(right);
            }
        }

        public static bool operator !=(BaseHolder<T>? left, BaseHolder<T>? right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else if (ReferenceEquals(obj, null))
            {
                return false;
            }
            else if (obj is not BaseHolder<T> other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }
    }
}
