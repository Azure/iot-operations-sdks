// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Azure.Iot.Operations.Protocol
{
    internal class ExecutionDispatcherCollection : IDisposable
    {
        private readonly SemaphoreSlim _mapSemaphore;
        private readonly Dictionary<string, Dispatcher> _clientIdCommandDispatcherMap;
        private readonly Func<int?, Dispatcher> _commandDispatcherFactory;

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
            _mapSemaphore = new SemaphoreSlim(1);
            _clientIdCommandDispatcherMap = [];
            _commandDispatcherFactory = (int? preferredDispatchConcurrency) => new ExecutionDispatcher(preferredDispatchConcurrency ?? DefaultDispatchConcurrency).SubmitAsync;
        }

        internal Dispatcher GetDispatcher(string mqttClientId, int? preferredDispatchConcurrency = null)
        {
            _mapSemaphore.Wait();
            if (!_clientIdCommandDispatcherMap.TryGetValue(mqttClientId, out Dispatcher? dispatchCommand))
            {
                dispatchCommand = _commandDispatcherFactory(preferredDispatchConcurrency);
                _clientIdCommandDispatcherMap[mqttClientId] = dispatchCommand;
            }

            _mapSemaphore.Release();
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
                _mapSemaphore.Dispose();
            }
        }
    }
}
