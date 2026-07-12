using System.Numerics;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>Config for a linear dutch decay (uniswapx-sdk <c>DutchDecayConfig</c>).</summary>
public sealed record DutchDecayConfig(BigInteger StartAmount, BigInteger EndAmount, long DecayStartTime, long DecayEndTime);

/// <summary>Port of uniswapx-sdk <c>utils/dutchDecay.ts</c>: exact linear decay between a start and end amount over time.</summary>
public static class DutchDecay
{
    /// <summary>Returns the linearly-decayed amount at <paramref name="atTime"/> (defaults to the current unix time).</summary>
    public static BigInteger GetDecayedAmount(DutchDecayConfig config, long? atTime = null)
    {
        long time = atTime ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BigInteger startAmount = config.StartAmount;
        BigInteger endAmount = config.EndAmount;
        long decayStartTime = config.DecayStartTime;
        long decayEndTime = config.DecayEndTime;

        // decay is over, return the ending amount
        if (decayEndTime <= time)
        {
            return endAmount;
        }

        // decay hasnt started, return the starting amount
        if (decayStartTime >= time)
        {
            return startAmount;
        }

        // no decay, just return the static amount
        if (startAmount == endAmount)
        {
            return startAmount;
        }

        BigInteger duration = decayEndTime - decayStartTime;
        BigInteger elapsed = time - decayStartTime;
        if (startAmount > endAmount)
        {
            // decaying downward
            BigInteger decay = (startAmount - endAmount) * elapsed / duration;
            return startAmount - decay;
        }
        else
        {
            // decaying upward
            BigInteger decay = (endAmount - startAmount) * elapsed / duration;
            return startAmount + decay;
        }
    }
}
