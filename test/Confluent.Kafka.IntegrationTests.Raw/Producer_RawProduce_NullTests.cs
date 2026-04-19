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
