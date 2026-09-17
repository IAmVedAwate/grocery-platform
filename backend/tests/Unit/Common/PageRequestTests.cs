using Application.Common;
using Xunit;

namespace Unit.Common;

public class PageRequestTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_IsClampedToAtLeastOne(int requestedPage, int expectedPage)
    {
        var page = new PageRequest(requestedPage, 20);
        Assert.Equal(expectedPage, page.Page);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(500, 100)]
    [InlineData(50, 50)]
    public void PageSize_IsClampedBetweenDefaultAndMax(int requestedSize, int expectedSize)
    {
        var page = new PageRequest(1, requestedSize);
        Assert.Equal(expectedSize, page.PageSize);
    }

    [Fact]
    public void Skip_ComputesFromPageAndPageSize()
    {
        var page = new PageRequest(3, 20);
        Assert.Equal(40, page.Skip);
    }
}
