using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/isSorted.test.ts (IsSorted lives on ListExtensions).
public class ListExtensionsTests
{
    private static readonly Func<int, int, int> Ascending = (a, b) => a - b;
    private static readonly Func<int, int, int> Descending = (a, b) => b - a;

    [Fact]
    public void EmptyList() =>
        Assert.True(new List<int>().IsSorted(Ascending));

    [Fact]
    public void ListWithOneElement() =>
        Assert.True(new List<int> { 1 }.IsSorted(Ascending));

    [Fact]
    public void ListWithTwoSortedElements() =>
        Assert.True(new List<int> { 1, 2 }.IsSorted(Ascending));

    [Fact]
    public void ListWithTwoEqualElements() =>
        Assert.True(new List<int> { 2, 2 }.IsSorted(Ascending));

    [Fact]
    public void ListWithTwoUnsortedElements() =>
        Assert.False(new List<int> { 2, 1 }.IsSorted(Ascending));

    [Fact]
    public void ListWithOneUnsortedPair() =>
        Assert.False(new List<int> { 1, 2, 3, 4, 6, 5, 7 }.IsSorted(Ascending));

    [Fact]
    public void ListWithOneUnsortedPairAtTheEnd() =>
        Assert.False(new List<int> { 1, 2, 3, 4, 5, 7, 6 }.IsSorted(Ascending));

    [Fact]
    public void ListWithOneUnsortedPairAtTheBeginning() =>
        Assert.False(new List<int> { 2, 1, 3, 4, 5, 6, 7 }.IsSorted(Ascending));

    [Fact]
    public void ListWithDuplicates() =>
        Assert.True(new List<int> { 1, 2, 2, 3, 4, 5, 6, 7 }.IsSorted(Ascending));

    [Fact]
    public void ListWithOppositeComparator() =>
        Assert.False(new List<int> { 1, 2, 2, 3, 4, 5, 6, 7 }.IsSorted(Descending));

    [Fact]
    public void ReverseSortedListWithOppositeComparator() =>
        Assert.True(new List<int> { 7, 6, 5, 4, 3, 2, 2, 1 }.IsSorted(Descending));
}
