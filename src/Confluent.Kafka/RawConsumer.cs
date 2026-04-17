using System;
using System.Threading;
using Confluent.Kafka.Impl;

namespace Confluent.Kafka
{
    /// <summary>
    ///     A Kafka consumer that adds an allocation-free consume path on top of
    ///     <see cref="Consumer{TKey, TValue}"/>. Inherits all subscription, commit,
    ///     and offset-management behavior from the base consumer (bound to
    ///     <see cref="Ignore"/>, <see cref="Ignore"/>).
    /// </summary>
    internal class RawConsumer : Consumer<Ignore, Ignore>, IRawConsumer
    {
        private readonly RawStatisticsHandler rawStatisticsHandler;

        internal RawConsumer(RawConsumerBuilder builder) : base(builder)
        {
            this.rawStatisticsHandler = builder.RawStatisticsHandler;
        }

        protected override unsafe int StatisticsCallback(IntPtr rk, IntPtr json, UIntPtr json_len, IntPtr opaque)
        {
            if (kafkaHandle.IsClosed) { return 0; }
            try
            {
                rawStatisticsHandler?.Invoke(new ReadOnlySpan<byte>(json.ToPointer(), (int)json_len));
            }
            catch (Exception e)
            {
                handlerException = e;
            }
            return 0;
        }

        /// <inheritdoc/>
        public RawMessage ConsumeRaw(int millisecondsTimeout)
        {
            var msgPtr = kafkaHandle.ConsumerPoll((IntPtr)millisecondsTimeout);

            if (handlerException != null)
            {
                var ex = handlerException;
                handlerException = null;
                if (msgPtr != IntPtr.Zero)
                {
                    Librdkafka.message_destroy(msgPtr);
                }
                throw ex;
            }

            return new RawMessage(msgPtr);
        }

        /// <inheritdoc/>
        public RawMessage ConsumeRaw(TimeSpan timeout)
            => ConsumeRaw(timeout.TotalMillisecondsAsInt());

        /// <inheritdoc/>
        public RawMessage ConsumeRaw(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var msg = ConsumeRaw(cancellationDelayMaxMs);
                if (msg.IsEmpty)
                {
                    continue;
                }
                return msg;
            }
        }
    }
}
