using UniswapSharp.Core.Utils;

namespace UniswapSharp.Testing.Core.Utils;

public class SortedInsertTests
{
    private Comparison<int> comp = (a, b) => a - b;

    [Fact]
    public void ThrowsIfMaxSizeIsZero()
    {
        Assert.Throws<ArgumentException>(() => SortedInsert.Insert(new List<int>(), 1, 0, comp));
    }

    [Fact]
    public void ThrowsIfItemsLengthGreaterThanMaxSize()
    {
        Assert.Throws<ArgumentException>(() => SortedInsert.Insert(new List<int> { 1, 2 }, 1, 1, comp));
    }

    [Fact]
    public void AddsIfEmpty()
    {
        var arr = new List<int>();
        Assert.Equal(0, SortedInsert.Insert(arr, 3, 2, comp));
        Assert.Equal(new List<int> { 3 }, arr);
    }

    [Fact]
    public void AddsIfNotFull()
    {
        var arr = new List<int> { 1, 5 };
        Assert.Equal(0, SortedInsert.Insert(arr, 3, 3, comp));
        Assert.Equal(new List<int> { 1, 3, 5 }, arr);
    }

    [Fact]
    public void AddsIfWillNotBeFullAfter()
    {
        var arr = new List<int> { 1 };
        Assert.Equal(0, SortedInsert.Insert(arr, 0, 3, comp));
        Assert.Equal(new List<int> { 0, 1 }, arr);
    }

    [Fact]
    public void ReturnsAddIfSortsAfterLast()
    {
        var arr = new List<int> { 1, 2, 3 };
        Assert.Equal(4, SortedInsert.Insert(arr, 4, 3, comp));
        Assert.Equal(new List<int> { 1, 2, 3 }, arr);
    }

    [Fact]
    public void RemovesFromEndIfFull()
    {
        var arr = new List<int> { 1, 3, 4 };
        Assert.Equal(4, SortedInsert.Insert(arr, 2, 3, comp));
        Assert.Equal(new List<int> { 1, 2, 3 }, arr);
    }

    [Fact]
    public void UsesComparator()
    {
        var arr = new List<int> { 4, 2, 1 };
        Assert.Equal(1, SortedInsert.Insert(arr, 3, 3, (a, b) => comp(a, b) * -1));
        Assert.Equal(new List<int> { 4, 3, 2 }, arr);
    }

    public class MaxSizeOfOneTests
    {
        private Comparison<int> comp = (a, b) => a - b;

        [Fact]
        public void EmptyAdd()
        {
            var arr = new List<int>();
            Assert.Equal(0, SortedInsert.Insert(arr, 3, 1, comp));
            Assert.Equal(new List<int> { 3 }, arr);
        }

        [Fact]
        public void FullAddGreater()
        {
            var arr = new List<int> { 2 };
            Assert.Equal(3, SortedInsert.Insert(arr, 3, 1, comp));
            Assert.Equal(new List<int> { 2 }, arr);
        }

        [Fact]
        public void FullAddLesser()
        {
            var arr = new List<int> { 4 };
            Assert.Equal(4, SortedInsert.Insert(arr, 3, 1, comp));
            Assert.Equal(new List<int> { 3 }, arr);
        }
    }
}
