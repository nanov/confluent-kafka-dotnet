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
        Assert.True(msg.Key.SequenceEqual(key));
        Assert.True(msg.Value.SequenceEqual(value));
        Assert.True(msg.Topic.SequenceEqual(Encoding.UTF8.GetBytes(topic)));
        Assert.Equal(0, (int)msg.Partition);
        Assert.Equal(0, (long)msg.Offset);
    }

    [Fact]
    public void Consumer_ConsumeRaw_Headers()
    {
        var topic = $"raw-hdr-{Guid.NewGuid():N}";
        var key = Encoding.UTF8.GetBytes("k");
        var value = Encoding.UTF8.GetBytes("v");

        var headers = new Headers
        {
            { "h1", Encoding.UTF8.GetBytes("one") },
            { "h2", Encoding.UTF8.GetBytes("two") },
            { "empty", null },
        };

        using (var producer = new ProducerBuilder<byte[], byte[]>(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
        }).Build())
        {
            producer.Produce(topic, new Message<byte[], byte[]> { Key = key, Value = value, Headers = headers });
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
        Assert.False(msg.Headers.IsEmpty);

        var collected = new System.Collections.Generic.List<(string Name, byte[] Value)>();
        foreach (var (hName, hValue) in msg.Headers)
        {
            collected.Add((Encoding.UTF8.GetString(hName), hValue.IsEmpty ? null : hValue.ToArray()));
        }

        Assert.Equal(3, collected.Count);
        Assert.Equal("h1", collected[0].Name);
        Assert.True(collected[0].Value.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes("one")));
        Assert.Equal("h2", collected[1].Name);
        Assert.True(collected[1].Value.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes("two")));
        Assert.Equal("empty", collected[2].Name);
        Assert.Null(collected[2].Value);
    }
}
