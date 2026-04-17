using System;
using System.Runtime.InteropServices;
using Confluent.Kafka.Impl;

namespace Confluent.Kafka
{
    /// <summary>
    ///     An allocation-free, stack-only view over a single consumed Kafka message.
    ///     Wraps a native <c>rd_kafka_message</c> pointer and exposes its fields as
    ///     <see cref="ReadOnlySpan{T}"/> slices into librdkafka's buffer.
    ///
    ///     The spans are valid only for the lifetime of this struct. Dispose to
    ///     release the native message.
    /// </summary>
    public unsafe ref struct RawMessage
    {
        private rd_kafka_message* msg;

        internal RawMessage(IntPtr msgPtr)
        {
            msg = (rd_kafka_message*)msgPtr;
        }

        /// <summary>
        ///     True when the poll returned no message (timeout elapsed).
        /// </summary>
        public bool IsEmpty => msg == null;

        /// <summary>
        ///     The error code for this message. <see cref="ErrorCode.NoError"/> for a
        ///     normal message, <see cref="ErrorCode.Local_PartitionEOF"/> at EOF, or
        ///     another code on error.
        /// </summary>
        public ErrorCode ErrorCode => msg == null ? ErrorCode.NoError : msg->err;

        /// <summary>
        ///     True if this result indicates the end of a partition was reached.
        /// </summary>
        public bool IsPartitionEOF => msg != null && msg->err == ErrorCode.Local_PartitionEOF;

        /// <summary>
        ///     The topic name as a UTF-8 read-only span pointing directly into
        ///     librdkafka's topic handle. Empty if no message is present.
        /// </summary>
        public ReadOnlySpan<byte> Topic
        {
            get
            {
                if (msg == null || msg->rkt == IntPtr.Zero) return ReadOnlySpan<byte>.Empty;
                var p = (byte*)Librdkafka.topic_name(msg->rkt).ToPointer();
#if NET7_0_OR_GREATER
                return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(p);
#else
                int len = 0;
                while (p[len] != 0) len++;
                return new ReadOnlySpan<byte>(p, len);
#endif
            }
        }

        /// <summary>
        ///     The partition this message was consumed from.
        /// </summary>
        public Partition Partition => msg == null ? Partition.Any : msg->partition;

        /// <summary>
        ///     The offset of this message within its partition.
        /// </summary>
        public Offset Offset => msg == null ? Offset.Unset : msg->offset;

        /// <summary>
        ///     The leader epoch at the time this message was consumed, if available.
        /// </summary>
        public int? LeaderEpoch
        {
            get
            {
                if (msg == null || msg->rkt == IntPtr.Zero || msg->offset == Offset.Unset) return null;
                return Librdkafka.message_leader_epoch((IntPtr)msg);
            }
        }

        /// <summary>
        ///     The message timestamp.
        /// </summary>
        public Timestamp Timestamp
        {
            get
            {
                if (msg == null) return new Timestamp(0, TimestampType.NotAvailable);
                var ts = Librdkafka.message_timestamp((IntPtr)msg, out var type);
                return new Timestamp(ts, (TimestampType)type);
            }
        }

        /// <summary>
        ///     The message key as a read-only span directly into the native buffer.
        ///     Empty if the key is null or no message is present.
        /// </summary>
        public ReadOnlySpan<byte> Key
        {
            get
            {
                if (msg == null || msg->key == IntPtr.Zero) return ReadOnlySpan<byte>.Empty;
                return new ReadOnlySpan<byte>(msg->key.ToPointer(), (int)msg->key_len);
            }
        }

        /// <summary>
        ///     The message value as a read-only span directly into the native buffer.
        ///     Empty if the value is null or no message is present.
        /// </summary>
        public ReadOnlySpan<byte> Value
        {
            get
            {
                if (msg == null || msg->val == IntPtr.Zero) return ReadOnlySpan<byte>.Empty;
                return new ReadOnlySpan<byte>(msg->val.ToPointer(), (int)msg->len);
            }
        }

        /// <summary>
        ///     Allocation-free, foreachable view over this message's headers.
        ///     Empty when no message is present or the message carries no headers.
        ///     The returned <see cref="RawHeaders"/> is valid only for the lifetime
        ///     of this <see cref="RawMessage"/>.
        /// </summary>
        public RawHeaders Headers
        {
            get
            {
                if (msg == null) return default;
                Librdkafka.message_headers((IntPtr)msg, out IntPtr hdrsPtr);
                return new RawHeaders(hdrsPtr);
            }
        }

        /// <summary>
        ///     Releases the underlying native message. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (msg != null)
            {
                Librdkafka.message_destroy((IntPtr)msg);
                msg = null;
            }
        }
    }
}
