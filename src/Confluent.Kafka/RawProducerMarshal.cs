using System;
using System.Runtime.InteropServices;
using Confluent.Kafka.Impl;

namespace Confluent.Kafka
{
    /// <summary>
    ///     Unsafe, performance-oriented operations on <see cref="IRawProducer"/>.
    ///     These bypass safety guarantees of the primary API — caller is responsible
    ///     for correct memory lifetime and pinning. Invoke explicitly to make the
    ///     opt-in visible at the call site: <c>RawProducerMarshal.ProduceNoCopy(ref p, ...)</c>.
    /// </summary>
    public static class RawProducerMarshal
    {
        // A single, process-lifetime, non-null byte used to emit a PRESENT zero-length payload.
        // librdkafka's produceva distinguishes a NULL payload pointer (tombstone) from a non-null
        // pointer with len 0 (present-empty); a pinned empty span gives a NULL pointer, so we
        // substitute this stable address when the caller meant "present but empty". len is always 0
        // here, so the byte is never read — only its non-null-ness matters. Intentionally never freed.
        private static readonly IntPtr EmptyPayloadPointer = Marshal.AllocHGlobal(1);

        /// <summary>
        ///     Maps a pinned pointer + length + null-intent to the pointer librdkafka needs:
        ///     NULL for a tombstone, a stable non-null sentinel for a present-empty payload, or the
        ///     pinned pointer otherwise.
        /// </summary>
        internal static IntPtr Resolve(IntPtr pinned, int length, bool isNull)
            => isNull ? IntPtr.Zero
             : length == 0 ? EmptyPayloadPointer
             : pinned;
        /// <summary>
        ///     Produces a message WITHOUT copying its payload into librdkafka. The
        ///     caller MUST keep the key/value memory valid and pinned until the
        ///     delivery report fires — failure to do so results in heap corruption
        ///     or a crash. Use <see cref="RawDeliveryReport.Opaque"/> to correlate
        ///     this produce call with its delivery report.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            IntPtr opaque = default)
            => ProduceNoCopyCore(producer, topic, Partition.Any, key, keyLength, value, valueLength, opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            Partition partition,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            IntPtr opaque = default)
            => ProduceNoCopyCore(producer, topic, partition, key, keyLength, value, valueLength, opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, with headers.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            in KafkaHeaders headers,
            IntPtr opaque = default)
            => ProduceNoCopyWithHeadersCore(producer, topic, Partition.Any, key, keyLength, value, valueLength, in headers, opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition, with headers.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            Partition partition,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            in KafkaHeaders headers,
            IntPtr opaque = default)
            => ProduceNoCopyWithHeadersCore(producer, topic, partition, key, keyLength, value, valueLength, in headers, opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition, distinguishing a null
        ///     key/value (tombstone) from a present zero-length one. <paramref name="keyIsNull"/> /
        ///     <paramref name="valueIsNull"/> carry the intent a pinned empty pointer can't: when
        ///     true a NULL payload is sent; when false with length 0 a present-empty payload is sent.
        ///     Same memory-lifetime contract as the other no-copy overloads.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            Partition partition,
            IntPtr key, int keyLength, bool keyIsNull,
            IntPtr value, int valueLength, bool valueIsNull,
            IntPtr opaque = default)
            => ProduceNoCopyCore(producer, topic, partition,
                Resolve(key, keyLength, keyIsNull), keyLength,
                Resolve(value, valueLength, valueIsNull), valueLength,
                opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition, with headers,
        ///     distinguishing a null key/value (tombstone) from a present zero-length one. See the
        ///     headerless null-aware overload for the meaning of the <c>*IsNull</c> flags.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            Partition partition,
            IntPtr key, int keyLength, bool keyIsNull,
            IntPtr value, int valueLength, bool valueIsNull,
            in KafkaHeaders headers,
            IntPtr opaque = default)
            => ProduceNoCopyWithHeadersCore(producer, topic, partition,
                Resolve(key, keyLength, keyIsNull), keyLength,
                Resolve(value, valueLength, valueIsNull), valueLength,
                in headers, opaque);

        private static void ProduceNoCopyCore(
            IRawProducer producer,
            string topic,
            int partition,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            IntPtr opaque)
        {
            if (producer == null) throw new ArgumentNullException(nameof(producer));

            var err = producer.ProduceRawCore(
                topic, partition,
                key, keyLength,
                value, valueLength,
                IntPtr.Zero,
                IntPtr.Zero,        // no flags = no copy
                opaque);
            if (err != ErrorCode.NoError)
            {
                throw new KafkaException(producer.Handle.LibrdkafkaHandle.CreatePossiblyFatalError(err, null));
            }
        }

        private static void ProduceNoCopyWithHeadersCore(
            IRawProducer producer,
            string topic,
            int partition,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            in KafkaHeaders headers,
            IntPtr opaque)
        {
            if (producer == null) throw new ArgumentNullException(nameof(producer));

            producer.ProduceRawWithHeaders(
                topic, partition,
                key, keyLength,
                value, valueLength,
                in headers,
                IntPtr.Zero,            // no flags = no copy
                opaque);
        }
    }
}
