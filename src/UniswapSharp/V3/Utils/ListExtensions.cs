namespace UniswapSharp.V3.Utils;

public static class ListExtensions
{
    /// <summary>
    /// Determines if a list is sorted
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
    /// <param name="list">The list to check</param>
    /// <param name="comparator">The comparator function</param>
    /// <returns>true if sorted, false otherwise</returns>
    public static bool IsSorted<T>(this List<T> list, Func<T, T, int> comparator)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            if (comparator(list[i], list[i + 1]) > 0)
            {
                return false;
            }
        }
        return true;
    }
}