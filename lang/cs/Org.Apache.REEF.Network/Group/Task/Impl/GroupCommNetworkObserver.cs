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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Org.Apache.REEF.Network.Group.Driver.Impl;
using Org.Apache.REEF.Network.NetworkService;
using Org.Apache.REEF.Tang.Annotations;
using Org.Apache.REEF.Tang.Implementations.InjectionPlan;
using Org.Apache.REEF.Utilities.Diagnostics;
using Org.Apache.REEF.Utilities.Logging;
using Org.Apache.REEF.Wake.Remote;
using Org.Apache.REEF.Wake.Remote.Impl;

namespace Org.Apache.REEF.Network.Group.Task.Impl
{
    /// <summary>
    /// Handles all incoming messages for this Task.
    /// Writable version
    /// </summary>
    internal sealed class GroupCommNetworkObserver : IGroupCommNetworkObserver
    {
        private static readonly Logger LOGGER = Logger.GetLogger(typeof(GroupCommNetworkObserver));

        private readonly IInjectionFuture<StreamingNetworkService<GeneralGroupCommunicationMessage>> _networkService;

        private readonly ConcurrentDictionary<string, IObserver<NsMessage<GeneralGroupCommunicationMessage>>> _taskMessageObservers =
            new ConcurrentDictionary<string, IObserver<NsMessage<GeneralGroupCommunicationMessage>>>();

        private readonly ConcurrentDictionary<string, byte> _registeredNodes = 
            new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// Creates a new GroupCommNetworkObserver.
        /// </summary>
        [Inject]
        private GroupCommNetworkObserver(
            IInjectionFuture<StreamingNetworkService<GeneralGroupCommunicationMessage>> networkService)
        {
            _networkService = networkService;
        }

        public TaskMessageObserver<T> Register<T>(string taskSourceId)
        {
            return _taskMessageObservers
                .GetOrAdd(taskSourceId, new TaskMessageObserver<T>())
                as TaskMessageObserver<T>;
        }

        /// <summary>
        /// Handles the incoming WritableNsMessage for this Task.
        /// Delegates the GeneralGroupCommunicationMessage to the correct 
        /// WritableCommunicationGroupNetworkObserver.
        /// </summary>
        public void OnNext(IRemoteMessage<NsMessage<GeneralGroupCommunicationMessage>> remoteMessage)
        {
            try
            {
                var nsMessage = remoteMessage.Message;
                var gcm = nsMessage.Data.First();
                var gcMessageTaskSource = gcm.Source;
                IObserver<NsMessage<GeneralGroupCommunicationMessage>> observer;
                if (!_taskMessageObservers.TryGetValue(gcMessageTaskSource, out observer))
                {
                    throw new KeyNotFoundException("Unable to find registered NodeMessageObserver for source Task " +
                                                   gcMessageTaskSource + ".");
                }

                _registeredNodes.GetOrAdd(gcMessageTaskSource,
                    id =>
                    {
                        var socketRemoteId = remoteMessage.Identifier as SocketRemoteIdentifier;
                        if (socketRemoteId == null)
                        {
                            throw new InvalidOperationException();
                        }

                        _networkService.Get().RemoteManager.RegisterObserver(
                            socketRemoteId.Addr, observer);

                        return new byte();
                    });
            }
            catch (Exception e)
            {
                Exceptions.CaughtAndThrow(e, Level.Error, LOGGER);
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}
