using LiveTracker.Application.Ingestion;
using LiveTracker.Application.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace LiveTracker.Tests;

public class PendingBatchBufferTests
{
    private static PendingBatchBuffer Create(int maxRows) =>
        new(Options.Create(new IngestionOptions { MaxBufferedRows = maxRows }));

    [Fact]
    public void Add_BelowLimit_KeepsEverything()
    {
        var buffer = Create(10);
        var dropped = buffer.Add(TestData.Batch(3));

        Assert.Equal(0, dropped);
        Assert.Equal(3, buffer.PendingRows);
    }

    [Fact]
    public void Add_OverLimit_DropsOldestBatchFirst()
    {
        var buffer = Create(5);
        var first = TestData.Batch(3);
        var second = TestData.Batch(3);

        buffer.Add(first);
        var dropped = buffer.Add(second);

        Assert.Equal(3, dropped);
        Assert.Equal(3, buffer.PendingRows);
        Assert.Same(second, buffer.PeekOldest());
    }

    [Fact]
    public void Add_SingleBatchLargerThanLimit_IsNotDropped()
    {
        // Tek başına sığmayan tur bile saklanır; atılacak eski tur yoksa veri silinmez.
        var buffer = Create(5);
        var dropped = buffer.Add(TestData.Batch(10));

        Assert.Equal(0, dropped);
        Assert.Equal(10, buffer.PendingRows);
    }

    [Fact]
    public void Add_EmptyBatch_IsIgnored()
    {
        var buffer = Create(5);
        buffer.Add(TestData.Batch(0));

        Assert.Equal(0, buffer.PendingRows);
        Assert.Null(buffer.PeekOldest());
    }

    [Fact]
    public void RemoveOldest_ReducesPendingRows()
    {
        var buffer = Create(100);
        buffer.Add(TestData.Batch(4));
        buffer.Add(TestData.Batch(2));

        buffer.RemoveOldest();

        Assert.Equal(2, buffer.PendingRows);
    }
}