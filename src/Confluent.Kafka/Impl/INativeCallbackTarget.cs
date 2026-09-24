// Copyright 2016-2023 Confluent Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Refer to LICENSE for more information.

using System;


namespace Confluent.Kafka.Impl
{
    /// <summary>
    ///     Receiver of librdkafka callbacks common to all client types.
    ///
    ///     On net8.0+ librdkafka calls static [UnmanagedCallersOnly] entry
    ///     points (see NativeCallbacks) which locate the owning client through
    ///     the librdkafka opaque pointer (a GCHandle) and dispatch through this
    ///     interface. The method signatures mirror the private callback
    ///     methods of Consumer / Producer so the implementations are plain
    ///     forwards.
    /// </summary>
    internal interface INativeCallbackTarget
    {
        void ErrorCallback(IntPtr rk, ErrorCode err, string reason, IntPtr opaque);

        int StatisticsCallback(IntPtr rk, IntPtr json, UIntPtr json_len, IntPtr opaque);

        void OAuthBearerTokenRefreshCallback(IntPtr rk, IntPtr oauthbearer_config, IntPtr opaque);

        void LogCallback(IntPtr rk, SyslogLevel level, string fac, string buf);
    }

    /// <summary>
    ///     Receiver of the consumer specific librdkafka callbacks.
    /// </summary>
    internal interface IConsumerCallbackTarget : INativeCallbackTarget
    {
        void RebalanceCallback(IntPtr rk, ErrorCode err, IntPtr partitions, IntPtr opaque);

        void CommitCallback(IntPtr rk, ErrorCode err, IntPtr offsets, IntPtr opaque);
    }

    /// <summary>
    ///     Receiver of the producer specific librdkafka callbacks.
    /// </summary>
    internal interface IProducerCallbackTarget : INativeCallbackTarget
    {
        void DeliveryReportCallback(IntPtr rk, IntPtr rkmessage, IntPtr opaque);
    }
}
