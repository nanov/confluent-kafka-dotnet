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

[Collection(KafkaCollection.Name)]
public class Producer_RawProduce_HeaderValueTests
{
    private readonly KafkaFixture kafka;

    public Producer_RawProduce_HeaderValueTests(KafkaFixture kafka)
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

    private static KafkaHeaders NullAndPresentEmpty()
    {
        var headers = new KafkaHeaders();
        headers.Add("null", default);
        headers.Add("present-empty", Array.Empty<byte>(), valueIsNull: false);
        return headers;
    }

    private void AssertHeaderValues(string topic, params bool[] valueNull)
    {
        using var consumer = BuildConsumer(topic);
        using var msg = consumer.ConsumeRaw(TimeSpan.FromSeconds(30));

        Assert.False(msg.IsEmpty);
        Assert.Equal(ErrorCode.NoError, msg.ErrorCode);

        int index = 0;
        foreach (var header in msg.Headers)
        {
            Assert.Equal(valueNull[index++], header.KafkaValue.IsNull);
            Assert.True(header.KafkaValue.IsEmpty);
            Assert.True(header.Value.IsEmpty);
        }
        Assert.Equal(valueNull.Length, index);
    }

    [Fact]
    public void PresentEmpty_And_Null_HeaderValues_RoundTrip()
    {
        var topic = $"raw-prod-hdr-present-{Guid.NewGuid():N}";
        var headers = NullAndPresentEmpty();
        var producer = BuildProducer();
        try
        {
            producer.RawProduce(topic, default, new byte[] { 1 }, in headers);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertHeaderValues(topic, true, false);
    }

    [Fact]
    public void PresentEmpty_HeaderValue_TwoHeaderSets_WithNativeSpanValue()
    {
        var topic = $"raw-prod-hdr-kns-{Guid.NewGuid():N}";
        var shared = NullAndPresentEmpty();
        var perMessage = new KafkaHeaders();
        perMessage.Add("present-empty", Array.Empty<byte>(), valueIsNull: false);
        var producer = BuildProducer();
        try
        {
            producer.RawProduce(topic, KafkaNativeSpan.Null, KafkaNativeSpan.Present(default), in shared, in perMessage);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertHeaderValues(topic, true, false, false);
    }

    [Fact]
    public void NoCopy_PresentEmpty_HeaderValue()
    {
        var topic = $"raw-prod-hdr-nocopy-{Guid.NewGuid():N}";
        var headers = NullAndPresentEmpty();
        var producer = BuildProducer();
        try
        {
            RawProducerMarshal.ProduceNoCopy(ref producer, topic, IntPtr.Zero, 0, IntPtr.Zero, 0, in headers);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertHeaderValues(topic, true, false);
    }

    [Fact]
    public void EmptyHeaderValue_KeepsLegacyNullSemantics()
    {
        var topic = $"raw-prod-hdr-legacy-{Guid.NewGuid():N}";
        var headers = new KafkaHeaders();
        headers.Add("empty", Array.Empty<byte>());
        var producer = BuildProducer();
        try
        {
            producer.RawProduce(topic, default, new byte[] { 1 }, in headers);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        AssertHeaderValues(topic, true);
    }

    [Fact]
    public void PresentEmpty_HeaderValue_ReadsAsEmptyArray_InTypedConsumer()
    {
        var topic = $"raw-prod-hdr-typed-{Guid.NewGuid():N}";
        var headers = NullAndPresentEmpty();
        var producer = BuildProducer();
        try
        {
            producer.RawProduce(topic, default, new byte[] { 1 }, in headers);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        using var consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).Build();
        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));

        Assert.NotNull(result);
        Assert.Equal(2, result.Message.Headers.Count);
        Assert.Null(result.Message.Headers[0].GetValueBytes());
        Assert.Equal(Array.Empty<byte>(), result.Message.Headers[1].GetValueBytes());
    }
}
