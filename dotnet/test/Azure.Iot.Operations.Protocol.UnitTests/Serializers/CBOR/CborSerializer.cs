﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Dahomey.Cbor;
    using Azure.Iot.Operations.Protocol;

#pragma warning disable VSTHRD002 // Synchronously waiting on tasks or awaiters may cause deadlocks. Use await or JoinableTaskFactory.Run instead.

    public class CborSerializer : IPayloadSerializer
    {
        protected static readonly CborOptions cborOptions = new()
        {
            DateTimeFormat = Dahomey.Cbor.DateTimeFormat.ISO8601,
            ObjectFormat = Dahomey.Cbor.Attributes.CborObjectFormat.IntKeyMap,
        };

        static CborSerializer()
        {
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(TimeSpan), new DurationCborConverter());
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(DateOnly), new DateCborConverter());
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(TimeOnly), new TimeCborConverter());
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(Guid), new UuidCborConverter());
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(byte[]), new BytesCborConverter());
        }

        public string DefaultContentType => "application/cbor";

        public int DefaultPayloadFormatIndicator => 0;

        public DeserializedPayloadContext<T> FromBytes<T>(byte[]? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
            try
            {
                if (payload == null)
                {
                    if (typeof(T) != typeof(EmptyCbor))
                    {
                        throw AkriMqttException.GetPayloadInvalidException();
                    }

                    return new((new EmptyCbor() as T)!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }

                using (var stream = new MemoryStream(payload))
                {
                    ValueTask<T> task = Cbor.DeserializeAsync<T>(stream, cborOptions);
                    return new(task.IsCompletedSuccessfully ? task.Result : default!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }

        public SerializedPayloadContext ToBytes<T>(T? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(EmptyCbor))
                {
                    return new(null, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }

                using (var stream = new MemoryStream())
                {
                    Cbor.SerializeAsync(payload, stream, cborOptions).Wait();
                    stream.Flush();

                    byte[] buffer = new byte[stream.Length];
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.Read(buffer, 0, (int)stream.Length);

                    return new(buffer, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
