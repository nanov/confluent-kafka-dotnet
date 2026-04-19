using System;
using System.Runtime.InteropServices;
using Confluent.Kafka.Impl;

namespace Confluent.Kafka
{
    /// <summary>
    ///     An allocation-free, stack-only view over a producer delivery report.
    ///     Wraps a native <c>rd_kafka_message</c> pointer owned by librdkafka
    ///     for the duration of the callback — do not store or dispose.
    /// </summary>
    public unsafe readonly ref struct RawDeliveryReport
    {
        private readonly rd_kafka_message* msg;

        internal RawDeliveryReport(rd_kafka_message* msg)
        {
            this.msg = msg;
        }

        /// <summary>
        ///     The error code for this delivery. <see cref="ErrorCode.NoError"/> on success.
        /// </summary>
        public ErrorCode ErrorCode => msg->err;

        /// <summary>True if delivery failed.</summary>
        public bool IsError => msg->err != ErrorCode.NoError;

        /// <summary>The partition the message was delivered to.</summary>
        public Partition Partition => msg->partition;

        /// <summary>The offset assigned by the broker.</summary>
        public Offset Offset => msg->offset;

        /// <summary>
        ///     The leader epoch at the time of delivery, if available.
        /// </summary>
        public int? LeaderEpoch =>
            msg->rkt != IntPtr.Zero && msg->offset != Offset.Unset
                ? Librdkafka.message_leader_epoch((IntPtr)msg)
                : null;

        /// <summary>
        ///     The topic name as a UTF-8 read-only span pointing directly into
        ///     librdkafka's topic handle.
        /// </summary>
        public ReadOnlySpan<byte> Topic
        {
            get
            {
                if (msg->rkt == IntPtr.Zero) return ReadOnlySpan<byte>.Empty;
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

        /// <summary>The broker-assigned timestamp.</summary>
        public Timestamp Timestamp
        {
            get
            {
                var ts = Librdkafka.message_timestamp((IntPtr)msg, out var type);
                return new Timestamp(ts, (TimestampType)type);
            }
        }

        /// <summary>
        ///     Persistence status reported by librdkafka.
        /// </summary>
        public PersistenceStatus Status => Librdkafka.message_status((IntPtr)msg);

        /// <summary>
        ///     The produced message key as a read-only span directly into
        ///     librdkafka's buffer. Empty if the key was null.
        /// </summary>
        public ReadOnlySpan<byte> Key =>
            msg->key == IntPtr.Zero
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(msg->key.ToPointer(), (int)msg->key_len);

        /// <summary>
        ///     The produced message value as a read-only span directly into
        ///     librdkafka's buffer. Empty if the value was null.
        /// </summary>
        public ReadOnlySpan<byte> Value =>
            msg->val == IntPtr.Zero
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(msg->val.ToPointer(), (int)msg->len);

        /// <summary>
        ///     The per-message opaque pointer passed to the produce call, or
        ///     <see cref="IntPtr.Zero"/> if none was supplied. Useful for
        ///     correlating delivery reports with caller-side state without
        ///     maintaining a managed dictionary.
        /// </summary>
        public IntPtr Opaque => msg->_private;

        /// <summary>
        ///     Allocation-free, foreachable view over the produced message's headers.
        /// </summary>
        public RawHeaders Headers
        {
            get
            {
                Librdkafka.message_headers((IntPtr)msg, out IntPtr hdrsPtr);
                return new RawHeaders(hdrsPtr);
            }
        }
    }
}
