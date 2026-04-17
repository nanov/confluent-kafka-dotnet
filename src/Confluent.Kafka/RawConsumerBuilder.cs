using System;
using System.Collections.Generic;

namespace Confluent.Kafka
{
    /// <summary>
    ///     A builder for <see cref="RawConsumer"/>. Inherits all configuration and
    ///     handler setters from <see cref="ConsumerBuilder{TKey, TValue}"/> bound to
    ///     <see cref="Ignore"/>, <see cref="Ignore"/>, and overrides
    ///     <see cref="ConsumerBuilder{TKey, TValue}.Build"/> to return a
    ///     <see cref="RawConsumer"/>.
    /// </summary>
    public class RawConsumerBuilder : ConsumerBuilder<Ignore, Ignore>
    {
        internal RawStatisticsHandler RawStatisticsHandler { get; private set; }

        /// <summary>
        ///     Initialize a new <see cref="RawConsumerBuilder"/> with the given config.
        /// </summary>
        public RawConsumerBuilder(IEnumerable<KeyValuePair<string, string>> config) : base(config)
        {
        }

        /// <summary>
        ///     Set the handler to call on librdkafka statistics events, receiving the
        ///     JSON payload as a <see cref="ReadOnlySpan{T}"/> of UTF-8 bytes to avoid
        ///     string allocation on every stats tick.
        /// </summary>
        /// <remarks>
        ///     Enable statistics via the <c>statistics.interval.ms</c> config (disabled
        ///     by default). Mutually exclusive with the inherited
        ///     <see cref="ConsumerBuilder{TKey, TValue}.SetStatisticsHandler"/>.
        /// </remarks>
        public RawConsumerBuilder SetStatisticsHandler(RawStatisticsHandler statisticsHandler)
        {
            if (this.RawStatisticsHandler != null || this.StatisticsHandler != null)
            {
                throw new InvalidOperationException("Statistics handler may not be specified more than once.");
            }
            this.RawStatisticsHandler = statisticsHandler;
            // Set a no-op string handler so Consumer.cs registers the native stats
            // callback. Our RawConsumer overrides StatisticsCallback and never invokes
            // this dummy.
            base.SetStatisticsHandler((_, _) => { });
            return this;
        }

        /// <summary>
        ///     Build a new <see cref="IConsumer{TKey, TValue}"/>. The returned
        ///     instance is also an <see cref="IRawConsumer"/>; prefer
        ///     <see cref="BuildRaw"/> for the typed reference.
        /// </summary>
        public override IConsumer<Ignore, Ignore> Build()
        {
            return new RawConsumer(this);
        }

        /// <summary>
        ///     Build a new <see cref="IRawConsumer"/>.
        /// </summary>
        public IRawConsumer BuildRaw()
        {
            return (IRawConsumer)Build();
        }
    }
}
