# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

First release candidate — the full [`Uniswap/sdks`](https://github.com/Uniswap/sdks) monorepo surface
is ported to C#/.NET 10, test-first and verified to the digit against the upstream `.test.ts` vectors
(**1,694 xUnit tests, 0 failing**, on Linux / Windows / macOS).

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
