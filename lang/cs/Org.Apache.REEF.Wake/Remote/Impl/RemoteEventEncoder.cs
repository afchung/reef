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

using System.Net;
using Org.Apache.REEF.Wake.Remote.Proto;

namespace Org.Apache.REEF.Wake.Remote.Impl
{
    internal sealed class RemoteEventEncoder<T> : IEncoder<IRemoteEvent<T>>
    {
        private readonly IEncoder<T> _encoder;

        public RemoteEventEncoder(IEncoder<T> encoder)
        {
            _encoder = encoder;
        }

        public byte[] Encode(IRemoteEvent<T> obj)
        {
            return new WakeMessagePBuf
            {
                source = EncodeIPEndpoint(obj.LocalEndPoint),
                sink = EncodeIPEndpoint(obj.RemoteEndPoint),
                data = _encoder.Encode(obj.Value),
                seq = obj.Sequence,
            }.Serialize();
        }

        private static string EncodeIPEndpoint(IPEndPoint endpoint)
        {
            if (endpoint == null)
            {
                return null;
            }

            return endpoint.Address + ":" + endpoint.Port;
        }
    }
}
