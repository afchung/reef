﻿// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

using System;
using System.Collections.Generic;
using Org.Apache.REEF.Tang.Annotations;

namespace Org.Apache.REEF.Wake
{
    public sealed class NetworkObserverFactory<T> : IDisposable
    {
        private readonly ISet<IDisposable> _registrationHandle = new HashSet<IDisposable>();
        private readonly IObserverFactory<T> _observerFactory;
        private readonly object _lockObj = new object();
        
        [Inject]
        private NetworkObserverFactory(IObserverFactory<T> observerFactory)
        {
            _observerFactory = observerFactory;
        }

        public IObserver<T> OnNewClient()
        {
            return _observerFactory.Create();
        }

        public void OnNewClientRegistered(IDisposable disposable)
        {
            _registrationHandle.Add(disposable);
        }

        public void Dispose()
        {
            lock (_lockObj)
            {
                foreach (var disposable in _registrationHandle)
                {
                    disposable.Dispose();
                }

                _registrationHandle.Clear();
            }
        }
    }
}