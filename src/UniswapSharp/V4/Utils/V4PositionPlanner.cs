using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.V4.Entities;
using Act = UniswapSharp.V4.Utils.Actions;

namespace UniswapSharp.V4.Utils;

/// <summary>
/// A wrapper around <see cref="V4Planner"/> that builds the position-manager actions (mint,
/// increase, decrease, burn, settle-pair, close-currency, take-pair and sweep) with the ABI-ordered
/// inputs each action expects. Ported from v4-sdk/src/utils/v4PositionPlanner.ts.
/// </summary>
public class V4PositionPlanner : V4Planner
{
    /// <summary>MINT_POSITION.</summary>
    public void AddMint(
        Pool pool,
        int tickLower,
        int tickUpper,
        BigInteger liquidity,
        BigInteger amount0Max,
        BigInteger amount1Max,
        string owner,
        string hookData = Constants.EMPTY_BYTES)
    {
        PoolKey poolKey = Pool.GetPoolKey(pool.Currency0, pool.Currency1, pool.Fee, pool.TickSpacing, pool.Hooks);
        var inputs = new object?[]
        {
            new object?[] { poolKey.Currency0, poolKey.Currency1, poolKey.Fee, poolKey.TickSpacing, poolKey.Hooks },
            tickLower,
            tickUpper,
            liquidity,
            amount0Max,
            amount1Max,
            owner,
            hookData,
        };
        AddAction(Act.MINT_POSITION, inputs);
    }

    /// <summary>INCREASE_LIQUIDITY.</summary>
    public void AddIncrease(
        BigInteger tokenId,
        BigInteger liquidity,
        BigInteger amount0Max,
        BigInteger amount1Max,
        string hookData = Constants.EMPTY_BYTES)
    {
        var inputs = new object?[] { tokenId, liquidity, amount0Max, amount1Max, hookData };
        AddAction(Act.INCREASE_LIQUIDITY, inputs);
    }

    /// <summary>DECREASE_LIQUIDITY.</summary>
    public void AddDecrease(
        BigInteger tokenId,
        BigInteger liquidity,
        BigInteger amount0Min,
        BigInteger amount1Min,
        string hookData = Constants.EMPTY_BYTES)
    {
        var inputs = new object?[] { tokenId, liquidity, amount0Min, amount1Min, hookData };
        AddAction(Act.DECREASE_LIQUIDITY, inputs);
    }

    /// <summary>BURN_POSITION.</summary>
    public void AddBurn(
        BigInteger tokenId,
        BigInteger amount0Min,
        BigInteger amount1Min,
        string hookData = Constants.EMPTY_BYTES)
    {
        var inputs = new object?[] { tokenId, amount0Min, amount1Min, hookData };
        AddAction(Act.BURN_POSITION, inputs);
    }

    /// <summary>SETTLE_PAIR.</summary>
    public void AddSettlePair(BaseCurrency currency0, BaseCurrency currency1)
    {
        var inputs = new object?[] { CurrencyMap.ToAddress(currency0), CurrencyMap.ToAddress(currency1) };
        AddAction(Act.SETTLE_PAIR, inputs);
    }

    /// <summary>CLOSE_CURRENCY.</summary>
    public void AddCloseCurrency(BaseCurrency currency)
    {
        var inputs = new object?[] { CurrencyMap.ToAddress(currency) };
        AddAction(Act.CLOSE_CURRENCY, inputs);
    }

    /// <summary>TAKE_PAIR.</summary>
    public void AddTakePair(BaseCurrency currency0, BaseCurrency currency1, string recipient)
    {
        var inputs = new object?[] { CurrencyMap.ToAddress(currency0), CurrencyMap.ToAddress(currency1), recipient };
        AddAction(Act.TAKE_PAIR, inputs);
    }

    /// <summary>SWEEP.</summary>
    public void AddSweep(BaseCurrency currency, string to)
    {
        var inputs = new object?[] { CurrencyMap.ToAddress(currency), to };
        AddAction(Act.SWEEP, inputs);
    }
}
