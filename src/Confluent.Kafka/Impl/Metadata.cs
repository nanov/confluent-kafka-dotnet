// Copyright 2016-2017 Confluent Inc., 2015-2016 Andreas Heider
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
// Derived from: rdkafka-dotnet, licensed under the 2-clause BSD License.
//
// Refer to LICENSE for more information.

using System;
using System.Runtime.InteropServices;


namespace Confluent.Kafka.Impl
{
    // Note: all of these structs are blittable (native strings are kept as
    // char* and decoded with Util.Marshal.PtrToStringUTF8) so they can be read
    // in place with Util.Marshal.ReadStruct, without runtime marshalling.

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_metadata_broker {
        internal int id;
        internal /* char * */ IntPtr host;
        internal int port;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_metadata_partition {
        internal int id;
        internal ErrorCode err;
        internal int leader;
        internal int replica_cnt;
        internal /* int32_t * */ IntPtr replicas;
        internal int isr_cnt;
        internal /* int32_t * */ IntPtr isrs;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_metadata_topic {
        internal /* char * */ IntPtr topic;
        internal int partition_cnt;
        internal /* struct rd_kafka_metadata_partition * */ IntPtr partitions;
        internal ErrorCode err;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_metadata {
        internal int broker_cnt;
        internal /* struct rd_kafka_metadata_broker * */ IntPtr brokers;
        internal int topic_cnt;
        internal /* struct rd_kafka_metadata_topic * */ IntPtr topics;
        internal int orig_broker_id;
        internal /* char * */ IntPtr orig_broker_name;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_group_member_info
    {
        internal /* char * */ IntPtr member_id;
        internal /* char * */ IntPtr client_id;
        internal /* char * */ IntPtr client_host;
        internal IntPtr member_metadata;
        internal IntPtr member_metadata_size;
        internal IntPtr member_assignment;
        internal IntPtr member_assignment_size;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_group_info
    {
        internal rd_kafka_metadata_broker broker;
        internal /* char * */ IntPtr group;
        internal ErrorCode err;
        internal /* char * */ IntPtr state;
        internal /* char * */ IntPtr protocol_type;
        internal /* char * */ IntPtr protocol;
        internal IntPtr members;
        internal int member_cnt;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct rd_kafka_group_list
    {
        internal IntPtr groups;
        internal int group_cnt;
    };
    
    enum rd_kafka_vtype
    {
        End,       // va-arg sentinel
        Topic,     // (const char *) Topic name
        Rkt,       // (rd_kafka_topic_t *) Topic handle
        Partition, // (int32_t) Partition
        Value,     // (void *, size_t) Message value (payload)
        Key,       // (void *, size_t) Message key
        Opaque,    // (void *) Application opaque
        MsgFlags,  // (int) RD_KAFKA_MSG_F_.. flags
        Timestamp, // (int64_t) Milliseconds since epoch UTC
        Header,    // (const char *, const void *, ssize_t) Message Header
        Headers,   // (rd_kafka_headers_t *) Headers list
    }

    [StructLayout(LayoutKind.Sequential)]
    ref struct ptr_and_size
    {
        public IntPtr ptr;
        public UIntPtr size;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    ref struct vu_data
    {
        [FieldOffset(0)]
        public IntPtr topic;

        [FieldOffset(0)]
        public int partition;

        [FieldOffset(0)]
        public ptr_and_size key;

        [FieldOffset(0)]
        public ptr_and_size val;

        [FieldOffset(0)]
        public IntPtr opaque;

        [FieldOffset(0)]
        public IntPtr msgflags;

        [FieldOffset(0)]
        public long timestamp;

        [FieldOffset(0)]
        public IntPtr headers;
    }

    [StructLayout(LayoutKind.Sequential)]
    ref struct rd_kafka_vu
    {
        public rd_kafka_vtype vt;
        public vu_data data;
    };
}
