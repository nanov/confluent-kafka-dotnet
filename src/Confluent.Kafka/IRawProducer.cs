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
    ///     <see cref="KafkaNativeSpan"/> key/value. Also inherits the standard
    ///     <see cref="IProducer{TKey, TValue}"/> surface (bound to
    ///     <see cref="Ignore"/>, <see cref="Ignore"/>) for Flush/Dispose/etc.
    /// </summary>
    public interface IRawProducer : IProducer<Ignore, Ignore>
    {
        /// <summary>
        ///     Produce a message from <see cref="KafkaNativeSpan"/> key and value, zero managed
        ///     allocations on the hot path. The native librdkafka layer copies the bytes
        ///     (MSG_F_COPY) so the payloads are only read synchronously within this call — safe to
        ///     reuse after return.
        ///
        ///     <para>
        ///       A <see cref="ReadOnlySpan{T}"/> converts implicitly to a <b>present</b> payload, so
        ///       an empty span is sent as a present zero-length value. Pass <c>default</c> /
        ///       <see cref="KafkaNativeSpan.Null"/> for a NULL value (tombstone / no key).
        ///     </para>
        /// </summary>
        /// <param name="topic">The topic to produce to.</param>
        /// <param name="key">The message key. <c>default</c> for no key (tombstone key).</param>
        /// <param name="value">The message value. <c>default</c> for a tombstone.</param>
        /// <param name="opaque">Per-message opaque pointer, surfaced as <see cref="RawDeliveryReport.Opaque"/>.</param>
        /// <exception cref="KafkaException">Thrown when the enqueue fails.</exception>
        void RawProduce(string topic, KafkaNativeSpan key, KafkaNativeSpan value, IntPtr opaque = default);

        /// <summary>
        ///     Produce a message to a specific partition. See
        ///     <see cref="RawProduce(string, KafkaNativeSpan, KafkaNativeSpan, IntPtr)"/> for null-vs-empty semantics.
        /// </summary>
        /// <param name="partition">The target partition. Use <see cref="Partition.Any"/> to let librdkafka pick.</param>
        void RawProduce(string topic, Partition partition, KafkaNativeSpan key, KafkaNativeSpan value, IntPtr opaque = default);

        /// <summary>
        ///     Produce a message with headers. The caller owns the memory backing each header's
        ///     value and must keep it valid until this call returns. Header names are re-encoded to
        ///     UTF-8 using a stack buffer. See
        ///     <see cref="RawProduce(string, KafkaNativeSpan, KafkaNativeSpan, IntPtr)"/> for null-vs-empty semantics.
        /// </summary>
        void RawProduce(string topic, KafkaNativeSpan key, KafkaNativeSpan value, in KafkaHeaders headers, IntPtr opaque = default);

        /// <summary>
        ///     Produce a message with headers to a specific partition.
        /// </summary>
        void RawProduce(string topic, Partition partition, KafkaNativeSpan key, KafkaNativeSpan value, in KafkaHeaders headers, IntPtr opaque = default);

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
