using System;
using System.Threading;
using Xunit;

namespace Confluent.Kafka.IntegrationTests.Raw;

[Collection(KafkaCollection.Name)]
public class Producer_RawProduce_NullTests
{
    private readonly KafkaFixture kafka;

    public Producer_RawProduce_NullTests(KafkaFixture kafka)
    {
        this.kafka = kafka;
    }

    private IRawConsumer BuildConsumer(string topic)
    {
        var consumer = new RawConsumerBuilder(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).BuildRaw();
        consumer.Subscribe(topic);
        return consumer;
    }

    [Fact]
    public void Produce_NullKey_NullValue_Delivered()
    {
        var topic = $"raw-prod-null-{Guid.NewGuid():N}";

        int drCount = 0;
        ErrorCode observedErr = default;
        PersistenceStatus observedStatus = default;

        var producer = new RawProducerBuilder(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
        })
        .SetDeliveryReportHandler((in RawDeliveryReport dr) =>
        {
            Interlocked.Increment(ref drCount);
            observedErr = dr.ErrorCode;
            observedStatus = dr.Status;
        })
        .BuildRaw();

        try
        {
            producer.RawProduce(topic, default, default);
            producer.Flush(TimeSpan.FromSeconds(10));
        }
        finally
        {
            producer.Dispose();
        }

        Assert.Equal(1, drCount);
        Assert.Equal(ErrorCode.NoError, observedErr);
        Assert.Equal(PersistenceStatus.Persisted, observedStatus);

        using var consumer = BuildConsumer(topic);
        using var msg = consumer.ConsumeRaw(TimeSpan.FromSeconds(30));

        Assert.False(msg.IsEmpty);
        Assert.Equal(ErrorCode.NoError, msg.ErrorCode);
        Assert.True(msg.Key.IsEmpty);
        Assert.True(msg.Value.IsEmpty);
    }
}

[Collection(KafkaCollection.Name)]
public class Producer_RawProduce_NativeSpanTests
{
    private readonly KafkaFixture kafka;

    public Producer_RawProduce_NativeSpanTests(KafkaFixture kafka)
    {
        this.kafka = kafka;
    }

    private IRawProducer BuildProducer()
        => new RawProducerBuilder(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
        }).BuildRaw();

    private IRawConsumer BuildConsumer(string topic)
    {
        var consumer = new RawConsumerBuilder(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).BuildRaw();
        consumer.Subscribe(topic);
        return consumer;
    }

    private void AssertRoundTrip(string topic, bool keyNull, bool valueNull)
    {
        using var consumer = BuildConsumer(topic);
        using var msg = consumer.ConsumeRaw(TimeSpan.FromSeconds(30));

        Assert.False(msg.IsEmpty);
        Assert.Equal(ErrorCode.NoError, msg.ErrorCode);
        Assert.Equal(keyNull, msg.KafkaKey.IsNull);
        Assert.Equal(valueNull, msg.KafkaValue.IsNull);
        Assert.True(msg.KafkaKey.IsEmpty);
        Assert.True(msg.KafkaValue.IsEmpty);
        Assert.True(msg.Key.IsEmpty);
        Assert.True(msg.Value.IsEmpty);
    }

    [Fact]
    public void PresentEmpty_KeyAndValue_RoundTripAsPresent()
    {
        var topic = $"raw-prod-kns-present-{Guid.NewGuid():N}";
        var producer = BuildProducer();
        try
        {
            producer.RawProduce(topic, KafkaNativeSpan.Present(default), KafkaNativeSpan.Present(default));
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertRoundTrip(topic, keyNull: false, valueNull: false);
    }

    [Fact]
    public void NullKey_PresentEmptyValue_WithHeaders()
    {
        var topic = $"raw-prod-kns-hdr-{Guid.NewGuid():N}";
        var headers = new KafkaHeaders();
        headers.Add("h", new byte[] { 1 });
        var producer = BuildProducer();
        try
        {
            producer.RawProduce(topic, KafkaNativeSpan.Null, KafkaNativeSpan.Present(default), in headers);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertRoundTrip(topic, keyNull: true, valueNull: false);
    }

    [Fact]
    public void EmptySpans_KeepLegacyNullSemantics()
    {
        var topic = $"raw-prod-kns-legacy-{Guid.NewGuid():N}";
        var producer = BuildProducer();
        try
        {
            // Binds the ReadOnlySpan overload, as before KafkaNativeSpan existed.
            producer.RawProduce(topic, ReadOnlySpan<byte>.Empty, Array.Empty<byte>());
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertRoundTrip(topic, keyNull: true, valueNull: true);
    }

    [Fact]
    public void NoCopy_NullAware_PresentEmptyValue()
    {
        var topic = $"raw-prod-kns-nocopy-{Guid.NewGuid():N}";
        var producer = BuildProducer();
        try
        {
            RawProducerMarshal.ProduceNoCopy(ref producer, topic, Partition.Any,
                IntPtr.Zero, 0, keyIsNull: true,
                IntPtr.Zero, 0, valueIsNull: false);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertRoundTrip(topic, keyNull: true, valueNull: false);
    }
}
