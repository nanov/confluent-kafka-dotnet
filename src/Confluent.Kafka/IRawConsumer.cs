using System;
using System.Threading;

namespace Confluent.Kafka
{
    /// <summary>
    ///     Callback invoked on every librdkafka statistics event. The
    ///     <paramref name="statsJsonUtf8"/> span points directly into librdkafka's
    ///     buffer and is only valid for the duration of the call — do not store
    ///     the span beyond the callback scope.
    /// </summary>
    public delegate void RawStatisticsHandler(ReadOnlySpan<byte> statsJsonUtf8);

    /// <summary>
    ///     A Kafka consumer that exposes an allocation-free consume path via
    ///     <see cref="RawMessage"/>. Also inherits the standard <see cref="IConsumer{TKey, TValue}"/>
    ///     surface (bound to <see cref="Ignore"/>, <see cref="Ignore"/>) for
    ///     Subscribe/Assign/Commit/etc.
    /// </summary>
    public interface IRawConsumer : IConsumer<Ignore, Ignore>
    {
        /// <summary>
        ///     Poll for a single message. Returns a stack-only <see cref="RawMessage"/>
        ///     that exposes key and value as <see cref="ReadOnlySpan{T}"/> slices
        ///     directly into librdkafka's buffer. Always use with <c>using</c> to
        ///     release the native message.
        /// </summary>
        /// <param name="millisecondsTimeout">
        ///     Maximum time to block waiting for a message, in milliseconds.
        /// </param>
        /// <returns>
        ///     A <see cref="RawMessage"/>. If the timeout elapsed with no message,
        ///     <see cref="RawMessage.IsEmpty"/> is true.
        /// </returns>
        RawMessage ConsumeRaw(int millisecondsTimeout);

        /// <inheritdoc cref="ConsumeRaw(int)"/>
        RawMessage ConsumeRaw(TimeSpan timeout);

        /// <summary>
        ///     Poll for a single message, blocking until one arrives or the
        ///     <paramref name="cancellationToken"/> fires. The returned
        ///     <see cref="RawMessage"/> is never empty on success.
        /// </summary>
        /// <exception cref="OperationCanceledException">
        ///     Thrown when <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        RawMessage ConsumeRaw(CancellationToken cancellationToken = default);
    }
}
