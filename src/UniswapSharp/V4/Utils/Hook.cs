using UniswapSharp.Core.Utils;

namespace UniswapSharp.V4.Utils;

/// <summary>
/// The hook permission flags, ported from v4-sdk/src/utils/hook.ts. The enum value IS the flag's
/// bit index within the hook address (upstream's <c>hookFlagIndex</c>), bits 0-13.
/// </summary>
public enum HookOptions
{
    AfterRemoveLiquidityReturnsDelta = 0,
    AfterAddLiquidityReturnsDelta = 1,
    AfterSwapReturnsDelta = 2,
    BeforeSwapReturnsDelta = 3,
    AfterDonate = 4,
    BeforeDonate = 5,
    AfterSwap = 6,
    BeforeSwap = 7,
    AfterRemoveLiquidity = 8,
    BeforeRemoveLiquidity = 9,
    AfterAddLiquidity = 10,
    BeforeAddLiquidity = 11,
    AfterInitialize = 12,
    BeforeInitialize = 13,
}

/// <summary>The decoded set of hook permissions for an address.</summary>
public class HookPermissions
{
    public bool BeforeInitialize { get; init; }
    public bool AfterInitialize { get; init; }
    public bool BeforeAddLiquidity { get; init; }
    public bool AfterAddLiquidity { get; init; }
    public bool BeforeRemoveLiquidity { get; init; }
    public bool AfterRemoveLiquidity { get; init; }
    public bool BeforeSwap { get; init; }
    public bool AfterSwap { get; init; }
    public bool BeforeDonate { get; init; }
    public bool AfterDonate { get; init; }
    public bool BeforeSwapReturnsDelta { get; init; }
    public bool AfterSwapReturnsDelta { get; init; }
    public bool AfterAddLiquidityReturnsDelta { get; init; }
    public bool AfterRemoveLiquidityReturnsDelta { get; init; }
}

public static class Hook
{
    public static HookPermissions Permissions(string address)
    {
        CheckAddress(address);
        return new HookPermissions
        {
            BeforeInitialize = HasPermissionInternal(address, HookOptions.BeforeInitialize),
            AfterInitialize = HasPermissionInternal(address, HookOptions.AfterInitialize),
            BeforeAddLiquidity = HasPermissionInternal(address, HookOptions.BeforeAddLiquidity),
            AfterAddLiquidity = HasPermissionInternal(address, HookOptions.AfterAddLiquidity),
            BeforeRemoveLiquidity = HasPermissionInternal(address, HookOptions.BeforeRemoveLiquidity),
            AfterRemoveLiquidity = HasPermissionInternal(address, HookOptions.AfterRemoveLiquidity),
            BeforeSwap = HasPermissionInternal(address, HookOptions.BeforeSwap),
            AfterSwap = HasPermissionInternal(address, HookOptions.AfterSwap),
            BeforeDonate = HasPermissionInternal(address, HookOptions.BeforeDonate),
            AfterDonate = HasPermissionInternal(address, HookOptions.AfterDonate),
            BeforeSwapReturnsDelta = HasPermissionInternal(address, HookOptions.BeforeSwapReturnsDelta),
            AfterSwapReturnsDelta = HasPermissionInternal(address, HookOptions.AfterSwapReturnsDelta),
            AfterAddLiquidityReturnsDelta = HasPermissionInternal(address, HookOptions.AfterAddLiquidityReturnsDelta),
            AfterRemoveLiquidityReturnsDelta = HasPermissionInternal(address, HookOptions.AfterRemoveLiquidityReturnsDelta),
        };
    }

    public static bool HasPermission(string address, HookOptions hookOption)
    {
        CheckAddress(address);
        return HasPermissionInternal(address, hookOption);
    }

    public static bool HasInitializePermissions(string address)
    {
        CheckAddress(address);
        return HasPermissionInternal(address, HookOptions.BeforeInitialize) ||
               HasPermissionInternal(address, HookOptions.AfterInitialize);
    }

    public static bool HasLiquidityPermissions(string address)
    {
        CheckAddress(address);
        // this implicitly encapsulates liquidity delta permissions
        return HasPermissionInternal(address, HookOptions.BeforeAddLiquidity) ||
               HasPermissionInternal(address, HookOptions.AfterAddLiquidity) ||
               HasPermissionInternal(address, HookOptions.BeforeRemoveLiquidity) ||
               HasPermissionInternal(address, HookOptions.AfterRemoveLiquidity);
    }

    public static bool HasSwapPermissions(string address)
    {
        CheckAddress(address);
        // this implicitly encapsulates swap delta permissions
        return HasPermissionInternal(address, HookOptions.BeforeSwap) ||
               HasPermissionInternal(address, HookOptions.AfterSwap);
    }

    public static bool HasDonatePermissions(string address)
    {
        CheckAddress(address);
        return HasPermissionInternal(address, HookOptions.BeforeDonate) ||
               HasPermissionInternal(address, HookOptions.AfterDonate);
    }

    private static bool HasPermissionInternal(string address, HookOptions hookOption)
    {
        // Use only the last 4 bytes (last 8 hex chars) to avoid precision issues; all flags are in bits 0-13.
        string last8 = address.Length >= 8 ? address[^8..] : address;
        uint value = Convert.ToUInt32(last8, 16);
        return (value & (1u << (int)hookOption)) != 0;
    }

    private static void CheckAddress(string address)
    {
        if (!IsAddress(address))
        {
            throw new ArgumentException("Invariant failed: invalid address");
        }
    }

    private static bool IsAddress(string address)
    {
        string candidate = address.StartsWith("0x") || address.StartsWith("0X") ? address : "0x" + address;
        try
        {
            AddressValidator.ValidateAndParseAddress(candidate);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
