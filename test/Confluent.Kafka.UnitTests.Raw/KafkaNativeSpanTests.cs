using System;
using Xunit;

namespace Confluent.Kafka.UnitTests.Raw;

public class KafkaNativeSpanTests
{
    [Fact]
    public void Default_IsNull()
    {
        KafkaNativeSpan s = default;
        Assert.True(s.IsNull);
        Assert.True(s.IsEmpty);
        Assert.Equal(0, s.Length);
        Assert.True(KafkaNativeSpan.Null.IsNull);
    }

    [Fact]
    public void Present_Empty_IsNotNull()
    {
        var s = KafkaNativeSpan.Present(ReadOnlySpan<byte>.Empty);
        Assert.False(s.IsNull);
        Assert.True(s.IsEmpty);
    }

    [Fact]
    public void Present_NonEmpty_ExposesBytes()
    {
        var s = KafkaNativeSpan.Present(new byte[] { 1, 2, 3 });
        Assert.False(s.IsNull);
        Assert.Equal(3, s.Length);
        Assert.True(s.Span.SequenceEqual(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void ImplicitFromEmptySpan_IsNull()
    {
        KafkaNativeSpan s = ReadOnlySpan<byte>.Empty;
        Assert.True(s.IsNull);
    }

    [Fact]
    public void ImplicitFromNonEmptySpan_IsPresent()
    {
        KafkaNativeSpan s = new ReadOnlySpan<byte>(new byte[] { 7 });
        Assert.False(s.IsNull);
        Assert.Equal(1, s.Length);
    }

    [Fact]
    public void ImplicitFromNullOrEmptyArray_IsNull()
    {
        KafkaNativeSpan fromNull = (byte[])null;
        KafkaNativeSpan fromEmpty = Array.Empty<byte>();
        Assert.True(fromNull.IsNull);
        Assert.True(fromEmpty.IsNull);
    }

    [Fact]
    public void ImplicitFromNonEmptyArray_IsPresent()
    {
        KafkaNativeSpan s = new byte[] { 1, 2 };
        Assert.False(s.IsNull);
        Assert.Equal(2, s.Length);
    }
}
