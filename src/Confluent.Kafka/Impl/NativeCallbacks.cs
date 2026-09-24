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

#if NET8_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Confluent.Kafka.Internal;
using NM = Confluent.Kafka.Impl.NativeMethods.NativeMethods;

#pragma warning disable CS3016 // CallConvs array attribute argument is not CLS compliant.


namespace Confluent.Kafka.Impl
{
    /// <summary>
    ///     Static, Native AOT friendly entry points for librdkafka callbacks
    ///     (net8.0+). No delegate marshalling / reverse P/Invoke thunks are
    ///     involved: librdkafka receives plain function pointers and the
    ///     owning client is recovered from the opaque pointer registered with
    ///     rd_kafka_conf_set_opaque (a weak GCHandle to the client).
    ///
    ///     Exceptions must never cross the native boundary (they would tear
    ///     the process down), so every entry point swallows them; the client
    ///     callbacks themselves already record handler exceptions to rethrow
    ///     on the calling thread.
    /// </summary>
    internal static unsafe class NativeCallbacks
    {
        private static T Target<T>(IntPtr opaque) where T : class
            => opaque == IntPtr.Zero ? null : GCHandle.FromIntPtr(opaque).Target as T;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Error(IntPtr rk, ErrorCode err, byte* reason, IntPtr opaque)
        {
            try
            {
                Target<INativeCallbackTarget>(opaque)?.ErrorCallback(rk, err, Util.Marshal.PtrToStringUTF8((IntPtr)reason), opaque);
            }
            catch (Exception) { }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int Stats(IntPtr rk, IntPtr json, UIntPtr json_len, IntPtr opaque)
        {
            try
            {
                return Target<INativeCallbackTarget>(opaque)?.StatisticsCallback(rk, json, json_len, opaque) ?? 0;
            }
            catch (Exception)
            {
                return 0; // instruct librdkafka to immediately free the json ptr.
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void OAuthBearerTokenRefresh(IntPtr rk, IntPtr oauthbearer_config, IntPtr opaque)
        {
            try
            {
                Target<INativeCallbackTarget>(opaque)?.OAuthBearerTokenRefreshCallback(rk, oauthbearer_config, opaque);
            }
            catch (Exception) { }
        }

        // The log callback does not receive the opaque pointer.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Log(IntPtr rk, SyslogLevel level, byte* fac, byte* buf)
        {
            try
            {
                Target<INativeCallbackTarget>(NM.rd_kafka_opaque(rk))?.LogCallback(
                    rk, level, Util.Marshal.PtrToStringUTF8((IntPtr)fac), Util.Marshal.PtrToStringUTF8((IntPtr)buf));
            }
            catch (Exception) { }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Rebalance(IntPtr rk, ErrorCode err, IntPtr partitions, IntPtr opaque)
        {
            try
            {
                Target<IConsumerCallbackTarget>(opaque)?.RebalanceCallback(rk, err, partitions, opaque);
            }
            catch (Exception) { }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void Commit(IntPtr rk, ErrorCode err, IntPtr offsets, IntPtr opaque)
        {
            try
            {
                Target<IConsumerCallbackTarget>(opaque)?.CommitCallback(rk, err, offsets, opaque);
            }
            catch (Exception) { }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void DeliveryReport(IntPtr rk, IntPtr rkmessage, IntPtr opaque)
        {
            try
            {
                Target<IProducerCallbackTarget>(opaque)?.DeliveryReportCallback(rk, rkmessage, opaque);
            }
            catch (Exception) { }
        }

        /// <summary>
        ///     rkt_opaque is the topic configuration opaque: a GCHandle to the
        ///     <see cref="Librdkafka.PartitionerDelegate"/> closure the Producer
        ///     built around the user's partitioner (see
        ///     Librdkafka.Adapters.rd_kafka_topic_conf_set_partitioner_cb). An
        ///     exception thrown by the user partitioner (which would previously
        ///     have been fatal) results in RD_KAFKA_PARTITION_UA so the message
        ///     fails with a delivery error instead.
        /// </summary>
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int Partitioner(IntPtr rkt, IntPtr keydata, UIntPtr keylen, int partition_cnt, IntPtr rkt_opaque, IntPtr msg_opaque)
        {
            try
            {
                return Target<Librdkafka.PartitionerDelegate>(rkt_opaque)
                    ?.Invoke(rkt, keydata, keylen, partition_cnt, rkt_opaque, msg_opaque) ?? -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        internal static delegate* unmanaged[Cdecl]<IntPtr, ErrorCode, byte*, IntPtr, void> ErrorPtr => &Error;
        internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, UIntPtr, IntPtr, int> StatsPtr => &Stats;
        internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> OAuthBearerTokenRefreshPtr => &OAuthBearerTokenRefresh;
        internal static delegate* unmanaged[Cdecl]<IntPtr, SyslogLevel, byte*, byte*, void> LogPtr => &Log;
        internal static delegate* unmanaged[Cdecl]<IntPtr, ErrorCode, IntPtr, IntPtr, void> RebalancePtr => &Rebalance;
        internal static delegate* unmanaged[Cdecl]<IntPtr, ErrorCode, IntPtr, IntPtr, void> CommitPtr => &Commit;
        internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> DeliveryReportPtr => &DeliveryReport;
        internal static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, UIntPtr, int, IntPtr, IntPtr, int> PartitionerPtr => &Partitioner;
    }
}
#endif
