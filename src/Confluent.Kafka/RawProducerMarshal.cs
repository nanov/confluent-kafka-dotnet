using System;
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
