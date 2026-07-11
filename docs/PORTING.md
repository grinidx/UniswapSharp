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
| `swapRouter.ts` | `SwapRouter.cs` | Yes — `V3/SwapRouterTests.cs` (12 cases, single-trade block) | ported |
| `utils/calldata.ts` | `Utils/Utilities.cs` (`ToHex`) | No dedicated test file | ported |
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
- _(append new entries as they arise)_

## 7. Re-syncing with upstream
Record the new upstream commit, `git -C /home/devops/uniswap-sdks-official log --oneline <old>..<new> -- sdks/v3-sdk sdks/sdk-core`,
port the deltas test-first, and bump the pinned commit above.
