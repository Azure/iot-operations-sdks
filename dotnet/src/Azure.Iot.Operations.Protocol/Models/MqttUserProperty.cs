// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttUserProperty
    {
        /// <summary>
        /// Creates a new MqttUserProperty with a string name and string value.
        /// The value is internally stored as UTF-8 bytes for performance.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value as a string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="value"/> is null.</exception>
        public MqttUserProperty(string name, string value)
            : this(name, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(value ?? throw new ArgumentNullException(nameof(value)))))
        {
        }

        /// <summary>
        /// Creates a new MqttUserProperty with a string name and a pre-encoded UTF-8 byte value.
        /// This constructor is more performant when the value is already available as bytes.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value as an ArraySegment of bytes. The ArraySegment must have a valid backing array.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> has a null backing array.</exception>
        public MqttUserProperty(string name, ArraySegment<byte> value)
            : this(name, value.Array is null
                ? throw new ArgumentException("ArraySegment must have a valid backing array.", nameof(value))
                : new ReadOnlyMemory<byte>(value.Array, value.Offset, value.Count))
        {
        }

        /// <summary>
        /// Creates a new MqttUserProperty with a string name and a pre-encoded UTF-8 byte value.
        /// This constructor is more performant when the value is already available as bytes.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value as ReadOnlyMemory of bytes.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public MqttUserProperty(string name, ReadOnlyMemory<byte> value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ValueBuffer = value;
        }

        /// <summary>
        /// Gets the property name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the property value as a ReadOnlyMemory of bytes.
        /// This is the most performant way to access the value when passing it through without modification.
        /// </summary>
        public ReadOnlyMemory<byte> ValueBuffer { get; }

        /// <summary>
        /// Gets the property value as a string.
        /// This decodes the internal byte buffer as UTF-8.
        /// </summary>
        public string Value => ValueBuffer.IsEmpty ? string.Empty : Encoding.UTF8.GetString(ValueBuffer.Span);
    }
}