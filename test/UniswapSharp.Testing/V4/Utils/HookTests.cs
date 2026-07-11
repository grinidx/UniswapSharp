using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.V4.Utils;

// Ported 1:1 from sdks/v4-sdk/src/utils/hook.test.ts
public class HookTests
{
    private static string ConstructHookAddress(params HookOptions[] hookOptions)
    {
        int hookFlags = 0;
        foreach (var option in hookOptions)
        {
            hookFlags |= 1 << (int)option;
        }
        string addressFlag = hookFlags.ToString("x");
        return "0x" + new string('0', 40 - addressFlag.Length) + addressFlag;
    }

    private const string allHooksAddress = "0x0000000000000000000000000000000000003fff";
    private const string emptyHookAddress = "0x0000000000000000000000000000000000000000";
    private static readonly string hookBeforeInitialize = ConstructHookAddress(HookOptions.BeforeInitialize);
    private static readonly string hookAfterInitialize = ConstructHookAddress(HookOptions.AfterInitialize);
    private static readonly string hookBeforeAddLiquidity = ConstructHookAddress(HookOptions.BeforeAddLiquidity);
    private static readonly string hookAfterAddLiquidity = ConstructHookAddress(HookOptions.AfterAddLiquidity);
    private static readonly string hookBeforeRemoveLiquidity = ConstructHookAddress(HookOptions.BeforeRemoveLiquidity);
    private static readonly string hookAfterRemoveLiquidity = ConstructHookAddress(HookOptions.AfterRemoveLiquidity);
    private static readonly string hookBeforeSwap = ConstructHookAddress(HookOptions.BeforeSwap);
    private static readonly string hookAfterSwap = ConstructHookAddress(HookOptions.AfterSwap);
    private static readonly string hookBeforeDonate = ConstructHookAddress(HookOptions.BeforeDonate);
    private static readonly string hookAfterDonate = ConstructHookAddress(HookOptions.AfterDonate);
    private static readonly string hookBeforeSwapReturnsDelta = ConstructHookAddress(HookOptions.BeforeSwapReturnsDelta);
    private static readonly string hookAfterSwapReturnsDelta = ConstructHookAddress(HookOptions.AfterSwapReturnsDelta);
    private static readonly string hookAfterAddLiquidityReturnsDelta = ConstructHookAddress(HookOptions.AfterAddLiquidityReturnsDelta);
    private static readonly string hookAfterRemoveLiquidityReturnsDelta = ConstructHookAddress(HookOptions.AfterRemoveLiquidityReturnsDelta);

    // ---- permissions ----
    [Fact]
    public void Permissions_ThrowsForInvalidAddress()
    {
        var ex = Assert.Throws<ArgumentException>(() => Hook.Permissions("0x123"));
        Assert.Equal("Invariant failed: invalid address", ex.Message);
    }

    [Fact]
    public void Permissions_WorksWithoutPrefix()
    {
        Assert.True(Hook.Permissions(hookBeforeInitialize[2..]).BeforeInitialize);
    }

    [Fact]
    public void Permissions_BeforeInitialize()
    {
        Assert.True(Hook.Permissions(hookBeforeInitialize).BeforeInitialize);
        Assert.True(Hook.Permissions(allHooksAddress).BeforeInitialize);
        Assert.False(Hook.Permissions(hookAfterInitialize).BeforeInitialize);
        Assert.False(Hook.Permissions(emptyHookAddress).BeforeInitialize);
    }

    [Fact]
    public void Permissions_AfterInitialize()
    {
        Assert.True(Hook.Permissions(hookAfterInitialize).AfterInitialize);
        Assert.True(Hook.Permissions(allHooksAddress).AfterInitialize);
        Assert.False(Hook.Permissions(hookBeforeInitialize).AfterInitialize);
        Assert.False(Hook.Permissions(hookBeforeAddLiquidity).AfterInitialize);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterInitialize);
    }

    [Fact]
    public void Permissions_BeforeAddLiquidity()
    {
        Assert.True(Hook.Permissions(hookBeforeAddLiquidity).BeforeAddLiquidity);
        Assert.True(Hook.Permissions(allHooksAddress).BeforeAddLiquidity);
        Assert.False(Hook.Permissions(hookBeforeInitialize).BeforeAddLiquidity);
        Assert.False(Hook.Permissions(hookAfterAddLiquidity).BeforeAddLiquidity);
        Assert.False(Hook.Permissions(emptyHookAddress).BeforeAddLiquidity);
    }

    [Fact]
    public void Permissions_AfterAddLiquidity()
    {
        Assert.True(Hook.Permissions(hookAfterAddLiquidity).AfterAddLiquidity);
        Assert.True(Hook.Permissions(allHooksAddress).AfterAddLiquidity);
        Assert.False(Hook.Permissions(hookBeforeAddLiquidity).AfterAddLiquidity);
        Assert.False(Hook.Permissions(hookBeforeRemoveLiquidity).AfterAddLiquidity);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterAddLiquidity);
    }

    [Fact]
    public void Permissions_BeforeRemoveLiquidity()
    {
        Assert.True(Hook.Permissions(hookBeforeRemoveLiquidity).BeforeRemoveLiquidity);
        Assert.True(Hook.Permissions(allHooksAddress).BeforeRemoveLiquidity);
        Assert.False(Hook.Permissions(hookAfterAddLiquidity).BeforeRemoveLiquidity);
        Assert.False(Hook.Permissions(hookAfterRemoveLiquidity).BeforeRemoveLiquidity);
        Assert.False(Hook.Permissions(emptyHookAddress).BeforeRemoveLiquidity);
    }

    [Fact]
    public void Permissions_AfterRemoveLiquidity()
    {
        Assert.True(Hook.Permissions(hookAfterRemoveLiquidity).AfterRemoveLiquidity);
        Assert.True(Hook.Permissions(allHooksAddress).AfterRemoveLiquidity);
        Assert.False(Hook.Permissions(hookBeforeRemoveLiquidity).AfterRemoveLiquidity);
        Assert.False(Hook.Permissions(hookBeforeSwap).AfterRemoveLiquidity);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterRemoveLiquidity);
    }

    [Fact]
    public void Permissions_BeforeSwap()
    {
        Assert.True(Hook.Permissions(hookBeforeSwap).BeforeSwap);
        Assert.True(Hook.Permissions(allHooksAddress).BeforeSwap);
        Assert.False(Hook.Permissions(hookAfterRemoveLiquidity).BeforeSwap);
        Assert.False(Hook.Permissions(hookAfterSwap).BeforeSwap);
        Assert.False(Hook.Permissions(emptyHookAddress).BeforeSwap);
    }

    [Fact]
    public void Permissions_AfterSwap()
    {
        Assert.True(Hook.Permissions(hookAfterSwap).AfterSwap);
        Assert.True(Hook.Permissions(allHooksAddress).AfterSwap);
        Assert.False(Hook.Permissions(hookBeforeSwap).AfterSwap);
        Assert.False(Hook.Permissions(hookBeforeDonate).AfterSwap);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterSwap);
    }

    [Fact]
    public void Permissions_BeforeDonate()
    {
        Assert.True(Hook.Permissions(hookBeforeDonate).BeforeDonate);
        Assert.True(Hook.Permissions(allHooksAddress).BeforeDonate);
        Assert.False(Hook.Permissions(hookAfterSwap).BeforeDonate);
        Assert.False(Hook.Permissions(hookAfterDonate).BeforeDonate);
        Assert.False(Hook.Permissions(emptyHookAddress).BeforeDonate);
    }

    [Fact]
    public void Permissions_AfterDonate()
    {
        Assert.True(Hook.Permissions(hookAfterDonate).AfterDonate);
        Assert.True(Hook.Permissions(allHooksAddress).AfterDonate);
        Assert.False(Hook.Permissions(hookBeforeDonate).AfterDonate);
        Assert.False(Hook.Permissions(hookBeforeSwapReturnsDelta).AfterDonate);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterDonate);
    }

    [Fact]
    public void Permissions_BeforeSwapReturnsDelta()
    {
        Assert.True(Hook.Permissions(hookBeforeSwapReturnsDelta).BeforeSwapReturnsDelta);
        Assert.True(Hook.Permissions(allHooksAddress).BeforeSwapReturnsDelta);
        Assert.False(Hook.Permissions(hookAfterDonate).BeforeSwapReturnsDelta);
        Assert.False(Hook.Permissions(hookAfterSwapReturnsDelta).BeforeSwapReturnsDelta);
        Assert.False(Hook.Permissions(emptyHookAddress).BeforeSwapReturnsDelta);
    }

    [Fact]
    public void Permissions_AfterSwapReturnsDelta()
    {
        Assert.True(Hook.Permissions(hookAfterSwapReturnsDelta).AfterSwapReturnsDelta);
        Assert.True(Hook.Permissions(allHooksAddress).AfterSwapReturnsDelta);
        Assert.False(Hook.Permissions(hookBeforeSwapReturnsDelta).AfterSwapReturnsDelta);
        Assert.False(Hook.Permissions(hookAfterAddLiquidityReturnsDelta).AfterSwapReturnsDelta);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterSwapReturnsDelta);
    }

    [Fact]
    public void Permissions_AfterAddLiquidityReturnsDelta()
    {
        Assert.True(Hook.Permissions(hookAfterAddLiquidityReturnsDelta).AfterAddLiquidityReturnsDelta);
        Assert.True(Hook.Permissions(allHooksAddress).AfterAddLiquidityReturnsDelta);
        Assert.False(Hook.Permissions(hookAfterSwapReturnsDelta).AfterAddLiquidityReturnsDelta);
        Assert.False(Hook.Permissions(hookAfterRemoveLiquidityReturnsDelta).AfterAddLiquidityReturnsDelta);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterAddLiquidityReturnsDelta);
    }

    [Fact]
    public void Permissions_AfterRemoveLiquidityReturnsDelta()
    {
        Assert.True(Hook.Permissions(hookAfterRemoveLiquidityReturnsDelta).AfterRemoveLiquidityReturnsDelta);
        Assert.True(Hook.Permissions(allHooksAddress).AfterRemoveLiquidityReturnsDelta);
        Assert.False(Hook.Permissions(hookAfterAddLiquidityReturnsDelta).AfterRemoveLiquidityReturnsDelta);
        Assert.False(Hook.Permissions(hookBeforeSwapReturnsDelta).AfterRemoveLiquidityReturnsDelta);
        Assert.False(Hook.Permissions(emptyHookAddress).AfterRemoveLiquidityReturnsDelta);
    }

    // ---- hasPermission ----
    [Fact]
    public void HasPermission_ThrowsForInvalidAddress()
    {
        var ex = Assert.Throws<ArgumentException>(() => Hook.HasPermission("0x123", HookOptions.BeforeInitialize));
        Assert.Equal("Invariant failed: invalid address", ex.Message);
    }

    [Fact]
    public void HasPermission_WorksWithoutPrefix()
    {
        Assert.True(Hook.HasPermission(hookBeforeInitialize[2..], HookOptions.BeforeInitialize));
    }

    [Fact]
    public void HasPermission_BeforeInitialize()
    {
        Assert.True(Hook.HasPermission(hookBeforeInitialize, HookOptions.BeforeInitialize));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.BeforeInitialize));
    }

    [Fact]
    public void HasPermission_AfterInitialize()
    {
        Assert.True(Hook.HasPermission(hookAfterInitialize, HookOptions.AfterInitialize));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.AfterInitialize));
    }

    [Fact]
    public void HasPermission_BeforeAddLiquidity()
    {
        Assert.True(Hook.HasPermission(hookBeforeAddLiquidity, HookOptions.BeforeAddLiquidity));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.BeforeAddLiquidity));
    }

    [Fact]
    public void HasPermission_AfterAddLiquidity()
    {
        Assert.True(Hook.HasPermission(hookAfterAddLiquidity, HookOptions.AfterAddLiquidity));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.AfterAddLiquidity));
    }

    [Fact]
    public void HasPermission_BeforeRemoveLiquidity()
    {
        Assert.True(Hook.HasPermission(hookBeforeRemoveLiquidity, HookOptions.BeforeRemoveLiquidity));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.BeforeRemoveLiquidity));
    }

    [Fact]
    public void HasPermission_AfterRemoveLiquidity()
    {
        Assert.True(Hook.HasPermission(hookAfterRemoveLiquidity, HookOptions.AfterRemoveLiquidity));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.AfterRemoveLiquidity));
    }

    [Fact]
    public void HasPermission_BeforeSwap()
    {
        Assert.True(Hook.HasPermission(hookBeforeSwap, HookOptions.BeforeSwap));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.BeforeSwap));
    }

    [Fact]
    public void HasPermission_AfterSwap()
    {
        Assert.True(Hook.HasPermission(hookAfterSwap, HookOptions.AfterSwap));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.AfterSwap));
    }

    [Fact]
    public void HasPermission_BeforeDonate()
    {
        Assert.True(Hook.HasPermission(hookBeforeDonate, HookOptions.BeforeDonate));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.BeforeDonate));
    }

    [Fact]
    public void HasPermission_AfterDonate()
    {
        Assert.True(Hook.HasPermission(hookAfterDonate, HookOptions.AfterDonate));
        Assert.False(Hook.HasPermission(emptyHookAddress, HookOptions.AfterDonate));
    }

    [Fact]
    public void HasPermission_BeforeSwapReturnsDelta()
    {
        Assert.True(Hook.HasPermission(hookBeforeSwapReturnsDelta, HookOptions.BeforeSwapReturnsDelta));
        Assert.False(Hook.HasPermission(hookAfterDonate, HookOptions.BeforeSwapReturnsDelta));
    }

    [Fact]
    public void HasPermission_AfterSwapReturnsDelta()
    {
        Assert.True(Hook.HasPermission(hookAfterSwapReturnsDelta, HookOptions.AfterSwapReturnsDelta));
        Assert.False(Hook.HasPermission(hookBeforeSwapReturnsDelta, HookOptions.AfterSwapReturnsDelta));
    }

    [Fact]
    public void HasPermission_AfterAddLiquidityReturnsDelta()
    {
        Assert.True(Hook.HasPermission(hookAfterAddLiquidityReturnsDelta, HookOptions.AfterAddLiquidityReturnsDelta));
        Assert.False(Hook.HasPermission(hookAfterSwapReturnsDelta, HookOptions.AfterAddLiquidityReturnsDelta));
    }

    [Fact]
    public void HasPermission_AfterRemoveLiquidityReturnsDelta()
    {
        Assert.True(Hook.HasPermission(hookAfterRemoveLiquidityReturnsDelta, HookOptions.AfterRemoveLiquidityReturnsDelta));
        Assert.False(Hook.HasPermission(hookAfterAddLiquidityReturnsDelta, HookOptions.AfterRemoveLiquidityReturnsDelta));
    }

    // ---- hasInitializePermissions ----
    [Fact]
    public void HasInitializePermissions_Before() => Assert.True(Hook.HasInitializePermissions(hookBeforeInitialize));

    [Fact]
    public void HasInitializePermissions_After() => Assert.True(Hook.HasInitializePermissions(hookAfterInitialize));

    [Fact]
    public void HasInitializePermissions_FalseForNonInitialize() => Assert.False(Hook.HasInitializePermissions(hookAfterSwap));

    // ---- hasLiquidityPermissions ----
    [Fact]
    public void HasLiquidityPermissions_BeforeAdd() => Assert.True(Hook.HasLiquidityPermissions(hookBeforeAddLiquidity));

    [Fact]
    public void HasLiquidityPermissions_AfterAdd() => Assert.True(Hook.HasLiquidityPermissions(hookAfterAddLiquidity));

    [Fact]
    public void HasLiquidityPermissions_BeforeRemove() => Assert.True(Hook.HasLiquidityPermissions(hookBeforeRemoveLiquidity));

    [Fact]
    public void HasLiquidityPermissions_AfterRemove() => Assert.True(Hook.HasLiquidityPermissions(hookAfterRemoveLiquidity));

    [Fact]
    public void HasLiquidityPermissions_FalseIfOnlyDeltaFlag() => Assert.False(Hook.HasLiquidityPermissions(hookAfterRemoveLiquidityReturnsDelta));

    // ---- hasSwapPermissions ----
    [Fact]
    public void HasSwapPermissions_Before() => Assert.True(Hook.HasSwapPermissions(hookBeforeSwap));

    [Fact]
    public void HasSwapPermissions_After() => Assert.True(Hook.HasSwapPermissions(hookAfterSwap));

    [Fact]
    public void HasSwapPermissions_FalseIfOnlyDeltaFlag() => Assert.False(Hook.HasSwapPermissions(hookBeforeSwapReturnsDelta));

    // ---- hasDonatePermissions ----
    [Fact]
    public void HasDonatePermissions_Before() => Assert.True(Hook.HasDonatePermissions(hookBeforeDonate));

    [Fact]
    public void HasDonatePermissions_After() => Assert.True(Hook.HasDonatePermissions(hookAfterDonate));

    [Fact]
    public void HasDonatePermissions_FalseForNonDonate() => Assert.False(Hook.HasDonatePermissions(hookAfterSwap));
}
