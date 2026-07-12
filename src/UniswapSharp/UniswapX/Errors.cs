namespace UniswapSharp.UniswapX;

/// <summary>Port of uniswapx-sdk <c>errors.ts</c>: thrown when a chain/order lookup is missing from a configuration map.</summary>
public sealed class MissingConfiguration : Exception
{
    public MissingConfiguration(string key, string value)
        : base($"Missing configuration for {key}: {value}")
    {
    }
}
