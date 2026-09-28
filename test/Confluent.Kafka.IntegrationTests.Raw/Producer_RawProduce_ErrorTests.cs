using System;
using System.Text;
using System.Threading;
using Xunit;

namespace Confluent.Kafka.IntegrationTests.Raw;

[Collection(KafkaCollection.Name)]
public class Producer_RawProduce_ErrorTests
{
    private readonly KafkaFixture kafka;

    public Producer_RawProduce_ErrorTests(KafkaFixture kafka)
    {
        this.kafka = kafka;
    }

    [Fact]
    public void Produce_UnknownPartition_ReportsError()
    {
        var topic = $"raw-prod-err-{Guid.NewGuid():N}";

        int count = 0;
        ErrorCode observedErr = default;
        Partition observedPartition = default;
        Offset observedOffset = default;
        PersistenceStatus observedStatus = default;

        var producer = new RawProducerBuilder(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
        })
        .SetDeliveryReportHandler((in RawDeliveryReport dr) =>
        {
            Interlocked.Increment(ref count);
            observedErr = dr.ErrorCode;
            observedPartition = dr.Partition;
            observedOffset = dr.Offset;
            observedStatus = dr.Status;
        })
        .BuildRaw();

        try
        {
            producer.RawProduce(topic, partition: new Partition(42), default, Encoding.UTF8.GetBytes("v"));
            producer.Flush(TimeSpan.FromSeconds(10));
        }
        finally
        {
            producer.Dispose();
        }

        Assert.Equal(1, count);
        Assert.Equal(ErrorCode.Local_UnknownPartition, observedErr);
        Assert.Equal((Partition)42, observedPartition);
        Assert.Equal(Offset.Unset, observedOffset);
        Assert.Equal(PersistenceStatus.NotPersisted, observedStatus);
    }

    /// <summary>A no-copy produce the client refuses (larger than message.max.bytes) throws a KafkaException
    /// carrying the error, with and without headers.</summary>
    [Fact]
    public unsafe void ProduceNoCopy_Oversized_ThrowsMsgSizeTooLarge()
    {
        var topic = $"raw-prod-err-{Guid.NewGuid():N}";
        var producer = new RawProducerBuilder(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            MessageMaxBytes = 1000,
        }).BuildRaw();
        var value = (byte*)System.Runtime.InteropServices.NativeMemory.Alloc(5000);
        try
        {
            var headers = new KafkaHeaders();
            headers.Add("h", new byte[] { 1 });

            var plain = Assert.Throws<KafkaException>(() =>
                RawProducerMarshal.ProduceNoCopy(ref producer, topic, IntPtr.Zero, 0, (IntPtr)value, 5000));
            var withHeaders = Assert.Throws<KafkaException>(() =>
                RawProducerMarshal.ProduceNoCopy(ref producer, topic, IntPtr.Zero, 0, (IntPtr)value, 5000, in headers));

            Assert.Equal(ErrorCode.MsgSizeTooLarge, plain.Error.Code);
            Assert.Equal(ErrorCode.MsgSizeTooLarge, withHeaders.Error.Code);
        }
        finally
        {
            System.Runtime.InteropServices.NativeMemory.Free(value);
            producer.Dispose();
        }
    }
}
