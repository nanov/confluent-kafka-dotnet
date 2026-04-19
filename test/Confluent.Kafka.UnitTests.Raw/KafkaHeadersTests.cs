using System;
using Xunit;

namespace Confluent.Kafka.UnitTests.Raw;

public class KafkaHeadersTests
{
    [Fact]
    public void Default_Count_IsZero()
    {
        KafkaHeaders h = default;
        Assert.Equal(0, h.Count);
    }

    [Fact]
    public void Add_Single_IncrementsCount()
    {
        var h = new KafkaHeaders();
        h.Add("k", new byte[] { 1, 2, 3 });
        Assert.Equal(1, h.Count);
    }

    [Fact]
    public void Indexer_ReturnsAddedValue()
    {
        var h = new KafkaHeaders();
        h.Add("hello", new byte[] { 1, 2, 3 });

        var entry = h[0];
        Assert.Equal("hello", entry.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, entry.Value.ToArray());
    }

    [Fact]
    public void Add_ManyHeaders_AllAccessibleViaIndexer()
    {
        var h = new KafkaHeaders();
        const int n = 20;

        for (int i = 0; i < n; i++)
        {
            h.Add($"k{i}", new byte[] { (byte)i });
        }

        Assert.Equal(n, h.Count);
        for (int i = 0; i < n; i++)
        {
            var e = h[i];
            Assert.Equal($"k{i}", e.Name);
            Assert.Equal(new byte[] { (byte)i }, e.Value.ToArray());
        }
    }

    [Fact]
    public void Add_ManyHeaders_TriggersResize_StillAccessible()
    {
        var h = new KafkaHeaders();
        const int n = 100;

        for (int i = 0; i < n; i++)
        {
            h.Add($"k{i}", BitConverter.GetBytes(i));
        }

        Assert.Equal(n, h.Count);
        Assert.Equal("k0", h[0].Name);
        Assert.Equal($"k{n - 1}", h[n - 1].Name);
    }

    [Fact]
    public void Add_BulkSpan_AddsAllInOrder()
    {
        var h = new KafkaHeaders();
        ReadOnlySpan<KafkaHeader> batch = new[]
        {
            new KafkaHeader { Name = "a", Value = new byte[] { 1 } },
            new KafkaHeader { Name = "b", Value = new byte[] { 2 } },
            new KafkaHeader { Name = "c", Value = new byte[] { 3 } },
        };

        h.Add(batch);

        Assert.Equal(3, h.Count);
        Assert.Equal("a", h[0].Name);
        Assert.Equal("b", h[1].Name);
        Assert.Equal("c", h[2].Name);
    }

    [Fact]
    public void Add_BulkSpan_OnTopOfExistingEntries_Appends()
    {
        var h = new KafkaHeaders();
        h.Add("first", new byte[] { 0 });

        ReadOnlySpan<KafkaHeader> batch = new[]
        {
            new KafkaHeader { Name = "a", Value = new byte[] { 1 } },
            new KafkaHeader { Name = "b", Value = new byte[] { 2 } },
        };
        h.Add(batch);

        Assert.Equal(3, h.Count);
        Assert.Equal("first", h[0].Name);
        Assert.Equal("a", h[1].Name);
        Assert.Equal("b", h[2].Name);
    }

    [Fact]
    public void Foreach_IteratesInOrder()
    {
        var h = new KafkaHeaders();
        h.Add("a", new byte[] { 1 });
        h.Add("b", new byte[] { 2 });
        h.Add("c", new byte[] { 3 });

        int i = 0;
        foreach (var entry in h)
        {
            Assert.Equal("abc"[i].ToString(), entry.Name);
            Assert.Equal(new byte[] { (byte)(i + 1) }, entry.Value.ToArray());
            i++;
        }
        Assert.Equal(3, i);
    }

    [Fact]
    public void Foreach_AcrossInlineOverflowBoundary()
    {
        var h = new KafkaHeaders();
        for (int i = 0; i < 20; i++)
        {
            h.Add($"k{i}", new byte[] { (byte)i });
        }

        int seen = 0;
        foreach (var entry in h)
        {
            Assert.Equal($"k{seen}", entry.Name);
            seen++;
        }
        Assert.Equal(20, seen);
    }

    [Fact]
    public void Add_EmptySpan_DoesNotChangeCount()
    {
        var h = new KafkaHeaders();
        h.Add("one", new byte[] { 1 });

        h.Add(ReadOnlySpan<KafkaHeader>.Empty);

        Assert.Equal(1, h.Count);
    }
}
