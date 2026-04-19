using System;
using System.Collections.Generic;

namespace Confluent.Kafka
{
    /// <summary>
    ///     A builder for <see cref="RawProducer"/>. Inherits all configuration and
    ///     handler setters from <see cref="ProducerBuilder{TKey, TValue}"/> bound to
    ///     <see cref="Ignore"/>, <see cref="Ignore"/>.
    /// </summary>
    public class RawProducerBuilder : ProducerBuilder<Ignore, Ignore>
    {
        internal RawDeliveryReportHandler RawDeliveryReportHandler { get; private set; }

        /// <summary>
        ///     Initialize a new <see cref="RawProducerBuilder"/> with the given config.
        /// </summary>
        public RawProducerBuilder(IEnumerable<KeyValuePair<string, string>> config) : base(config)
        {
            // The base producer demands serializers for TKey/TValue at construction,
            // even though RawProduce bypasses serialization entirely. Wire up a no-op
            // so construction succeeds.
            base.SetKeySerializer(IgnoreSerializer.Instance);
            base.SetValueSerializer(IgnoreSerializer.Instance);
        }

        private sealed class IgnoreSerializer : ISerializer<Ignore>
        {
            public static readonly IgnoreSerializer Instance = new IgnoreSerializer();
            public byte[] Serialize(Ignore data, SerializationContext context) => null;
        }

        /// <summary>
        ///     Set the handler invoked on every produced message after acknowledgement
        ///     or failure. The signature is currently empty; future revisions may expose
        ///     an allocation-free delivery report payload.
        /// </summary>
        public RawProducerBuilder SetDeliveryReportHandler(RawDeliveryReportHandler handler)
        {
            if (this.RawDeliveryReportHandler != null)
            {
                throw new InvalidOperationException("Delivery report handler may not be specified more than once.");
            }
            this.RawDeliveryReportHandler = handler;
            return this;
        }

        /// <summary>
        ///     Build a new <see cref="IProducer{TKey, TValue}"/>. The returned instance
        ///     is also an <see cref="IRawProducer"/>; prefer <see cref="BuildRaw"/> for
        ///     the typed reference.
        /// </summary>
        public override IProducer<Ignore, Ignore> Build()
        {
            return new RawProducer(this);
        }

        /// <summary>
        ///     Build a new <see cref="IRawProducer"/>.
        /// </summary>
        public IRawProducer BuildRaw()
        {
            return (IRawProducer)Build();
        }
    }
}
