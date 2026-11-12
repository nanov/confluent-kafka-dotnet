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
        // A process-lifetime, non-null address used for a PRESENT zero-length payload. librdkafka
        // treats a NULL payload pointer as a tombstone and a non-NULL pointer with length 0 as a
        // present empty value, but pinning an empty span yields NULL. The length is always 0 when
        // this is used, so the byte is never read. Never freed.
        private static readonly IntPtr EmptyPayloadPointer = Marshal.AllocHGlobal(1);

        /// <summary>
        ///     Returns the pointer librdkafka needs: NULL for a null payload, a stable non-null
        ///     address for a present empty payload, otherwise <paramref name="pinned"/>.
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
        ///     Produces a message WITHOUT copying, carrying both header collections
        ///     concatenated in order (<paramref name="headers1"/> then
        ///     <paramref name="headers2"/>) into one native header list.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            in KafkaHeaders headers1,
            in KafkaHeaders headers2,
            IntPtr opaque = default)
            => ProduceNoCopyWithHeadersCore(producer, topic, Partition.Any, key, keyLength, value, valueLength, in headers1, in headers2, opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition, carrying
        ///     both header collections concatenated in order.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            Partition partition,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            in KafkaHeaders headers1,
            in KafkaHeaders headers2,
            IntPtr opaque = default)
            => ProduceNoCopyWithHeadersCore(producer, topic, partition, key, keyLength, value, valueLength, in headers1, in headers2, opaque);

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition, with a key and value
        ///     that can each be null (a tombstone or absent key) or present, including present with
        ///     zero length. When <paramref name="keyIsNull"/> / <paramref name="valueIsNull"/> is
        ///     true a NULL payload is sent and the pointer is ignored; when false with length 0 a
        ///     present empty payload is sent. Same memory-lifetime contract as the other no-copy
        ///     overloads.
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
        ///     Produces a message WITHOUT copying, to a specific partition, with headers and a
        ///     nullable key and value. See the headerless overload taking <c>keyIsNull</c>.
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

        /// <summary>
        ///     Produces a message WITHOUT copying, to a specific partition, carrying both header
        ///     collections and a nullable key and value. See the headerless overload taking
        ///     <c>keyIsNull</c>.
        /// </summary>
        public static void ProduceNoCopy(
            ref IRawProducer producer,
            string topic,
            Partition partition,
            IntPtr key, int keyLength, bool keyIsNull,
            IntPtr value, int valueLength, bool valueIsNull,
            in KafkaHeaders headers1,
            in KafkaHeaders headers2,
            IntPtr opaque = default)
            => ProduceNoCopyWithHeadersCore(producer, topic, partition,
                Resolve(key, keyLength, keyIsNull), keyLength,
                Resolve(value, valueLength, valueIsNull), valueLength,
                in headers1, in headers2, opaque);

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
            producer.ThrowIfError(err);
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

        private static void ProduceNoCopyWithHeadersCore(
            IRawProducer producer,
            string topic,
            int partition,
            IntPtr key, int keyLength,
            IntPtr value, int valueLength,
            in KafkaHeaders headers1,
            in KafkaHeaders headers2,
            IntPtr opaque)
        {
            if (producer == null) throw new ArgumentNullException(nameof(producer));

            producer.ProduceRawWithHeaders(
                topic, partition,
                key, keyLength,
                value, valueLength,
                in headers1, in headers2,
                IntPtr.Zero,            // no flags = no copy
                opaque);
        }
    }
}
