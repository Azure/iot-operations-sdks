// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Azure.Iot.Operations.Protocol
{
    internal class ExecutionDispatcherCollection : IDisposable
    {
        private readonly SemaphoreSlim mapSemaphore;
        private readonly Dictionary<string, Dispatcher> clientIdCommandDispatcherMap;
        private readonly Func<int?, Dispatcher> commandDispatcherFactory;

        private static readonly ExecutionDispatcherCollection instance;

        public static int DefaultDispatchConcurrency { get; set; } = 10;

        static ExecutionDispatcherCollection()
        {
            instance = new ExecutionDispatcherCollection();
        }

        public static ExecutionDispatcherCollection GetCollectionInstance()
        {
            return instance;
        }

        internal ExecutionDispatcherCollection()
        {
            mapSemaphore = new SemaphoreSlim(1);
            clientIdCommandDispatcherMap = [];
            commandDispatcherFactory = (int? preferredDispatchConcurrency) => new ExecutionDispatcher(preferredDispatchConcurrency ?? DefaultDispatchConcurrency).SubmitAsync;
        }

        internal Dispatcher GetDispatcher(string mqttClientId, int? preferredDispatchConcurrency = null)
        {
            mapSemaphore.Wait();
            if (!clientIdCommandDispatcherMap.TryGetValue(mqttClientId, out Dispatcher? dispatchCommand))
            {
                dispatchCommand = commandDispatcherFactory(preferredDispatchConcurrency);
                clientIdCommandDispatcherMap[mqttClientId] = dispatchCommand;
            }

            mapSemaphore.Release();
            return dispatchCommand;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                mapSemaphore.Dispose();
            }
        }
    }
}
