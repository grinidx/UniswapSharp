namespace UniswapSharp.Core;

public class ChainAddresses
{
    public string? V3CoreFactoryAddress { get; set; }
    public string? MulticallAddress { get; set; }
    public string? QuoterAddress { get; set; }
    public string? V3MigratorAddress { get; set; }
    public string? NonfungiblePositionManagerAddress { get; set; }
    public string? TickLensAddress { get; set; }
    public string? SwapRouter02Address { get; set; }
    public string? MixedRouteQuoterV1Address { get; set; }
    public string? MixedRouteQuoterV2Address { get; set; }

    // v4
    public string? V4PoolManagerAddress { get; set; }
    public string? V4PositionManagerAddress { get; set; }
    public string? V4StateView { get; set; }
    public string? V4QuoterAddress { get; set; }
    public string? PermissionedV4PositionManagerAddress { get; set; }
}
