using Aonik.Finance.Services;
using FluentAssertions;

namespace Aonik.Application.Tests;

/// <summary>
/// Pure unit tests for the Finance list paging bounds (issue H10) — the clamp that
/// guarantees a single list call can never request an unbounded result set.
/// </summary>
public class FinancePagingTests
{
    [Theory]
    [InlineData(1, 50, 1, 50)]        // valid values pass through
    [InlineData(3, 200, 3, 200)]      // default page size passes through
    [InlineData(0, 50, 1, 50)]        // page number floored at 1
    [InlineData(-5, 50, 1, 50)]       // negative page number floored at 1
    [InlineData(1, 0, 1, 200)]        // unset page size -> default
    [InlineData(1, -10, 1, 200)]      // negative page size -> default
    [InlineData(1, 999, 1, 500)]      // oversized page size -> hard max
    [InlineData(1, 501, 1, 500)]      // just over the max -> hard max
    public void Normalize_Should_ClampIntoSafeWindow(
        int pageNumber, int pageSize, int expectedNumber, int expectedSize)
    {
        var (number, size) = FinancePaging.Normalize(pageNumber, pageSize);

        number.Should().Be(expectedNumber);
        size.Should().Be(expectedSize);
    }

    [Fact]
    public void Normalize_Should_NeverExceedMaxPageSize()
    {
        var (_, size) = FinancePaging.Normalize(1, int.MaxValue);
        size.Should().Be(FinancePaging.MaxPageSize);
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 50, 100)]
    public void Offset_Should_ComputeZeroBasedRowOffset(int pageNumber, int pageSize, int expected)
    {
        FinancePaging.Offset(pageNumber, pageSize).Should().Be(expected);
    }
}
