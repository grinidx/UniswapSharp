namespace UniswapSharp.UniswapX.Builder;

/// <summary>Guard-clause helper mirroring upstream <c>tiny-invariant</c> (throws on a false condition).</summary>
internal static class BuilderInvariant
{
    public static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>The current unix time in seconds (used by the "deadline in the future" checks).</summary>
    public static long NowSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
