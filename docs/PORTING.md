# Porting Guide — upstream ↔ UniswapSharp

## 1. Upstream source of truth
- Repo: https://github.com/Uniswap/sdks — packages `sdk-core`, `v3-sdk` (and `v4-sdk`, later phase).
- Pinned commit: `6081b3e7169a761188cd5e77675be9e5da5d331e` (2026-07-09).
- Local clone (this workspace): `/home/devops/uniswap-sdks-official/sdks/`.
- Every upstream module has a `.test.ts` beside it — those are the acceptance tests we port.

## 2. This repo
- `@uniswap/sdk-core` → `UniswapSharp.Core` (`src/UniswapSharp/Core`)
- `@uniswap/v3-sdk`  → `UniswapSharp.V3`   (`src/UniswapSharp/V3`)
- Tests mirror the tree under `test/UniswapSharp.Testing/`.

## 3. Type & idiom conventions
| TypeScript (upstream) | C# (UniswapSharp) |
|---|---|
| `JSBI` / `bigint` | `System.Numerics.BigInteger` |
| Decimal.js-backed `Fraction` | `Fraction` / `BigRational` (exact) |
| `invariant(cond, msg)` | guard clause throwing (e.g. `ArgumentException`) |
| union/enum string types | C# `enum` (e.g. `FeeAmount`) |
| `*.test.ts` (mocha/jest) | xUnit `[Fact]`/`[Theory]` + AwesomeAssertions |
| named exports (camelCase) | public members (PascalCase) |
**Rule:** never use floating point in protocol math; match upstream numeric output to the digit.

## 4. File-by-file mapping (v3-sdk)
| Upstream `sdks/v3-sdk/src/…` | UniswapSharp `src/UniswapSharp/V3/…` | Tests ported | Status |
|---|---|---|---|
| `constants.ts` | `Constants.cs` | Yes — `V3/ConstantsTests.cs` | ported |
| `internalConstants.ts` | `Constants.cs` (`NEGATIVE_ONE`/`ZERO`/`ONE`/`Q96`/`Q192` merged in) | Indirect — `V3/ConstantsTests.cs` | ported |
| `index.ts` | *(n/a — barrel export; C# namespaces replace this)* | n/a | n/a |
| `entities/index.ts` | *(n/a — barrel export)* | n/a | n/a |
| `entities/pool.ts` | `Entities/Pool.cs` | Yes — `V3/Entities/PoolTests.cs` | ported |
| `entities/position.ts` | `Entities/Position.cs` | Yes — `V3/Entities/PositionTests.cs` | ported |
| `entities/route.ts` | `Entities/Route.cs` (+ `Entities/RouteInput.cs` helper DTO) | Yes — `V3/Entities/RouteTests.cs` | ported |
| `entities/tick.ts` | `Entities/Tick.cs` | Yes — `V3/Entities/TickTests.cs` | ported |
| `entities/tickDataProvider.ts` | `Entities/ITickDataProvider.cs`, `Entities/NoTickDataProvider.cs` | Yes — `V3/Entities/TickDataProviderTests.cs` | ported |
| `entities/tickListDataProvider.ts` | `Entities/TickListDataProvider.cs` | Yes — `V3/Entities/TickListDataProviderTests.cs` | ported |
| `entities/trade.ts` | `Entities/Trade.cs` (+ `Entities/Swap.cs` helper DTO) | Yes — `V3/Entities/TradeTests.cs` | ported |
| `multicall.ts` | `Multicall.cs` | Yes — `V3/MulticallTests.cs` (encodeMulticall) | ported |
| `nonfungiblePositionManager.ts` | `NonfungiblePositionManager.cs` | Yes — create/add/collect/remove/safeTransferFrom/getPermitData tests | ported |
| `payments.ts` | `Payments.cs` | Yes — `V3/PaymentsTests.cs` (5 cases) | ported |
| `quoter.ts` | `SwapQuoter.cs` | Yes — `V3/SwapQuoterTests.cs` (7 cases, V1 + QuoterV2) | ported |
| `selfPermit.ts` | `SelfPermit.cs` | Yes — `V3/SelfPermitTests.cs` (2 cases) | ported |
| `staker.ts` | `Staker.cs` | Yes — `V3/StakerTests.cs` (8 cases) | ported |
| `swapRouter.ts` | `SwapRouter.cs` | Yes — `V3/SwapRouterTests.cs` (12, single-trade) + `V3/SwapRouterMultiTests.cs` (24, multiple-trade + multiple-route) | ported |
| `utils/calldata.ts` | `Utils/Utilities.cs` (`ToHex`) | Yes — `V3/Utils/ToHexTests.cs` (incl. sign-nibble regressions) | ported |
| `utils/computePoolAddress.ts` | `Utils/ComputePoolAddress.cs` | Yes — `V3/Utils/ComputePoolAddressTests.cs` (2 cases; zkSync CREATE2 case omitted, path not yet ported) | ported |
| `utils/encodeRouteToPath.ts` | `Utils/EncodeRouteToPath.cs` | Yes — `EncodeRouteToPathTests.cs` (12 cases) | ported |
| `utils/encodeSqrtRatioX96.ts` | `Utils/EncodeSqrtRatioX96.cs` | Yes — `V3/Utils/EncodeSqrtRatioX96Tests.cs` (5 cases) | ported |
| `utils/fullMath.ts` | `Utils/FullMath.cs` | Indirect — exercised via `SqrtPriceMath`/`SwapMath` call sites in `PoolTests.cs`/`TradeTests.cs` | ported |
| `utils/index.ts` | *(n/a — barrel export)* | n/a | n/a |
| `utils/isSorted.ts` | `Utils/ListExtensions.cs` (`IsSorted`) | Yes — `V3/Utils/ListExtensionsTests.cs` (11 cases) | ported |
| `utils/liquidityMath.ts` | `Utils/LiquidityMath.cs` | Indirect — exercised via `PositionTests.cs`/`PoolTests.cs` | ported |
| `utils/maxLiquidityForAmounts.ts` | `Utils/MaxLiquidity.cs` | Yes — `V3/Utils/MaxLiquidityTests.cs` (18 cases, imprecise + precise) | ported |
| `utils/mostSignificantBit.ts` | `Utils/MostSignificantBitCalculator.cs` | Yes — `V3/Utils/MostSignificantBitCalculatorTests.cs` (5 cases) | ported |
| `utils/nearestUsableTick.ts` | `Utils/NearestUsableTick.cs` | Yes — `V3/Utils/NearestUsableTickTests.cs` (9 cases) | ported |
| `utils/position.ts` | `Utils/PositionLibrary.cs` (`GetTokensOwed`) | Yes — `V3/Utils/PositionLibraryTests.cs` (incl. subIn256 wraparound) | ported |
| `utils/priceTickConversions.ts` | `Utils/PriceTick.cs` | Yes — `V3/Utils/PriceTickTests.cs` (18 cases) | ported |
| `utils/sqrtPriceMath.ts` | `Utils/SqrtPriceMath.cs` | Indirect — exercised via `PoolTests.cs`/`TradeTests.cs` | ported |
| `utils/swapMath.ts` | `Utils/SwapMath.cs` | Indirect — exercised via `PoolTests.cs`/`TradeTests.cs` | ported |
| `utils/tickLibrary.ts` | `Utils/TickLibrary.cs` | Yes — `V3/Utils/TickLibraryTests.cs` (4 `GetFeeGrowthInside` cases) | ported |
| `utils/tickList.ts` | `Utils/TickList.cs` | Yes — `V3/Utils/TickListTests.cs` (validate/isBelowSmallest/isAtOrAboveLargest/nextInitializedTick[WithinOneWord]) | ported |
| `utils/tickMath.ts` | `Utils/TickMath.cs` | Yes — `V3/Utils/TickMathTests.cs` (9 cases) | ported |
| `utils/v3swap.ts` | `Utils/V3Swap.cs` | Indirect — exercised via `Pool.GetOutputAmount`/`GetInputAmount` in `PoolTests.cs`/`TradeTests.cs` | ported |

Legend: **ported** = fully implemented, at least indirectly test-covered; **partial** = implemented except for a
named gap; **stub** = throws `NotImplementedException`; **test-ported** = the upstream `.test.ts` has a direct
1:1 xUnit counterpart (see the "Tests ported" column — most entity tests are test-ported 1:1; most `utils/*`
math is only indirectly covered today and would benefit from dedicated `*Tests.cs` files ported test-first).

### Outstanding stubs (Phase B)
None — all seven original `NotImplementedException` stubs are ported test-first
(`PositionLibrary.SubIn256`, `PriceTick`, `Payments`, `SwapQuoter`, and
`NonfungiblePositionManager`). Two latent runtime bugs uncovered along the way
(`EncodeRouteToPath`, `Multicall.EncodeMulticall`) are fixed and test-covered.

## 5. Porting workflow (test-first)
1. Read upstream `.ts` + `.test.ts`. 2. Port the test cases to xUnit; watch them fail.
3. Implement until green, matching numbers to the digit. 4. Update the table above. 5. PR into `main`.

## 6. Intentional divergences
- `CurrencyAmount.ToExact` now computes the exact decimal with `BigInteger` (integer part + zero-padded,
  trailing-trimmed fractional part), matching Decimal.js. The earlier `(decimal)` cast overflowed
  `System.Decimal` (~7.9e28) for large amounts; hardened test-first (`CurrencyAmountTests.cs`, incl. a
  max-uint256 case).
- `NearestUsableTick.Find` computes `round(tick / tickSpacing)` with integer arithmetic (floor-division
  plus round-half-toward-+∞) rather than upstream's floating-point `Math.round`, honouring the no-floating-point
  rule while matching the output to the digit. This also fixed two latent bugs: the guards used `Debug.Assert`
  (compiled out under `-c Release`, so `TICK_SPACING`/`TICK_BOUND` never threw) and `Math.Round` used C#'s
  default banker's rounding (so `Find(5, 10)` returned 0 instead of 10). Fixed test-first (`NearestUsableTickTests.cs`).
- `Utilities.ToHex` strips the sign nibble that `BigInteger.ToString("X")` prepends to a non-negative
  value whose top nibble is `>= 8` (e.g. `200 -> "0C8"`), matching JS `bigInt.toString(16)`'s minimal form.
  Without this the encoded `value` (msg.value) gained a spurious leading `00` byte for any amount with the
  high bit set. Latent because existing tests never routed such a value through `ToHex`; surfaced by the
  SwapRouter multi-trade ETH-in value sums (`0xc8`/`0xd0`). Fixed test-first (`ToHexTests.cs`).
- **sdk-core `sqrt`** (`Core/Utils/MathUtils.cs`): `MAX_SAFE_INTEGER` is `2^53 - 1` (JS `Number.MAX_SAFE_INTEGER`), not
  `long.MaxValue` (`2^63 - 1`). Above `2^53` a `double` loses integer precision, so the `Math.Sqrt` fast path
  must stop there and fall through to Newton's method (as upstream does); the old constant let the fast path
  run across `[2^53, 2^63)` and return a wrong `floor(sqrt)` for non-perfect-squares. Fixed test-first (`SqrtTests.cs`).
- **sdk-core `Token`** (`Core/Entities/Token.cs`): the FOT non-negative-fee invariant now throws `ArgumentException`
  ("NON-NEGATIVE FOT FEES"). It previously used `Debug.Assert`, compiled out under `-c Release`, so negative
  `buyFeeBps`/`sellFeeBps` were silently accepted. Fixed test-first (`TokenTests.Constructor_FailsWithNegativeFOTFees`).
- **sdk-core `computeZksyncCreate2Address`** (`Core/Utils/ZksyncAddressComputer.cs`): takes the **last 20 bytes**
  (`Skip(12).Take(20)`) of the keccak hash — the address — matching upstream's `keccak256(...).slice(26)` (a hex-string
  slice). It previously took the first 26 bytes, yielding a shifted, wrong address. Pinned test-first against the
  zkSync vector from `computePoolAddress.test.ts` (`ZksyncAddressComputerTests.cs`).
- **sdk-core `Fraction.ToFixed`/`ToSignificant`** (`Core/Entities/Fractions/Fraction.cs`) are now computed with
  exact `BigInteger` arithmetic instead of `double`/`decimal`/`Math.Pow`/`Math.Log10`. The old code took a
  floating-point path (against the no-float rule) and, worse, cast through `System.Decimal`, which overflows
  (~7.9e28) for large amounts — so formatting a large `CurrencyAmount`/`Price` threw. The new formatters match
  `big.js` (toFixed) and `decimal.js-light` (toSignificant) to the digit across all three rounding modes,
  negatives, and the significant-figure carry case. The `format` string parameter is retained for source
  compatibility but ignored (upstream's `toFormat` only sets an always-empty group separator). Expected values
  in `FractionFormattingTests.cs` were generated with the exact upstream libraries.
- _(append new entries as they arise)_

## 7. Re-syncing with upstream
Record the new upstream commit, `git -C /home/devops/uniswap-sdks-official log --oneline <old>..<new> -- sdks/v3-sdk sdks/sdk-core`,
port the deltas test-first, and bump the pinned commit above.

## 8. v4-sdk port (complete)
Reuses V3 concentrated-liquidity math; additive under `src/UniswapSharp/V4/` (tests under `test/UniswapSharp.Testing/V4/`).
Dependency-ordered, test-first phases:

| Upstream `sdks/v4-sdk/src/…` | UniswapSharp `src/UniswapSharp/V4/…` | Status |
|---|---|---|
| `internalConstants.ts` | `Constants.cs` (+ `PositionFunctions`) | ported |
| `actionConstants.ts` | `ActionConstants.cs` (`MSG_SENDER`) | ported |
| `utils/currencyMap.ts` | `Utils/CurrencyMap.cs` (`ToAddress`) | ported |
| `utils/sortsBefore.ts` | `Utils/CurrencyOrder.cs` (`SortsBefore`) | ported |
| `utils/calldata.ts` (`toHex`) | *(reuses V3 `Utils/Utilities.ToHex`)* | reused |
| `multicall.ts` | *(reuses V3 `Multicall.EncodeMulticall`)* | reused |
| `utils/hook.ts` | `Utils/Hook.cs` (`HookOptions`/`HookPermissions`) | ported — `V4/Utils/HookTests.cs` (46) |
| `utils/priceTickConversions.ts` | `Utils/PriceTick.cs` | ported — `V4/Utils/PriceTickTests.cs` (21, incl. native) |
| `entities/pool.ts` | `Entities/Pool.cs` (+ `PoolKey`/`PoolId`) | ported — `V4/Entities/PoolTests.cs` (29) |
| `utils/pathCurrency.ts`, `entities/route.ts` | `Utils/PathCurrency.cs`, `Entities/Route.cs` | ported — `V4/Entities/RouteTests.cs` (29) |
| `entities/position.ts` | `Entities/Position.cs` (+ `PermitTypes.cs`) | ported — `V4/Entities/PositionTests.cs` (3) |
| `utils/encodeRouteToPath.ts` | `Utils/EncodeRouteToPath.cs` (`PathKey[]`) | ported — `V4/Utils/EncodeRouteToPathTests.cs` (4) |
| `entities/trade.ts` | `Entities/Trade.cs` (+ `Swap.cs`/`RouteInput.cs`) | ported — `V4/Entities/TradeTests.cs` (67) |
| `utils/v4Planner.ts` | `Utils/V4Planner.cs` (`Actions` + `AbiParamEncoder`) | ported — `V4/Utils/V4PlannerTests.cs` (18) |
| `utils/v4PositionPlanner.ts` | `Utils/V4PositionPlanner.cs` | ported (via PositionManagerTests) |
| `PositionManager.ts` | `PositionManager.cs` (`V4PositionManager`) | ported — `V4/PositionManagerTests.cs` (20) |
| `utils/v4BaseActionsParser.ts` | `Utils/V4BaseActionsParser.cs` | ported — `V4/Utils/V4BaseActionsParserTests.cs` (19) |
| `utils/positionManagerAbi.ts` | *(n/a — encode via Nethereum selectors, not the ABI JSON)* | n/a |

## 9. Other monorepo packages (parity)
- **v2-sdk** → `src/UniswapSharp/V2/` (Constants, Errors, Pair [CREATE2 + 997/1000 fee math], Route, Trade, Router). Tests under `test/.../V2/` (92 cases). V2 slippage uses the linear `(1 ± slippage)` form; the V2 Router returns method-name + hex args (it does not ABI-encode calldata).
- **flashtestations-sdk** → `src/UniswapSharp/Flashtestations/` — **ported** (see section 12).
- **tamperproof-transactions** (EIP-7754) → `src/UniswapSharp/Tamperproof/` — **ported** (see section 13).
- **permit2-sdk** → `src/UniswapSharp/Permit2/` (Constants, Domain, `Eip712TypedDataEncoder` [ethers `_TypedDataEncoder` port, byte-exact], SignatureTransfer, AllowanceTransfer). 21 cases; 6 EIP-712 hash vectors matched to the digit. `providers/*` (on-chain reads) omitted.
- **smart-wallet-sdk** → `src/UniswapSharp/SmartWallet/` (Constants/ModeType, Types, CallPlanner/BatchedCallPlanner, SmartWallet [EncodeUserOp/EncodeBatchedCall/EncodeErc7821BatchedCall], Delegation.ParseFromCode). 50 cases; ERC-7821 mode words + `execute` selectors (`0x99e1d016`/`0xe9ae5c53`/`0x8dd7712f`) matched to the digit. `parseAuthorizationList*` (viem ECDSA recovery) omitted.

## 10. router-sdk port
Ported under `src/UniswapSharp/Router/` → namespace `UniswapSharp.Router` (tests under `test/.../Router/`).
Aggregates V2/V3/V4/mixed routes and encodes SwapRouter02 calldata. 196 xUnit cases; all calldata is
byte-verified against the upstream `.test.ts` expected values (multicall, exactInput(Single)/exactOutput(Single),
swapExactTokensForTokens/swapTokensForExactTokens, pull/sweep/unwrap/wrap, mint/increaseLiquidity).

| Upstream `sdks/router-sdk/src/…` | UniswapSharp `src/UniswapSharp/Router/…` | Tests ported | Status |
|---|---|---|---|
| `constants.ts` | `Constants.cs` | via callers | ported |
| `entities/protocol.ts` | `Entities/Protocol.cs` (`Protocol` enum) | via callers | ported |
| `utils/TPool.ts` | `Utils/TPool.cs` | via callers | ported |
| `utils/pathCurrency.ts` | `Utils/PathCurrency.cs` | Yes — `Router/Utils/PathCurrencyTests.cs` (1) | ported |
| `utils/encodeMixedRouteToPath.ts` | `Utils/EncodeMixedRouteToPath.cs` | Yes — `Router/Utils/EncodeMixedRouteToPathTests.cs` (20) | ported |
| `utils/index.ts` (`partitionMixedRouteByProtocol`, `getOutputOfPools`) | `Utils/MixedRouteUtils.cs` | Yes — in `MixedRouteTests.cs` | ported |
| `entities/route.ts` (`IRoute`, `RouteV2/V3/V4`, `MixedRoute`, `getPathToken`) | `Entities/Route.cs` | Yes — `Router/Entities/RouteTests.cs` (21) | ported |
| `entities/mixedRoute/route.ts` | `Entities/MixedRoute/MixedRouteSDK.cs` | Yes — `Router/Entities/MixedRoute/MixedRouteTests.cs` (51) | ported |
| `entities/mixedRoute/trade.ts` | `Entities/MixedRoute/MixedRouteTrade.cs` | Yes — `MixedRouteTradeTests.cs` (45) | ported |
| `entities/trade.ts` | `Entities/Trade.cs` | Yes — `Router/Entities/TradeTests.cs` (24) | ported |
| `multicallExtended.ts` | `MulticallExtended.cs` | Yes — `Router/MulticallExtendedTests.cs` (4) | ported |
| `paymentsExtended.ts` | `PaymentsExtended.cs` | Yes — `Router/PaymentsExtendedTests.cs` (10) | ported |
| `approveAndCall.ts` | `ApproveAndCall.cs` | via SwapRouter swap-and-add | ported |
| `swapRouter.ts` | `SwapRouter.cs` (`swapCallParameters` + `swapAndAddCallParameters`) | Yes — `Router/SwapRouterTests.cs` (32) | ported |

### Intentional divergences (router-sdk)
- **TPool union.** Upstream's `TPool = Pair | V3Pool | V4Pool` is a structural union; C# has none, so a mixed
  route holds its pools as `object` and `Utils/TPool.cs` provides the uniform accessors upstream reaches via
  `instanceof` (normalising the V2/V3 token-keyed and V4 currency-keyed surfaces onto `BaseCurrency`). V2/V3/V4
  price types (`Price<Token,Token>` vs `Price<BaseCurrency,BaseCurrency>`) are rebuilt to a common
  `Price<BaseCurrency,BaseCurrency>` since C# generics are invariant.
- **Route wrappers by composition.** Upstream `RouteV2/V3/V4` *extend* the SDK routes; C# can't multiply-inherit
  the invariant generic `Route<,>`, so the wrappers compose (`.V2Route`/`.V3Route`/`.V4Route`) and expose the
  `IRoute<TInput,TOutput>` surface. `MixedRoute` still inherits `MixedRouteSDK` (single base). `Trade.FromRoute`
  therefore also unwraps the wrappers before dispatching.
- **`SwapRouter.SwapCallParameters(object trades, …)`.** Accepts the heterogeneous `trades` argument upstream
  models with TS unions: a router `Trade`, a single V2/V3/mixed sub-trade, or a list of them. Polymorphic
  accessors dispatch over the four trade types. `swapCallParameters` value is `toHex(...)`; `swapAndAdd` value is
  the decimal `value.toString()` — matching upstream exactly.
- **ABI encoding.** Tuple/array/`bytes[]` calldata is built with the hand-rolled `V4/Utils/AbiParamEncoder`
  (Nethereum can't parse ethers tuple type strings) plus `V3/Utils/AbiFunctionEncoder` for selectors; every
  calldata hex is copied verbatim from the upstream tests and matches byte-for-byte. `MulticallExtended`
  `Validation` (BigintIsh | string) is modelled as `object?`.
- **Not ported:** upstream `inputTokenPermit`/`outputTokenPermit` self-permit options and `FeeOptions`-bearing
  swaps are wired through (`SelfPermit`/`Payments`) but not covered by a dedicated router test here (the permit
  encoders are already tested under `V3/SelfPermitTests`). The remaining `swapAndAddCallParameters` describe
  blocks (existing-position, the four approval-type variants, native in/out) are not ported — the single-hop,
  multi-hop and mixed-route swap-and-add paths are covered and byte-verified.

## 11. liquidity-launcher-sdk port
Ported under `src/UniswapSharp/LiquidityLauncher/` → namespace `UniswapSharp.LiquidityLauncher`
(config under `Config/`). Tests under `test/.../LiquidityLauncher/` — 44 xUnit cases, the direct 1:1
counterparts of the 9 upstream `.test.ts` files. Toolkit for launching tokens/auctions through the
Uniswap Liquidity Launcher stack (LiquidityLauncher + LBPStrategy + ContinuousClearingAuction +
TokenSplitter + uERC20/USUPERC20 factories). All poolIds, salts, CREATE2 addresses and encoded
calldata are matched to the upstream golden vectors byte-for-byte.

| Upstream `sdks/liquidity-launcher-sdk/src/…` | UniswapSharp `src/UniswapSharp/LiquidityLauncher/…` | Tests ported | Status |
|---|---|---|---|
| `chains.ts` | `Chains.cs` (`SupportedChainId`, `IsLaunchSupportedChain`) | via callers | ported |
| `errors.ts` | `Errors.cs` (`LauncherErrorCode`, `LauncherSdkError`, `IsLauncherSdkError`) | Yes — `ErrorsTests.cs` (3) | ported |
| `constants.ts` | `Constants.cs` | via callers | ported |
| `types.ts` | `Types.cs` (records) | via callers | ported |
| `addresses.ts` | `Addresses.cs` | Yes — `AddressesTests.cs` (13) | ported |
| `poolId.ts` | `PoolId.cs` (`ComputeLbpPoolId`, `ComputeGraffiti`) | Yes — `PoolIdTests.cs` (3) | ported |
| `salts.ts` | `Salts.cs` (`ComputeLauncherSalt`, `ComputeInitializerSalt`) | via callers | ported |
| `encode.ts` | `Encode.cs` | Yes — `EncodeTests.cs` (4) | ported |
| `build.ts` | `Build.cs` | Yes — `BuildTests.cs` (2) | ported |
| `lock.ts` | `Lock.cs` | Yes — `LockTests.cs` (5, incl. bytecode-hash pins) | ported |
| `lockRecipientBytecode.ts` | `LockRecipientBytecode.cs` (3 creation-bytecode constants) | pinned in `LockTests.cs` | ported |
| `format.ts` | `Format.cs` (`FormatFeePercent`, `FormatTokenAmount`) | via callers | ported |
| `config/blocks.ts` | `Config/Blocks.cs` | Yes — `Config/BlocksTests.cs` (4) | ported |
| `config/emission.ts` | `Config/Emission.cs` | Yes — `Config/EmissionTests.cs` (4) | ported |
| `config/price.ts` | `Config/Price.cs` | Yes — `Config/PriceTests.cs` (5) | ported |
| `config/fees.ts` | `Config/Fees.cs` (`FeeToTickSpacing`, `ResolvePoolFee`) | via callers | ported |
| `config/lpAllocation.ts` | `Config/LpAllocation.cs` | via callers | ported |
| `config/positions.ts` | `Config/Positions.cs` | via callers | ported |
| `abis.ts` | *(not ported as a surface)* | n/a | skipped |
| `reads.ts` | *(not ported)* | n/a | skipped |
| `availability.ts` | *(not ported)* | n/a | skipped |

### Skipped modules (liquidity-launcher-sdk)
- **`reads.ts`** — on-chain read descriptors + viem `PublicClient` helpers; no `.test.ts`, requires a
  live provider. Intentionally deferred for live verification later.
- **`availability.ts`** — fee-tier availability check that composes `reads.ts` against a live client;
  no `.test.ts`, depends on `reads.ts`. Deferred with it. Its pure inputs (`PoolId.ComputeLbpPoolId`,
  `Addresses.GetLauncherAddresses`) are ported.
- **`abis.ts`** — raw ABI JSON blobs for the read surface. Not ported as a standalone surface; the SDK
  encodes calldata via canonical function selectors (`V3/Utils/AbiFunctionEncoder`) + `AbiParamEncoder`
  rather than the ethers/viem ABI-JSON path, so the read ABIs are unused here.

### Intentional divergences (liquidity-launcher-sdk)
- **viem → Nethereum/AbiParamEncoder.** `keccak256` → `Nethereum.Util.Sha3Keccack`;
  `encodeAbiParameters` / `encodePacked` → the hand-rolled `V4/Utils/AbiParamEncoder` (byte-exact tuple/
  array/bytes coder); `getCreate2Address` → `Core/Utils/AddressValidator.GetCreate2Address`;
  `getAddress` (EIP-55 checksum) → `AddressValidator.GetAddress`. `encodeFunctionData` is the selector
  (`AbiFunctionEncoder.Selector`) + `AbiParamEncoder` args. Solidity `string` params are encoded as their
  UTF-8 bytes through the `bytes` coder (identical ABI layout) since `AbiParamEncoder` treats a `string`
  type's value as hex.
- **Floating point in the config helpers is preserved deliberately.** `config/emission.ts` (the convexity
  curve `Math.pow(i/rampSteps, 1/alpha)`), `config/blocks.ts` `timeToBlock` (`Math.round(Δseconds /
  blockTimeSeconds)` with sub-second L2 cadences), and the mps/tick rounding in `config/fees.ts`,
  `config/lpAllocation.ts` and `config/positions.ts` are IEEE-754 `number` math in the reference. These
  are UI-config helpers whose block ranges and mps weights the on-chain contracts validate with tolerance,
  not exact-integer protocol math. Matching upstream **to the digit** therefore requires mirroring the same
  `double` arithmetic, including a JS-compatible round (`Config/MathJs.Round` = `floor(x + 0.5)`, since
  JS `Math.round` rounds half toward +∞ whereas C#'s default is banker's rounding). The exact-integer rule
  is kept for all bigint math (poolId/salt/CREATE2/calldata/price/lpAllocation thresholds). Parity note:
  cross-platform `Math.Pow` may differ by ≤1 ULP from V8, but the emission algorithm clamps boundaries and
  absorbs the remainder into the final block, so the invariants (mps sum = `MPS_TOTAL`, contiguous window,
  large final block) hold regardless — the upstream `emission.test.ts` pins only those invariants, and no
  golden per-step vector exists.
- **`Lock` naming.** The class mirrors upstream `lock.ts` → `Lock`, which collides with .NET 9+
  `System.Threading.Lock` when both namespaces are imported; consumers/tests alias it (`using Lock =
  UniswapSharp.LiquidityLauncher.Lock;`).
- **`errors.test.ts` cjs/esm case omitted.** The structural-lookalike test (recognizing a
  `LauncherSdkError` duplicated across a dual cjs/esm install) has no analogue in a single-assembly C#
  port; `IsLauncherSdkError` reduces to a type test and the other two cases are ported.

## 12. flashtestations-sdk port
Ported under `src/UniswapSharp/Flashtestations/` → namespace `UniswapSharp.Flashtestations` (subfolders
`Types/`, `Crypto/`, `Config/`, `Rpc/`, `Verification/`; tests under `test/.../Flashtestations/`). Verifies
whether a Unichain block was built by a specific TEE workload: the deterministic core is
`Workload.ComputeWorkloadId` = `keccak256` over the concatenated TDX measurement registers; the rest is
EVM JSON-RPC I/O to fetch a block and parse its last transaction's `BlockBuilderProofVerified` event.
**103 xUnit cases** (all four upstream `test/**` files ported 1:1); the workload IDs are matched to the digit
against the upstream vectors (incl. the `0x952569f6…` value shared with the flashbots Solidity test).

| Upstream `sdks/flashtestations-sdk/src/…` | UniswapSharp `Flashtestations/…` | Tests ported | Status |
|---|---|---|---|
| `types/index.ts` | `Types/*.cs` (`MeasurementRegisters`, `VerificationTypes`, `ChainTypes`, `BlockParameter`, `Errors`) | via callers | ported |
| `types/validation.ts` | `Types/Validation.cs` | Yes — via `WorkloadTests.cs` | ported |
| `crypto/workload.ts` | `Crypto/Workload.cs` | Yes — `WorkloadTests.cs` (28) | ported |
| `config/chains.ts` | `Config/Chains.cs` | Yes — `ChainsTests.cs` (29) | ported |
| `rpc/abi.ts` | `Rpc/FlashtestationAbi.cs` | via callers | ported |
| `rpc/client.ts` | `Rpc/RpcClient.cs` (+ `Rpc/EvmRpc.cs`, `Rpc/NethereumEvmRpcClient.cs`) | Yes — `RpcClientTests.cs` (28) | ported |
| `verification/service.ts` | `Verification/Service.cs` (`FlashtestationService`) | Yes — `ServiceTests.cs` (18) | ported |
| `cli/**` (`commander`) | *(deferred — console wiring, no upstream tests)* | n/a | deferred |

### Intentional divergences (flashtestations-sdk)
- **Injectable RPC instead of viem.** viem's `createPublicClient` (`getBlock`/`getTransactionReceipt`/
  `readContract`) plus the standalone `parseEventLogs` are abstracted behind `IEvmRpcClient` +
  `IEvmRpcClientFactory` (low-level), and `RpcClient` behind `IRpcClient` + `IRpcClientFactory` (high-level).
  Tests inject fakes (mirroring the bun `mock.module('viem', …)` / `jest.spyOn(rpcClientModule,'RpcClient')`),
  so **no test touches the network**. The retry/backoff, connection cache (`RpcClient.ClearCache`, keyed
  `chainId:rpcUrl`) and error-wrapping (`NetworkError`/`BlockNotFoundError`) are ported faithfully.
- **Live implementation deferred.** A default `NethereumEvmRpcClient` (wraps `Nethereum.Web3`) is provided so
  the SDK can run against a real node, but it is **not exercised by tests** — live verification is deferred.
  Nethereum 6.1.0 has no dedicated `safe`/`finalized` block tag, so those fall back to `latest` in that path.
- **`string | string[]` registers.** The upstream `mrtd` / `rtmr0` union is modelled by a `HexValues` struct
  with implicit `string` / `string[]` conversions and an `IsArray` flag (mirroring `Array.isArray`).
- **Pure deps used for real, not mocked.** The service test mocks `computeAllWorkloadIds` and
  `getBlockExplorerUrl` in TS; in C# these deterministic functions are called for real (the tests compute the
  expected workload IDs from the register vectors). The one "empty explorer URL" case uses a real chain
  (Unichain Alphanet, whose configured explorer URL is empty) rather than mocking the lookup.
- **Type-enforced singular guard.** The two upstream tests that pass an *array* to the singular
  `computeWorkloadId` (via a TS `as any` cast) can't be expressed in C# — the singular type's `Mrtd`/`Rtmr0`
  are `string`. The runtime guard (`"mrtd/rtmr0 must be a single value, not an array"`) is instead exercised
  directly through `Validation.ValidateSingularWorkloadMeasurementRegisters(WorkloadMeasurementRegisters)`.
- **CLI deferred.** `src/cli/**` (commander arg-parsing + console output) has no upstream tests and is a
  console-app concern; it is intentionally not ported. Its pure helpers (`resolveChainConfig`, error-code
  mapping) can be added later behind the same tested library surface if a .NET CLI is desired.

## 13. tamperproof-transactions port (EIP-7754)
Ported under `src/UniswapSharp/Tamperproof/` → namespace `UniswapSharp.Tamperproof` (tests under
`test/.../Tamperproof/`, 137 xUnit cases). EIP-7754 "TWIST": a dApp signs its calldata; a wallet verifies the
signature against a public key published in the site's DNS TXT record (fetched over DNS-over-HTTPS) and/or an
HTTPS manifest. Built on Web Crypto JWS algorithms — **not** Ethereum secp256k1 — so this maps to
`System.Security.Cryptography` (+ BouncyCastle for Ed25519), not Nethereum.

| Upstream `sdks/tamperproof-transactions/src/…` | UniswapSharp `src/UniswapSharp/Tamperproof/…` | Tests ported | Status |
|---|---|---|---|
| `constants/errors.ts` | `Constants/Errors.cs` (+ `TamperproofException`) | via callers (message-asserted) | ported |
| `utils/hex.ts` | `Utils/Hex.cs` (`FromHex`/`ToHex`/`NormalizeHex`/`FromBase64`) | Yes — `HexTests.cs` (34) | ported |
| `utils/canonicalJson.ts` | `Utils/CanonicalJson.cs` (`CanonicalStringify`/`SerializeRequestPayload`) | Yes — `CanonicalJsonTests.cs` (7) | ported |
| `utils/txtRecord.ts` | `Utils/TxtRecord.cs` (`ParseTxtRecord`/`ProcessTxtRecordData`) | Yes — `TxtRecordTests.cs` (19) | ported |
| `algorithms.ts` | `Algorithms.cs` (+ `Utils/SigningCrypto.cs`) | via sign/verify | ported |
| `sign.ts` | `Sign.cs` (`Signer.Sign`) | Yes — `SignTests.cs` (19) | ported |
| `verify.ts` | `Verify.cs` (`Verifier`) + `IDohResolver.cs` / `IManifestFetcher.cs` | Yes — `VerifyTests.cs` (34) | ported |
| `generate.ts` | `Generate.cs` (`Generator.Generate`, `PublicKey`) | Yes — `GenerateTests.cs` (24) | ported |
| `utils/webcrypto.ts`, `utils/crypto-browser-shim.ts` | *(n/a — Node/browser WebCrypto shim)* | n/a | omitted |

Algorithm mapping (`algorithms.ts` → `System.Security.Cryptography`): **RS256/384/512** → RSA + PKCS#1 v1.5 +
SHA-256/384/512; **PS256/384/512** → RSA-PSS + SHA-256/384/512 (salt length = hash length, .NET's `Pss`
default); **ES256/384/512** → ECDsa on P-256/P-384/**P-521** with raw `r||s` signatures via
`DSASignatureFormat.IeeeP1363FixedFieldConcatenation`; **EdDSA** → Ed25519.

### Ed25519 dependency decision
`System.Security.Cryptography` has **no managed Ed25519** in net10.0, so EdDSA sign/verify use
**`BouncyCastle.Cryptography` 2.5.1** (pure-managed, already present transitively via Nethereum), added as an
explicit `<PackageReference>` in `src/UniswapSharp/UniswapSharp.csproj`. RSA and ECDSA stay on SSC. The
deterministic Ed25519 vector from `sign.test.ts` matches byte-for-byte.

### Intentional divergences (tamperproof-transactions)
- **Injectable DNS/HTTPS.** Upstream's `dohjs` `DohResolver` and global `fetch` are replaced with the
  `IDohResolver` / `IManifestFetcher` interfaces (upstream already parameterizes the resolver via
  `thisResolver`). Tests inject fakes exactly as `verify.test.ts` mocks the globals. A production
  `HttpManifestFetcher` (redirects rejected, `Accept: application/json`, timeout via `CancellationToken`) is
  provided; **no live DoH resolver is bundled** (live TXT resolution is deferred — callers inject one).
- **Web Crypto shim omitted.** `utils/webcrypto.ts` / `utils/crypto-browser-shim.ts` resolve Web Crypto in
  Node vs browser; .NET uses `System.Security.Cryptography` directly, so the shim has no analog.
- **Synchronous sign/verify.** `Signer.Sign` and `Verifier.Verify` are synchronous (SSC has no async subtle
  API); `Verifier.VerifyAsyncJson`/`VerifyAsyncDns` stay async for the injected I/O. `Verify` takes the SPKI
  public-key bytes directly (imported internally) rather than a pre-imported WebCrypto `CryptoKey`.
- **encodeURIComponent.** The TWIST path is percent-encoded via `Uri.EscapeDataString` with `!'()*` restored,
  matching `encodeURIComponent` exactly (`Uri.EscapeDataString` otherwise escapes those five). The sanitize
  vector (`//api v1/ƙeys?bad#frag` → `https://example.com/api%20v1/%C6%99eys%3Fbad%23frag`) matches to the byte.
- **Empty-TWIST truthiness.** The "first/multiple TWIST" and "no prefix" guards use `string.IsNullOrEmpty`,
  mirroring JS truthiness (an empty `TWIST=` value is treated as not-found), not C# `is null`.
- **`fromBase64` platform branch / `processTxtRecordData` fallback.** `Hex.FromBase64` always uses
  `Convert.FromBase64String` (the JS `atob`-vs-`Buffer` branch and its `ERROR_NO_BASE64_DECODER` path are a
  browser/Node shim with no .NET analog). `ProcessTxtRecordData`'s non-string/non-bytes fallback uses
  `.ToString()`; the upstream `String(false)` → `"false"` case (C# `bool.ToString()` is `"False"`) never
  occurs in the real DoH flow and is not asserted.

## 14. uniswapx-sdk port
Ported under `src/UniswapSharp/UniswapX/` → namespace `UniswapSharp.UniswapX` (tests under
`test/.../UniswapX/`, ~180 xUnit cases). Off-chain UniswapX orders: EIP-712 witness hashing +
`serialize()`/`parse()`, decay math, order builders and high-level trades. The permit2 witness
hashing reuses `Permit2.SignatureTransfer` + `Permit2.Eip712TypedDataEncoder`; all ABI
tuple/`bytes` encoding reuses `V4/Utils/AbiParamEncoder`.

| Upstream `sdks/uniswapx-sdk/src/…` | UniswapSharp `src/UniswapSharp/UniswapX/…` | Tests ported | Status |
|---|---|---|---|
| `constants.ts` (+ test), `constants/v4.ts` | `Constants.cs`, `ConstantsV4.cs` | Yes — `ConstantsTests.cs` (10) | ported |
| `errors.ts` | `Errors.cs` (`MissingConfiguration`) | via callers | ported |
| `utils/dutchDecay.ts` (+ test) | `Utils/DutchDecay.cs` | Yes — `DutchDecayTests.cs` (6) | ported |
| `utils/dutchBlockDecay.ts` (+ test) | `Utils/DutchBlockDecay.cs` (`NonLinearDutchDecayLib`) | Yes — `DutchBlockDecayTests.cs` (13) | ported |
| `order/types.ts`, `order/v4/types.ts` | `Order/Types.cs`, `Order/V4/Types.cs` | via callers | ported |
| `order/validation.ts` (+ test) | `Order/Validation.cs` | Yes — `ValidationTests.cs` (4) | ported |
| `order/DutchOrder.ts` (+ test) | `Order/DutchOrder.cs` | Yes — `DutchOrderTests.cs` (10) | ported |
| `order/RelayOrder.ts` (+ test) | `Order/RelayOrder.cs` (batch permit witness) | Yes — `RelayOrderTests.cs` (6) | ported |
| `order/V2DutchOrder.ts` (+ test) | `Order/V2DutchOrder.cs` (Unsigned/Cosigned) | Yes — `V2DutchOrderTests.cs` (7) | ported |
| `order/V3DutchOrder.ts` (+ test) | `Order/V3DutchOrder.cs` (nonlinear block decay) | Yes — `V3DutchOrderTests.cs` (9) | ported |
| `order/PriorityOrder.ts` (+ test) | `Order/PriorityOrder.cs` | Yes — `PriorityOrderTests.cs` (4) | ported |
| `order/v4/HybridOrder.ts` (+ test), `order/v4/hashing.ts` | `Order/V4/HybridOrder.cs`, `Order/V4/Hashing.cs` | Yes — `HybridOrderTests.cs` (11) | ported |
| `utils/order.ts` (+ test) | `Utils/OrderUtils.cs` (free fns) + `Utils/OrderParser.cs` (`UniswapXOrderParser`/`RelayOrderParser`) | Yes — `OrderUtilsTests.cs` (34) | ported |
| `builder/OrderBuilder.ts` + `builder/{Dutch,Relay,V2Dutch,V3Dutch,Priority,Hybrid}OrderBuilder.ts` | `Builder/*.cs` | Yes — via `OrderUtilsTests.cs` (build→serialize→parse round-trips for all 6) | ported |
| `trade/utils.ts` + `trade/{Dutch,V2Dutch,V3Dutch,Priority,Relay,Hybrid}OrderTrade.ts` | `Trade/*.cs` | Yes — `{Dutch,V2Dutch,V3Dutch,Priority,Relay}OrderTradeTests.cs` (33) | ported |
| `utils/NonceManager.ts` (+ test) | `Utils/NonceManager.cs` (pure fns + injectable `INonceLookup`) | Yes — `NonceManagerTests.cs` (28) | ported |

### Skipped (uniswapx-sdk)
- **`contracts/**`** (40+ TypeChain-generated bindings + `__factory` tree) — ethers codegen; **not ported**.
  Where a reactor/permit ABI is needed it is encoded directly with `AbiParamEncoder`/Nethereum from the
  canonical order-struct types.
- **Network-only utils with no `.test.ts`** — `utils/EventWatcher.ts`, `utils/OrderQuoter.ts`,
  `utils/OrderValidator.ts`, `utils/PermissionedTokenValidator.ts`, `utils/multicall.ts` — need live
  providers/contracts and have no deterministic vectors; **intentionally deferred** (live-verification later).
  The small deterministic `ResolvedUniswapXOrder`/`ResolvedRelayOrder` shapes that `order.resolve()` returns
  were lifted from `OrderQuoter.ts` into `Order/Types.cs`.
- **DCA-intent hashing** in `order/v4/hashing.ts` (`hashDCAIntent`, `hashPrivateIntent`, `hashDCACosignerData`,
  `hashOutputAllocation(s)`, `hashFeedInfo(Array)`, `DCA_INTENT_TYPES`) — there is no DCA order class or
  `.test.ts`, so these are **deferred**. The hybrid-order hashing and the re-exported `ORDER_INFO_V4_TYPE_HASH`
  / `DCA_COSIGNER_DATA_TYPE_HASH` constants **are** ported.
- **HybridOrderTrade** class is ported (`Trade/HybridOrderTrade.cs`) but has no upstream `.test.ts` (none exists);
  covered indirectly by the `HybridOrder` + resolve tests.

### Intentional divergences (uniswapx-sdk)
- **Order-hash form.** `order.hash()` is ethers `_TypedDataEncoder.from(TYPES).hash(value)` =
  `keccak256(encodeData(primaryType, value))`; exposed as `Eip712TypedDataEncoder.HashStruct`/`HashStructHex`
  (added to the permit2 encoder). No upstream test ships a golden order-hash hex — parity is structural
  (upstream type strings + encodeData layout) plus sign→recover round-trips for every signed/cosigned path.
- **Signature recovery.** `getSigner` and `recoverCosigner` use Nethereum `EthECKey`: raw digest recovery for
  the permit signer and for V3/Hybrid cosignatures (`ethers.recoverAddress`), EIP-191 personal-message recovery
  (`EthereumMessageSigner`) for V2/Priority cosignatures (`ethers.verifyMessage`).
- **ABI `tuple(...)` spelling.** `V4/Utils/AbiTypeParser` now also accepts ethers' `tuple(...)` prefix (additive;
  existing bare-paren `(...)` usage in V4/Router is unaffected), so order ABI strings are ported verbatim.
- **Builders.** C# can't reproduce the TS covariant fluent-return through an abstract `OrderBuilder`, so each of
  the six builders is self-contained (it inlines the small shared `OrderInfo` validate/build helper) rather than
  inheriting a base class. Behaviour and invariant messages match upstream.
- **Trades.** The TS phantom `TTradeType` generic is dropped (`TradeType` is a field); the two remaining generics
  are constrained to sdk-core `BaseCurrency`. The two reflection-based `getExpectedAmountIn/Out`-throw tests in
  `V3DutchOrderTrade.test.ts` are omitted (private methods); the public fallback behaviour is covered.
- **Mutable order info.** Order `info` types are records with `get; set;` + `List<>` so upstream in-place
  mutations (`info.outputs.push(...)`, `info.universalRouterCalldata = ...`) and builder mutation work; test
  equality uses AwesomeAssertions `BeEquivalentTo` (deep) rather than record `Equals`.
- **`REVERSE_REACTOR_MAPPING` / `REVERSE_RESOLVER_MAPPING`** are mutable `Dictionary` statics (mirroring the JS
  objects the tests patch) and are built iterating chain ids in ascending-numeric order to match JS integer-key
  iteration.

## 15. universal-router-sdk port (the capstone package)
Ported under `src/UniswapSharp/UniversalRouter/` → namespace `UniswapSharp.UniversalRouter` (tests under
`test/.../UniversalRouter/`). Encodes Universal Router command calldata (V2/V3/V4/mixed swaps, wrap/unwrap,
fees, Permit2 ingress, Across bridge, SwapProxy) and signed-route EIP-712 payloads, building on
`UniswapSharp.Router` (aggregated `Trade`), `V4/Utils/V4Planner`, and `AbiParamEncoder`. **142 xUnit cases**;
all command bytes, per-command ABI inputs, `execute`/`executeSigned`/proxy calldata, and the Permit2
signature-sanitization round-trip match upstream byte-for-byte.

| Upstream `sdks/universal-router-sdk/src/…` | UniswapSharp `src/UniswapSharp/UniversalRouter/…` | Tests ported | Status |
|---|---|---|---|
| `utils/constants.ts` | `Utils/Constants.cs` (`UniversalRouterVersion`, `CHAIN_CONFIGS`, addr/block lookups) | Yes — `Utils/ConstantsTests.cs` (13) | ported |
| `utils/numbers.ts` | `Utils/Numbers.cs` (`EncodeFeeBips`/`EncodeFee1e18`) | Yes — via `FeeEncodingTests.cs` | ported |
| `utils/getCurrencyAddress.ts` | `Utils/GetCurrencyAddress.cs` | via callers | ported |
| `utils/pathCurrency.ts` | `Utils/PathCurrency.cs` | via callers | ported |
| `utils/toV4URVersion.ts` | `Utils/ToV4URVersion.cs` | Yes — `Utils/ToV4URVersionTests.cs` (5) | ported |
| `utils/routerCommands.ts` | `Utils/RoutePlanner.cs` (`CommandType`, `COMMAND_DEFINITION`, `V2V3_SWAP_COMMANDS_V2_1_1`, `RoutePlanner`, `CreateCommand`) | Yes — `CommandParserTests`, `PerHopSlippageTests` | ported |
| `entities/Command.ts` | `Entities/Command.cs` (`ICommand`, `TradeConfig`, `RouterActionType`) | via callers | ported |
| `entities/actions/uniswap.ts` | `Entities/Actions/UniswapTrade.cs` (+ `Uniswap.cs`: `SwapOptions`, `TokenTransferMode`, `FlatFeeOptions`) | Yes — feeEncoding/swapProxy/nativeErc20/perHop | ported |
| `entities/actions/unwrapWETH.ts` | *(UnwrapWETH command action — modeled via `EncodeInputTokenOptions`)* | indirect | ported |
| `entities/actions/across.ts` | `Entities/Actions/Across.cs` (`AcrossV4DepositV3Params`) | Yes — `AcrossTests.cs` (5) | ported |
| `utils/inputTokens.ts` | `Utils/InputTokens.cs` (`Permit2Permit`, `EncodePermit`, `EncodeInputTokenOptions`) + `Utils/Signatures.cs` | Yes — `Permit2Tests.cs` (4) | ported |
| `types/encodeSwaps.ts` | `Types/EncodeSwaps.cs` (`SwapSpecification`, `SwapStep`/`V4Action` unions, `Fee`) | Yes — `EncodeSwapsTests.cs` | ported |
| `utils/encodeV4Action.ts` | `Utils/EncodeV4Action.cs` | Yes — via `EncodeSwapsTests` | ported |
| `utils/encodeSwapStep.ts` | `Utils/EncodeSwapStep.cs` | Yes — `EncodeSwapsTests.cs` | ported |
| `utils/computeEncodeSwapsAmounts.ts` | `Utils/ComputeEncodeSwapsAmounts.cs` | Yes — via `EncodeSwapsTests` | ported |
| `utils/normalizeEncodeSwapsSpec.ts` | `Utils/NormalizeEncodeSwapsSpec.cs` | Yes — via `EncodeSwapsTests` | ported |
| `utils/validateEncodeSwaps.ts` | `Utils/ValidateEncodeSwaps.cs` | Yes — `EncodeSwapsTests.cs` (~20 rejections) | ported |
| `utils/routerTradeAdapter.ts` | `Utils/RouterTradeAdapter.cs` | Yes — `RouterTradeAdapterTests.cs` (10) | ported |
| `utils/eip712.ts` | `Utils/Eip712.cs` (`EXECUTE_SIGNED_TYPES`, domain, nonce) | Yes — `SignedRoutesTests.cs` (7) | ported |
| `utils/commandParser.ts` | `Utils/CommandParser.cs` (`CommandParser`/`GenericCommandParser`, V3-path subparsers) | Yes — `CommandParserTests` + `CommandParserV4Tests` | ported |
| `swapRouter.ts` | `SwapRouter.cs` (+ `SwapRouterEncodeSwaps.cs`, `SwapRouterSwapCallParameters.cs`) — `swapCallParameters`, `encodeSwaps`, `getExecuteSignedPayload`/`encodeExecuteSigned`, proxy plan | Yes — many | ported (see divergences) |

### Modules skipped (and why)
- **`src/contracts/**`** — TypeChain-generated bindings/factories. Not ported; `execute`/`executeSigned`/proxy
  `execute` and every command's inputs are encoded directly via `AbiFunctionEncoder` (keccak selector) +
  `AbiParamEncoder` (ethers `defaultAbiCoder` equivalent), matching the upstream ABI byte-for-byte.
- **The Foundry/`test:forge` Solidity suite** (`test/forge/*.t.sol`, `writeInterop.ts`, `MigratorCallParameters.t.sol`,
  `SwapERC20CallParameters.t.sol`) — exercises on-chain contracts, not the SDK. `test/forge/interop.json` holds
  golden calldata the TS `uniswapTrades.test.ts` *registers* (its success-path assertion `compareUniswapTrades`
  is a no-op; only `methodParameters.value` is asserted in TS).
- **`swapRouter.ts` `migrateV3ToV4CallParameters`** — covered only by the skipped Foundry `MigratorCallParameters`
  suite; not ported. (All other `swapRouter.ts` surface is ported.)
- **Fork-dependent `uniswapTrades.test.ts` cases** (v2/v3/mixed/multi-route/fees describe blocks) — these build
  trades from `getUniswapPools()`, which RPC-fetches mainnet pool reserves/liquidity at a fork block; those pool
  states aren't available offline, so the exact amounts (and therefore calldata) can't be reproduced. The
  **static-pool** surface of the same file *is* ported and byte-verified via the dedicated unit tests
  (`FeeEncodingTests`, `SwapProxyTests`, `NativeErc20InputTests`, `PerHopSlippageTests`, `RouterTradeAdapterTests`),
  which cover V2/V3/V4/mixed swap encoding, fees, native/wrapped-native in/out, per-hop slippage, and the adapter.

### Intentional divergences (universal-router-sdk)
- **`SwapCallParameters(RouterTrade, …)`.** Takes the router-sdk aggregated `Trade<BaseCurrency,BaseCurrency>`
  directly (upstream's `RouterTrade<Currency,Currency,TradeType>`); the `value` field is `toHex(...)` and matches
  upstream's `BigNumber.toHexString()` for all non-negative amounts.
- **Union types.** TS discriminated unions (`SwapStep`, `V4Action`, `Fee`, `PoolInRoute`) become abstract-record
  hierarchies; `switch`-on-type replaces the `.type`/`.kind`/`.action` string discriminators.
- **`UniversalRouterVersion`** is an int-backed enum (ordering matches the upstream numeric-string
  `localeCompare`); `IsAtLeastV2_1_1` and `ToV4URVersion` resolve by the version's string value, so an unknown
  version (`(UniversalRouterVersion)999`) throws "No v4-sdk URVersion mapping" exactly as the TS `'9.9.9'` case.
- **Signature sanitization.** `Utils/Signatures.cs` ports ethers' `splitSignature`/`joinSignature` (EIP-2098
  compact + `v`-as-recovery-id normalization) used by `EncodePermit`; the Permit2 tests sign with Nethereum's
  `EthECKey.SignAndCalculateV` and confirm the canonical round-trip byte-for-byte against a real signature.
- **`AbiParamEncoder.ToBigInteger`** now also accepts `0x`-prefixed hex strings (ethers `BigNumberish`
  compatibility) so fee hex (`encodeFee1e18`) and other hex amounts encode without a decimal-only parse.
- **`ToV4URVersion`/`EncodeV4Action`** produce the V4 planner's *positional* action tuples (C# `AbiParamEncoder`
  is positional; ethers matches struct object fields by name) — the encoded bytes are identical.
