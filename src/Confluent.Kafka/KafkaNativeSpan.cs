using System;

namespace Confluent.Kafka
{
    /// <summary>
    ///     A Kafka key or value payload that distinguishes a <b>null</b> payload (a tombstone /
    ///     absent key) from a <b>present</b> one that may itself be zero-length. It is the
    ///     ref-struct stand-in for the <c>ReadOnlySpan&lt;byte&gt;?</c> the language won't let you
    ///     write — <see cref="ReadOnlySpan{T}"/> is a ref struct and cannot be a
    ///     <see cref="Nullable{T}"/> type argument.
    ///
    ///     <para>
    ///       The distinction matters because Kafka (and librdkafka's <c>produceva</c>) treat a NULL
    ///       payload pointer and a non-NULL zero-length pointer differently: the former is a
    ///       tombstone, the latter a present empty value. A bare <see cref="ReadOnlySpan{T}"/>
    ///       collapses both onto an empty span.
    ///     </para>
    ///
    ///     <para>
    ///       <c>default(KafkaNativeSpan)</c> is a <b>null</b> payload. The implicit conversions from
    ///       <see cref="ReadOnlySpan{T}"/> / <see cref="byte"/>[] preserve the legacy produce
    ///       semantics — an <b>empty</b> (or null) input maps to a null payload — so existing span
    ///       call sites are unchanged. To opt into a present zero-length payload (distinct from a
    ///       tombstone), construct it explicitly with <see cref="Present"/>.
    ///     </para>
    /// </summary>
    public readonly ref struct KafkaNativeSpan
    {
        private readonly ReadOnlySpan<byte> _span;
        // Stored as "present" (not "null") so the struct's zero value is the null/tombstone case.
        private readonly bool _present;

        private KafkaNativeSpan(ReadOnlySpan<byte> span)
        {
            _span = span;
            _present = true;
        }

        /// <summary>
        ///     The payload bytes. Empty when the payload is null or present-but-empty — use
        ///     <see cref="IsNull"/> to tell those apart.
        /// </summary>
        public ReadOnlySpan<byte> Span => _span;

        /// <summary>True when this is a null payload (a tombstone / absent key).</summary>
        public bool IsNull => !_present;

        /// <summary>True when the payload carries no bytes (whether null or present-but-empty).</summary>
        public bool IsEmpty => _span.IsEmpty;

        /// <summary>The payload length in bytes (0 when null or present-but-empty).</summary>
        public int Length => _span.Length;

        /// <summary>A null payload — a Kafka tombstone. Same as <c>default(KafkaNativeSpan)</c>.</summary>
        public static KafkaNativeSpan Null => default;

        /// <summary>A present payload over <paramref name="span"/> (zero-length spans stay present, not null).</summary>
        public static KafkaNativeSpan Present(ReadOnlySpan<byte> span) => new KafkaNativeSpan(span);

        /// <summary>
        ///     Legacy-preserving conversion: an <b>empty</b> (or null) span maps to a NULL payload
        ///     (tombstone), exactly as the old <see cref="ReadOnlySpan{T}"/> <c>RawProduce</c>
        ///     overload did; a non-empty span is a present payload. Use <see cref="Present"/> to
        ///     force a present zero-length payload instead.
        /// </summary>
        public static implicit operator KafkaNativeSpan(ReadOnlySpan<byte> span)
            => span.IsEmpty ? default : new KafkaNativeSpan(span);

        /// <summary>
        ///     Legacy-preserving conversion for <see cref="byte"/>[] callers (the compiler won't
        ///     chain <c>byte[] → ReadOnlySpan&lt;byte&gt; → KafkaNativeSpan</c> on its own): a null
        ///     or empty array maps to a NULL payload (tombstone), a non-empty array to a present
        ///     payload. Use <see cref="Present"/> to force a present zero-length payload.
        /// </summary>
        public static implicit operator KafkaNativeSpan(byte[] array)
            => array is null || array.Length == 0 ? default : new KafkaNativeSpan(array);

        /// <summary>Projects to the underlying bytes for readers that don't care about null-vs-empty.</summary>
        public static implicit operator ReadOnlySpan<byte>(KafkaNativeSpan payload) => payload._span;
    }
}
