# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Upstream parity sweep — `6081b3e` → `35c4e35` (2026-09-01)

Brings the port up to upstream `Uniswap/sdks@35c4e35`, 54 commits on from the previous pin.

#### Fixed
- **`Router`** — `MixedRouteSDK.MidPrice` returned an **inverted** price across native/wrapped
  boundaries (upstream #706, ROUTE-886). `PartitionMixedRouteByProtocol` also failed to end a
  section at such a boundary, and `GetOutputOfPools` picked the wrong side of a genuine ETH/WETH
  pool.
- **`UniversalRouter`** — `ACROSS_V4_DEPOSIT_V3` was encoded as a flat 13-value parameter list
  instead of the single offset-prefixed tuple `ChainedActions.sol` decodes, so **every Across
  bridge deposit this SDK produced was undecodable on-chain** (#690). V4 exact-output legs took
  output with the `OPEN_DELTA` sentinel, silently forwarding an under-delivered partial fill
  instead of reverting (#640, ROUTE-1394).
- **`Core`** — sepolia `TickLensAddress` held the multicall address (#654). Arc's average block
  time corrected to 0.5s.

#### Changed
- **BREAKING — `LiquidityLauncher.Config.Positions.BuildPositionDefinitions`** now requires the
  raised `currency` and launched `token` addresses so it can apply v4 currency ordering (#651).
  When `currency` sorts as `currency0` — always so for native-ETH launches — custom asymmetric
  ranges are mirrored onto the reciprocal price band instead of landing on the mirror image of the
  intended band. **This changes output for existing callers passing custom asymmetric ranges**;
  the new values are correct, but they are different values. Full-range positions are unaffected.
- **BREAKING — `LiquidityLauncher.Config.Fees.FeeToTickSpacing`** is superseded by
  `ResolveNewPoolTickSpacing`, now `max(round(fee / 100), 1)` and no longer consulting the v3
  `TICK_SPACINGS` table (#699). Fee 2500 → 25 (was 1), 3000 → 30 (was 60), 10000 → 100 (was 200).
  The old name remains as an `[Obsolete]` alias.

#### Added
- **`UniversalRouter.DirectTransfers`** — opt-in direct transfers on swap steps (#638). Steps may
  pull input straight from the user and pay output straight to the recipient, bounded so the user
  never pays more than `exactOrMaxAmountIn` or receives less than the minimum output. Off by
  default; the default regime encodes byte-identically to before.
- **`LiquidityLauncher.QuickLaunch`** — the canonical quick-launch preset and pure, address-free
  matcher (#643, #664, #673, #680).
- **`LiquidityLauncher.InstantLaunch`** — deployment registry, preset, pool key/id derivation and
  transaction assembler (#660, #663, #672, #678, #680, #718).
- **`LiquidityLauncher.InstantLaunchFees`** — creator-fee accumulation, claimable and compounded
  math over indexed events (#666, #670).
- Chain and address registry updates: Arc (5042) across the launcher stack, Ink (57073) for the
  UniswapX DutchV3 rollout, Monad for SmartWallet, `PermissionedV4HooksAddress` in `Core`, and
  redeployed universal-router and LBPStrategy addresses.

### Dependency sweep and test-platform migration

#### Changed
- Dependency sweep, verified behind a green 1,695-test suite: `xunit.v3` 3.2.2 → 4.0.0,
  `xunit.runner.visualstudio` 3.1.5 → 4.0.0, `Microsoft.NET.Test.Sdk` 18.7.0 → 18.9.0,
  `AwesomeAssertions` 9.4.0 → 9.6.0, `BouncyCastle.Cryptography` 2.5.1 → 2.7.0,
  `MinVer` 6.0.0 → 7.0.0, `Microsoft.SourceLink.GitHub` 8.0.0 → 10.0.400, and the
  `setup-dotnet`, `codeql-action`, `publish-unit-test-result-action` and `action-gh-release`
  pins. No library API or protocol-math change.
- Tests now run on **Microsoft.Testing.Platform** instead of VSTest. `xunit.v3` 4.x drops the
  VSTest bridge on the .NET 10 SDK, so `global.json` selects the MTP runner and CI uses MTP's
  reporting flags. `coverlet.collector` is replaced by `Microsoft.Testing.Extensions.CodeCoverage`
  and `.TrxReport`; the TRX check and Cobertura coverage report are unchanged.
- Dependabot is now security-updates only; routine currency is handled by verified sweeps.

## [1.0.0] - 2026-07-13

First stable release — the full [`Uniswap/sdks`](https://github.com/Uniswap/sdks) monorepo surface
is ported to C#/.NET 10, test-first and verified to the digit against the upstream `.test.ts` vectors
(**1,695 xUnit tests, 0 failing**, on Linux / Windows / macOS, with zero compiler warnings).

Preceded by `1.0.0-rc.1` and `1.0.0-rc.2`, both published and smoke-tested from nuget.org.

### Added
- **sdk-core** (`UniswapSharp.Core`) — currencies/tokens, `Fraction`/`Percent`/`Price`/`CurrencyAmount`
  with exact `BigInteger`/`BigRational` arithmetic, and the full chain / addresses / WETH9 registry.
- **v2-sdk** (`UniswapSharp.V2`) — `Pair` (CREATE2 + 997/1000 fee math), `Route`, `Trade`, `Router`.
- **v3-sdk** (`UniswapSharp.V3`) — `Pool`, `Position`, `Route`, `Trade`, `Tick`; tick / sqrt-price /
  swap / liquidity math; and the V3 periphery calldata builders (SwapRouter, NonfungiblePositionManager,
  Quoter, Payments, Multicall, Staker, SelfPermit).
- **v4-sdk** (`UniswapSharp.V4`) — currency-based `Pool`/`Position`/`Route`/`Trade`, hook permissions,
  `V4Planner`/`V4PositionPlanner`, `PositionManager`, and the actions parser.
- **router-sdk** (`UniswapSharp.Router`) — mixed v2+v3+v4 routes, aggregated `Trade`, SwapRouter02 calldata.
- **universal-router-sdk** (`UniswapSharp.UniversalRouter`) — `RoutePlanner`, `RouterTradeAdapter`,
  `SwapRouter`, and signed-route EIP-712.
- **uniswapx-sdk** (`UniswapSharp.UniswapX`) — Dutch/Priority/Relay/V3/Hybrid orders, decay math,
  builders, trades, and Permit2-witness EIP-712 order hashing.
- **permit2-sdk** (`UniswapSharp.Permit2`) — `SignatureTransfer`, `AllowanceTransfer`, and a byte-exact
  EIP-712 typed-data encoder (port of ethers `_TypedDataEncoder`).
- **smart-wallet-sdk** (`UniswapSharp.SmartWallet`) — ERC-7821 call planners and encoders.
- **liquidity-launcher-sdk** (`UniswapSharp.LiquidityLauncher`) — launch-config math, CREATE2
  poolId/salts, and calldata encoding.
- **flashtestations-sdk** (`UniswapSharp.Flashtestations`) — TEE workload-ID (keccak) and block
  verification behind an injectable RPC interface.
- **tamperproof-transactions** (`UniswapSharp.Tamperproof`) — EIP-7754 sign/verify (RSA/ECDSA via
  `System.Security.Cryptography`, Ed25519 via BouncyCastle), canonical JSON, and DNS-over-HTTPS
  behind an injectable resolver.
- Repository foundations: CI with PR test reporting + coverage, CodeQL, Dependabot, community-health
  files, contributor & porting guides, and tag-driven NuGet packaging (SourceLink + symbols + MinVer).

### Changed
- **Nullable-reference hardening of the public API (pre-1.0.0).** Option/params types now carry explicit null
  contracts taken from the upstream TypeScript interfaces: upstream-required fields are `required` in C#,
  upstream-optional fields (`x?: T`) are nullable. `CurrencyAmount.Wrapped()` and `.AsBaseCurrency()` are now
  non-nullable (they provably never return null), strengthening the contract for callers. The solution builds
  with **zero compiler warnings**. Consumers using object initializers may need to supply fields now marked
  `required` — a deliberate source-level tightening done before the stable release.
- Test assertions migrated from FluentAssertions to **AwesomeAssertions** (Apache-2.0 community fork)
  to avoid FluentAssertions v8's commercial license. Test-only; no effect on the shipped package.
- Runtime dependencies updated to `Nethereum` 6.1.0 and `ExtendedNumerics.BigRational` 3000.0.2.132,
  with an explicit `Newtonsoft.Json` 13.0.4 pin to clear the transitive NU1903 advisory.
- `BouncyCastle.Cryptography` 2.5.1 promoted from a transitive to an explicit dependency (same version)
  to provide Ed25519 for the tamperproof-transactions port.

### Fixed
- **V3 `SwapRouter` input-token-permit path was unusable.** `SwapOptions.InputTokenPermit` was typed against an
  empty stub class, so the only assignable value failed `SelfPermit.EncodePermit`'s type tests and always threw
  `"Invalid permit options"` — while the valid permit types could not be assigned to it at all. The upstream
  `PermitOptions` union is now modelled as `SelfPermit.IPermitOptions`, implemented by both
  `StandardPermitArguments` and `AllowedPermitArguments`. Every permit option (`InputTokenPermit`,
  `OutputTokenPermit`, `Token0Permit`, `Token1Permit`) is typed against it instead of `object`, so misuse is now
  a compile error. Pinned by a new regression test; upstream ships no test for this path.
- `PoolTests.BigNums_CorrectlyHandlesTwoBigIntegers` awaited its `GetInputAmount` call — previously the
  unawaited `Task` swallowed exceptions, so the test could pass vacuously.
- Several latent correctness bugs found while porting and pinned with upstream vectors: `sdk-core`
  `sqrt`, the FOT `Token` guard, zkSync address slicing, exact `Fraction` formatting (no float),
  `CurrencyAmount.ToExact()` overflow/format handling, `EncodeRouteToPath`, `Multicall` encoding,
  `NearestUsableTick` rounding, and `Utilities.ToHex` sign-nibble handling.

### Notes / deferred
- Live-network paths are ported behind injectable interfaces and pinned to the upstream mock vectors
  (flashtestations RPC, tamperproof DNS-over-HTTPS/HTTPS, and fork-dependent quoting/trade cases);
  end-to-end validation against a live node/DNS is deferred. Upstream code-generated contract bindings
  (`contracts/**`) and Foundry Solidity suites are intentionally not ported. See
  [docs/PORTING.md](docs/PORTING.md) for the full list of skips and intentional divergences.

[Unreleased]: https://github.com/grinidx/UniswapSharp/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/grinidx/UniswapSharp/releases/tag/v1.0.0
