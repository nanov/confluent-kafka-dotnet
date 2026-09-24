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
using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Confluent.Kafka.Admin;
using Confluent.Kafka.Internal;
using NM = Confluent.Kafka.Impl.NativeMethods.NativeMethods;


namespace Confluent.Kafka.Impl
{
    /// <summary>
    ///     Native AOT compatible librdkafka binding (net8.0+).
    ///
    ///     Upstream binds the native library by reflecting over one of three
    ///     copy/pasted DllImport classes (one per DllName) and creating a
    ///     delegate per function. Here a single source-generated
    ///     <see cref="NM"/> class is used and the DllName -> file mapping
    ///     (librdkafka / alpine-librdkafka / centos8-librdkafka / user path) is
    ///     performed by a DllImportResolver instead, so no reflection is
    ///     required. SetDelegates() (generated, see
    ///     LibRdKafka.Modern.SetDelegates.cs) assigns the very same delegate
    ///     fields the upstream wrappers call through, which keeps LibRdKafka.cs
    ///     otherwise untouched.
    /// </summary>
    internal static partial class Librdkafka
    {
        private static IntPtr nativeLibraryHandle;
        private static string userSpecifiedLibraryPath;
        private static bool resolverRegistered;

        /// <summary>
        ///     net8.0+ counterpart of Load{NetStandard,OSX,Linux}Delegates.
        ///     Called with loadLockObj held.
        /// </summary>
        private static void LoadModern(string userSpecifiedPath)
        {
            userSpecifiedLibraryPath = userSpecifiedPath;

            if (!resolverRegistered)
            {
                NativeLibrary.SetDllImportResolver(typeof(Librdkafka).Assembly, ResolveLibrary);
                resolverRegistered = true;
            }

            if (!SetDelegates())
            {
                throw new DllNotFoundException("Failed to load the librdkafka native library.");
            }
        }

        /// <summary>
        ///     Maps the "librdkafka" DllName to the native library to load.
        ///     Invoked by the runtime lazily, once per P/Invoke entry point, so
        ///     the resolved handle is cached.
        /// </summary>
        private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != NM.DllName)
            {
                return IntPtr.Zero;
            }

            var handle = nativeLibraryHandle;
            if (handle != IntPtr.Zero)
            {
                return handle;
            }

            if (userSpecifiedLibraryPath != null)
            {
                // Throws DllNotFoundException including the path on failure.
                handle = NativeLibrary.Load(userSpecifiedLibraryPath);
            }
            else
            {
                var probe = searchPath ?? (DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories);
                foreach (var candidate in NativeLibraryCandidates())
                {
                    // Standard runtime probing: deps.json native assets,
                    // runtimes/<rid>/native, the application directory, and
                    // platform name decoration (lib prefix, .so/.dylib/.dll).
                    if (NativeLibrary.TryLoad(candidate, assembly, probe, out handle))
                    {
                        break;
                    }
                }
            }

            if (handle == IntPtr.Zero)
            {
                // Let the runtime raise DllNotFoundException for the import.
                return IntPtr.Zero;
            }

            nativeLibraryHandle = handle;
            return handle;
        }

        /// <summary>
        ///     Same candidate order as the upstream Load*Delegates methods.
        /// </summary>
        private static string[] NativeLibraryCandidates()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new[] { NM.DllName };
            }

            // The lists are generated from the upstream NativeMethods_*.cs
            // variants; this selection mirrors upstream's LoadLinuxDelegates.
            var osName = PlatformApis.GetOSName();
            return osName.Equals("alpine", StringComparison.OrdinalIgnoreCase)
                ? NM.AlpineDllNames
                : NM.LinuxDllNames;
        }

        /// <summary>
        ///     Registers the client instance the static NativeCallbacks entry
        ///     points dispatch to. Must be called before any conf_set_*_cb.
        /// </summary>
        internal static void conf_set_opaque(IntPtr conf, IntPtr opaque)
            => NM.rd_kafka_conf_set_opaque(conf, opaque);

        internal static IntPtr opaque(IntPtr rk)
            => NM.rd_kafka_opaque(rk);

        /// <summary>
        ///     Bridges upstream wrapper signatures that the P/Invoke source
        ///     generator cannot express (StringBuilder error buffers, managed
        ///     callback delegates) onto the generated imports, so the delegate
        ///     fields and wrappers in LibRdKafka.cs keep their upstream shape.
        ///     The mechanical adapters are generated (LibRdKafka.Modern.SetDelegates.cs);
        ///     this hand written part holds the helpers and the callback
        ///     registrations that use [UnmanagedCallersOnly] trampolines
        ///     instead of delegate marshalling. All of these run on cold paths.
        /// </summary>
        private static unsafe partial class Adapters
        {
            /// <summary>
            ///     Rents a zeroed buffer of at least <paramref name="size"/>
            ///     bytes to receive a NUL terminated error string.
            /// </summary>
            private static byte[] RentErrStr(UIntPtr size)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(Math.Max((int)size, 1));
                buffer[0] = 0;
                return buffer;
            }

            /// <summary>
            ///     Copies the NUL terminated UTF-8 string in
            ///     <paramref name="buffer"/> into <paramref name="dest"/> and
            ///     returns the buffer to the pool.
            /// </summary>
            private static void ReturnErrStr(byte[] buffer, StringBuilder dest)
            {
                if (dest != null)
                {
                    var length = Array.IndexOf(buffer, (byte)0);
                    if (length < 0)
                    {
                        length = buffer.Length;
                    }
                    dest.Clear();
                    dest.Append(Encoding.UTF8.GetString(buffer, 0, length));
                }
                ArrayPool<byte>.Shared.Return(buffer);
            }

            private static IntPtr FunctionPointer<TDelegate>(TDelegate callback) where TDelegate : Delegate
                => callback == null ? IntPtr.Zero : Marshal.GetFunctionPointerForDelegate<TDelegate>(callback);

            // Client callback registration.
            //
            // Upstream passes an instance-method delegate of the client here
            // and lets the runtime marshal it. On net8.0+ the delegate argument
            // is deliberately ignored: the static [UnmanagedCallersOnly] entry
            // point of NativeCallbacks is registered instead, and it reaches
            // the same client method through the opaque set by conf_set_opaque
            // (INativeCallbackTarget). This keeps the registration code in
            // Consumer / Producer identical to upstream. A callback added
            // upstream without a NativeCallbacks counterpart can be bound with
            // FunctionPointer(cb) in the meantime (delegate marshalling also
            // works under AOT, just less efficiently).

            internal static unsafe void rd_kafka_conf_set_dr_msg_cb(IntPtr conf, DeliveryReportDelegate cb)
                => NM.rd_kafka_conf_set_dr_msg_cb(conf, (IntPtr)NativeCallbacks.DeliveryReportPtr);

            internal static unsafe void rd_kafka_conf_set_rebalance_cb(IntPtr conf, RebalanceDelegate cb)
                => NM.rd_kafka_conf_set_rebalance_cb(conf, (IntPtr)NativeCallbacks.RebalancePtr);

            internal static unsafe void rd_kafka_conf_set_offset_commit_cb(IntPtr conf, CommitDelegate cb)
                => NM.rd_kafka_conf_set_offset_commit_cb(conf, (IntPtr)NativeCallbacks.CommitPtr);

            internal static unsafe void rd_kafka_conf_set_error_cb(IntPtr conf, ErrorDelegate cb)
                => NM.rd_kafka_conf_set_error_cb(conf, (IntPtr)NativeCallbacks.ErrorPtr);

            internal static unsafe void rd_kafka_conf_set_log_cb(IntPtr conf, LogDelegate cb)
                => NM.rd_kafka_conf_set_log_cb(conf, (IntPtr)NativeCallbacks.LogPtr);

            internal static unsafe void rd_kafka_conf_set_stats_cb(IntPtr conf, StatsDelegate cb)
                => NM.rd_kafka_conf_set_stats_cb(conf, (IntPtr)NativeCallbacks.StatsPtr);

            internal static unsafe void rd_kafka_conf_set_oauthbearer_token_refresh_cb(IntPtr conf, OAuthBearerTokenRefreshDelegate cb)
                => NM.rd_kafka_conf_set_oauthbearer_token_refresh_cb(conf, (IntPtr)NativeCallbacks.OAuthBearerTokenRefreshPtr);

            /// <summary>
            ///     The partitioner callback is a per-topic-config closure (see
            ///     Producer) rather than a client method, so it is reached
            ///     through the topic config opaque: a weak GCHandle to the
            ///     delegate (the Producer keeps its own strong handle for the
            ///     lifetime of the client). The weak handle is released when
            ///     the delegate is collected.
            /// </summary>
            internal static unsafe void rd_kafka_topic_conf_set_partitioner_cb(IntPtr topic_conf, PartitionerDelegate cb)
            {
                var weakHandle = partitionerHandles.GetValue(cb, static d => new WeakHandle(d)).Handle;
                NM.rd_kafka_topic_conf_set_opaque(topic_conf, GCHandle.ToIntPtr(weakHandle));
                NM.rd_kafka_topic_conf_set_partitioner_cb(topic_conf, (IntPtr)NativeCallbacks.PartitionerPtr);
            }

            private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PartitionerDelegate, WeakHandle> partitionerHandles = new();

            private sealed class WeakHandle
            {
                internal readonly GCHandle Handle;
                internal WeakHandle(Delegate target) { Handle = GCHandle.Alloc(target, GCHandleType.Weak); }
                ~WeakHandle() { if (Handle.IsAllocated) { Handle.Free(); } }
            }
        }
    }
}
#endif
