using System;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace Confluent.Kafka.IntegrationTests.Raw;

/// <summary>
///     The raw produce paths are allocation-free on the producing thread in steady state: the librdkafka handle is
///     read without going through <c>Handle</c>, whose getter allocates a wrapper on every access (32 B per produce).
/// </summary>
[Collection(KafkaCollection.Name)]
public class Producer_RawProduce_AllocationTests
{
    private const int Messages = 5000;

    private readonly KafkaFixture kafka;

    public Producer_RawProduce_AllocationTests(KafkaFixture kafka)
    {
        this.kafka = kafka;
    }

    [Fact]
    public unsafe void RawProduce_AndProduceNoCopy_WithAndWithoutHeaders_AllocateNothing()
    {
        var topic = $"raw-alloc-{Guid.NewGuid():N}";
        var delivered = 0;
        var builder = new RawProducerBuilder(new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
            LingerMs = 5,
        });
        builder.SetDeliveryReportHandler((in RawDeliveryReport _) => Interlocked.Increment(ref delivered));
        var producer = builder.BuildRaw();
        try
        {
            var key = new byte[8];
            var value = (byte*)NativeMemory.Alloc(512);
            var headers = new KafkaHeaders();
            for (var i = 0; i < 4; i++) headers.Add("h" + i, new byte[16]);
            try
            {
                fixed (byte* keyPtr = key)
                {
                    var k = (IntPtr)keyPtr;
                    var v = (IntPtr)value;
                    AssertNoAllocation(producer, () => producer.RawProduce(topic, key, new ReadOnlySpan<byte>(value, 512)), "RawProduce");
                    AssertNoAllocation(producer, () => producer.RawProduce(topic, key, new ReadOnlySpan<byte>(value, 512), in headers), "RawProduce + headers");
                    AssertNoAllocation(producer, () => RawProducerMarshal.ProduceNoCopy(ref producer, topic, k, 8, v, 512), "ProduceNoCopy");
                    AssertNoAllocation(producer, () => RawProducerMarshal.ProduceNoCopy(ref producer, topic, k, 8, v, 512, in headers), "ProduceNoCopy + headers");
                }
            }
            finally
            {
                producer.Flush(TimeSpan.FromSeconds(30)); // the no-copy values must outlive their delivery
                NativeMemory.Free(value);
            }
        }
        finally
        {
            producer.Dispose();
        }

        Assert.Equal(8 * Messages, delivered);
    }

    private static void AssertNoAllocation(IRawProducer producer, Action produce, string what)
    {
        for (var i = 0; i < Messages; i++) produce(); // warm up: topic handle, metadata, JIT
        producer.Flush(TimeSpan.FromSeconds(30));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Messages; i++) produce();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        producer.Flush(TimeSpan.FromSeconds(30));
        Assert.True(allocated == 0, $"{what}: {allocated:N0} bytes for {Messages:N0} produces ({allocated / (double)Messages:F2} B/message)");
    }
}
