using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Confluent.Kafka.UnitTests.Raw;

/// <summary>No broker needed: every message times out and its delivery report carries the produced headers.</summary>
public class RawProducerHeaderValueTests
{
    private const string Topic = "raw-hdr-value";
    private const string Expected = "null=null,empty=null,null-flag=null,present-empty=0,present=1";

    private static void AddNullValues(ref KafkaHeaders h)
    {
        h.Add("null", default);
        h.Add("empty", Array.Empty<byte>());
        h.Add("null-flag", new byte[] { 1 }, valueIsNull: true);
    }

    private static void AddPresentValues(ref KafkaHeaders h)
    {
        h.Add("present-empty", Array.Empty<byte>(), valueIsNull: false);
        h.Add("present", new byte[] { 7 }, valueIsNull: false);
    }

    private static IRawProducer BuildProducer(Dictionary<IntPtr, string> reports)
        => new RawProducerBuilder(new ProducerConfig
        {
            MessageTimeoutMs = 1,
        })
        .SetDeliveryReportHandler((in RawDeliveryReport dr) => reports[dr.Opaque] = Describe(dr.Headers))
        .BuildRaw();

    private static string Describe(RawHeaders headers)
    {
        var sb = new StringBuilder();
        foreach (var header in headers)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(Encoding.UTF8.GetString(header.Name)).Append('=');
            var value = header.KafkaValue;
            if (value.IsNull) sb.Append("null");
            else sb.Append(value.Length);
        }
        return sb.ToString();
    }

    [Fact]
    public void PresentEmptyValue_IsNotNull_OnEveryProducePath()
    {
        var all = new KafkaHeaders();
        AddNullValues(ref all);
        AddPresentValues(ref all);
        var first = new KafkaHeaders();
        AddNullValues(ref first);
        var second = new KafkaHeaders();
        AddPresentValues(ref second);
        byte[] key = { 1 };
        byte[] value = { 2 };

        var reports = new Dictionary<IntPtr, string>();
        var producer = BuildProducer(reports);
        try
        {
            producer.RawProduce(Topic, key, value, in all, (IntPtr)1);
            producer.RawProduce(Topic, key, value, in first, in second, (IntPtr)2);
            producer.RawProduce(Topic, KafkaNativeSpan.Present(key), KafkaNativeSpan.Present(value), in all, (IntPtr)3);
            producer.RawProduce(Topic, KafkaNativeSpan.Present(key), KafkaNativeSpan.Present(value), in first, in second, (IntPtr)4);
            RawProducerMarshal.ProduceNoCopy(ref producer, Topic, IntPtr.Zero, 0, IntPtr.Zero, 0, in all, (IntPtr)5);
            RawProducerMarshal.ProduceNoCopy(ref producer, Topic, IntPtr.Zero, 0, IntPtr.Zero, 0, in first, in second, (IntPtr)6);
            RawProducerMarshal.ProduceNoCopy(ref producer, Topic, Partition.Any,
                IntPtr.Zero, 0, keyIsNull: true,
                IntPtr.Zero, 0, valueIsNull: false,
                in all, (IntPtr)7);
            RawProducerMarshal.ProduceNoCopy(ref producer, Topic, Partition.Any,
                IntPtr.Zero, 0, keyIsNull: true,
                IntPtr.Zero, 0, valueIsNull: false,
                in first, in second, (IntPtr)8);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        Assert.Equal(8, reports.Count);
        for (var i = 1; i <= 8; i++)
        {
            Assert.Equal(Expected, reports[(IntPtr)i]);
        }
    }

    [Fact]
    public void PresentEmptyValue_IsKeptByCopies()
    {
        var source = new KafkaHeaders();
        source.Add("a", Array.Empty<byte>(), valueIsNull: false);

        var headers = new KafkaHeaders();
        headers.Add(new[] { source[0] });
        headers.AddOrUpdate("b", source[0].Value);
        headers["c"] = source[0].Value;

        var reports = new Dictionary<IntPtr, string>();
        var producer = BuildProducer(reports);
        try
        {
            producer.RawProduce(Topic, default, default, in headers, (IntPtr)1);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            producer.Dispose();
        }

        Assert.Equal("a=0,b=0,c=0", reports[(IntPtr)1]);
    }
}
