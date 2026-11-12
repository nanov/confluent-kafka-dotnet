using System;

namespace Confluent.Kafka
{
    /// <summary>
    ///     A Kafka key or value payload that tells a <b>null</b> payload (a tombstone or absent
    ///     key) apart from a <b>present</b> one, including a present zero-length one. The
    ///     language won't accept <c>ReadOnlySpan&lt;byte&gt;?</c>, because a ref struct cannot be
    ///     a <see cref="Nullable{T}"/> type argument; this type stands in for it.
    ///
    ///     <para>
    ///       librdkafka treats a NULL payload pointer as a tombstone and a non-NULL pointer with
    ///       length 0 as a present empty value. A bare <see cref="ReadOnlySpan{T}"/> maps both to
    ///       an empty span.
    ///     </para>
    ///
    ///     <para>
    ///       <c>default(KafkaNativeSpan)</c> is a null payload. The implicit conversions from
    ///       <see cref="ReadOnlySpan{T}"/> and <see cref="byte"/>[] keep the semantics of the
    ///       <see cref="ReadOnlySpan{T}"/> <c>RawProduce</c> overloads: an empty or null input
    ///       becomes a null payload. A present zero-length payload must be asked for with
    ///       <see cref="Present"/>.
    ///     </para>
    /// </summary>
    public readonly ref struct KafkaNativeSpan
    {
        private readonly ReadOnlySpan<byte> _span;
        // Stored as "present" rather than "null" so that the zero value is the null payload.
        private readonly bool _present;

        private KafkaNativeSpan(ReadOnlySpan<byte> span)
        {
            _span = span;
            _present = true;
        }

        /// <summary>
        ///     The payload bytes. Empty both for a null payload and a present empty one; use
        ///     <see cref="IsNull"/> to tell them apart.
        /// </summary>
        public ReadOnlySpan<byte> Span => _span;

        /// <summary>True for a null payload (a tombstone or absent key).</summary>
        public bool IsNull => !_present;

        /// <summary>True when the payload has no bytes, whether it is null or present.</summary>
        public bool IsEmpty => _span.IsEmpty;

        /// <summary>The payload length in bytes; 0 for a null or present empty payload.</summary>
        public int Length => _span.Length;

        /// <summary>A null payload (a tombstone). Same as <c>default(KafkaNativeSpan)</c>.</summary>
        public static KafkaNativeSpan Null => default;

        /// <summary>A present payload over <paramref name="span"/>. A zero-length span stays present.</summary>
        public static KafkaNativeSpan Present(ReadOnlySpan<byte> span) => new KafkaNativeSpan(span);

        /// <summary>
        ///     An empty span becomes a null payload, the same as the <see cref="ReadOnlySpan{T}"/>
        ///     <c>RawProduce</c> overloads; a non-empty span becomes a present payload. Use
        ///     <see cref="Present"/> for a present zero-length payload.
        /// </summary>
        public static implicit operator KafkaNativeSpan(ReadOnlySpan<byte> span)
            => span.IsEmpty ? default : new KafkaNativeSpan(span);

        /// <summary>
        ///     A null or empty array becomes a null payload; a non-empty array becomes a present
        ///     payload. Use <see cref="Present"/> for a present zero-length payload.
        /// </summary>
        public static implicit operator KafkaNativeSpan(byte[] array)
            => array is null || array.Length == 0 ? default : new KafkaNativeSpan(array);

        // No implicit conversion to ReadOnlySpan<byte>, on purpose. With one, a call such as
        // RawProduce(topic, KafkaNativeSpan.Present(empty), someSpan) would bind to the
        // ReadOnlySpan overload and quietly turn the present-empty key into a null one. Without
        // it, overload resolution prefers the ReadOnlySpan overloads for every argument shape
        // that compiled before this type existed (default, null, arrays, Span, stackalloc,
        // ArraySegment), so existing call sites bind exactly as they did. Read the bytes via Span.
    }
}
