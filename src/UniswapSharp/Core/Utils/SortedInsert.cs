namespace UniswapSharp.Core.Utils;

public static class SortedInsert
{
    // Given an array of items sorted by `comparator`, insert an item into its sort index and constrain the size to
    // `maxSize` by removing the last item
    public static T? Insert<T>(List<T> items, T add, int maxSize, Comparison<T> comparator)
    {
        if (maxSize <= 0)
            throw new ArgumentException("MAX_SIZE_ZERO");

        // This is an invariant because the interface cannot return multiple removed items if items.Count exceeds maxSize
        if (items.Count > maxSize)
            throw new ArgumentException("ITEMS_SIZE");

        // Short circuit first item add
        if (items.Count == 0)
        {
            items.Add(add);
            return default(T);
        }
        else
        {
            bool isFull = items.Count == maxSize;
            // Short circuit if full and the additional item does not come before the last item
            if (isFull && comparator(items[items.Count - 1], add) <= 0)
            {
                return add;
            }

            int lo = 0, hi = items.Count;

            while (lo < hi)
            {
                int mid = (lo + hi) >>> 1;
                if (comparator(items[mid], add) <= 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            items.Insert(lo, add);
            if (isFull)
            {
                T lastItem = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                return lastItem;
            }
            return default(T);
        }
    }
}