using System;
using System.Text;
using Xunit;

namespace Confluent.Kafka.IntegrationTests.RawConsumer;

[Collection(KafkaCollection.Name)]
public class Consumer_ConsumeRawTests
{
    private readonly KafkaFixture kafka;

    public Consumer_ConsumeRawTests(KafkaFixture kafka)
    {
        this.kafka = kafka;
    }

    [Fact]
    public void Consumer_ConsumeRaw()
    {
        var topic = $"raw-{Guid.NewGuid():N}";
        var key = Encoding.UTF8.GetBytes("hello-key");
        var value = Encoding.UTF8.GetBytes("hello-value");

        using (var producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
        }).Build())
        {
            producer.Produce(topic, new Message<byte[], byte[]> { Key = key, Value = value });
            producer.Flush(TimeSpan.FromSeconds(10));
        }

        using var consumer = new RawConsumerBuilder(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        }).BuildRaw();

        consumer.Subscribe(topic);

        using var msg = consumer.ConsumeRaw(TimeSpan.FromSeconds(30));

        Assert.False(msg.IsEmpty);
        Assert.Equal(ErrorCode.NoError, msg.ErrorCode);
        Assert.True(msg.KeySpan.SequenceEqual(key));
        Assert.True(msg.ValueSpan.SequenceEqual(value));
        Assert.True(msg.Topic.SequenceEqual(Encoding.UTF8.GetBytes(topic)));
        Assert.Equal(0, (int)msg.Partition);
        Assert.Equal(0, (long)msg.Offset);
    }
}
