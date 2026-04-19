using System;

namespace Confluent.Kafka
{
    /// <summary>
    ///     Callback invoked by librdkafka once per produced message after it has
    ///     been acknowledged or failed. The <paramref name="report"/> is stack-only
    ///     and valid only for the duration of the call — do not store it.
    /// </summary>
    public delegate void RawDeliveryReportHandler(in RawDeliveryReport report);

    /// <summary>
    ///     A Kafka producer that exposes an allocation-free produce path via
    ///     <see cref="ReadOnlySpan{T}"/> key/value. Also inherits the standard
    ///     <see cref="IProducer{TKey, TValue}"/> surface (bound to
    ///     <see cref="Ignore"/>, <see cref="Ignore"/>) for Flush/Dispose/etc.
    /// </summary>
    public interface IRawProducer : IProducer<Ignore, Ignore>
    {
        /// <summary>
        ///     Produce a message from <see cref="ReadOnlySpan{T}"/> key and value,
        ///     zero managed allocations on the hot path. The native librdkafka
        ///     layer copies the bytes (MSG_F_COPY) so the spans are only read
        ///     synchronously within this call — safe to reuse after return.
        /// </summary>
        /// <param name="topic">The topic to produce to.</param>
        /// <param name="key">The message key bytes. Pass <c>default</c> for no key.</param>
        /// <param name="value">The message value bytes.</param>
        /// <exception cref="KafkaException">Thrown when the enqueue fails.</exception>
        void RawProduce(string topic, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

        /// <summary>
        ///     Produce a message to a specific partition.
        /// </summary>
        /// <param name="topic">The topic to produce to.</param>
        /// <param name="partition">The target partition. Use <see cref="Partition.Any"/> to let librdkafka pick.</param>
        /// <param name="key">The message key bytes. Pass <c>default</c> for no key.</param>
        /// <param name="value">The message value bytes.</param>
        /// <exception cref="KafkaException">Thrown when the enqueue fails.</exception>
        void RawProduce(string topic, Partition partition, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value);

        /// <summary>
        ///     Produce a message with headers. The caller owns the memory backing
        ///     each header's value and must keep it valid until this call returns.
        ///     Header names are re-encoded to UTF-8 using a stack buffer.
        /// </summary>
        /// <param name="topic">The topic to produce to.</param>
        /// <param name="key">The message key bytes. Pass <c>default</c> for no key.</param>
        /// <param name="value">The message value bytes.</param>
        /// <param name="headers">The headers collection.</param>
        /// <exception cref="KafkaException">Thrown when the enqueue fails.</exception>
        void RawProduce(string topic, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, in KafkaHeaders headers);

        /// <summary>
        ///     Produce a message with headers to a specific partition.
        /// </summary>
        void RawProduce(string topic, Partition partition, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, in KafkaHeaders headers);

        /// <summary>
        ///     Low-level produce entry point. Internal to this assembly — used by
        ///     <see cref="RawProducerMarshal"/> and the public overloads above.
        ///     Not for external callers.
        /// </summary>
        internal ErrorCode ProduceRawCore(
            string topic,
            int partition,
            IntPtr keyPtr, int keyLen,
            IntPtr valuePtr, int valueLen,
            IntPtr headers,
            IntPtr msgFlags,
            IntPtr opaque);

        /// <summary>
        ///     Higher-level produce that builds a native headers handle from
        ///     <paramref name="headers"/>, invokes <see cref="ProduceRawCore"/>,
        ///     and handles ownership/cleanup of the headers handle. Throws
        ///     <see cref="KafkaException"/> on failure.
        /// </summary>
        internal void ProduceRawWithHeaders(
            string topic,
            int partition,
            IntPtr keyPtr, int keyLen,
            IntPtr valuePtr, int valueLen,
            in KafkaHeaders headers,
            IntPtr msgFlags,
            IntPtr opaque);
    }
}
