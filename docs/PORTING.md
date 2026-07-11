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

## 8. v4-sdk port (in progress)
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
| `entities/position.ts` | `Entities/Position.cs` | pending (P6) |
| `utils/encodeRouteToPath.ts` | `Utils/EncodeRouteToPath.cs` (`PathKey[]`) | pending (P7) |
| `entities/trade.ts` | `Entities/Trade.cs` | pending (P8) |
| `utils/v4Planner.ts` | `Utils/V4Planner.cs` (`Actions` + tuple ABI encoder) | pending (P9) |
| `utils/v4PositionPlanner.ts` | `Utils/V4PositionPlanner.cs` | pending (P10) |
| `PositionManager.ts` | `V4PositionManager.cs` | pending (P11) |
| `utils/v4BaseActionsParser.ts` | `Utils/V4BaseActionsParser.cs` | pending (P12) |
| `utils/positionManagerAbi.ts` | *(n/a — encode via Nethereum selectors, not the ABI JSON)* | n/a |
