# UniswapSharp

A C# / .NET port of the official Uniswap SDK - the `sdk-core` and `v3-sdk` TypeScript
packages from [Uniswap/sdks](https://github.com/Uniswap/sdks). It lets .NET applications
work with Uniswap V3: model currencies, tokens, pools, positions, routes and trades;
run the tick, sqrt-price, swap and liquidity math; and encode transaction calldata for
the V3 periphery contracts.

## Project status

- Target framework: **.NET 10** (`net10.0`)
- V3 core (entities + math) is implemented and unit-tested
- 194 xUnit v3 tests; all passing (see Outstanding work)
- A handful of calldata / action-builder methods remain stubbed with `NotImplementedException`
- Not yet packaged or published to NuGet

## Layout

```
src/UniswapSharp/
  Core/            # port of @uniswap/sdk-core
    Entities/        # Token, Ether, NativeCurrency, Fractions (Fraction, Percent, Price, CurrencyAmount)
    Utils/           # AddressValidator, MathUtils, PriceImpact, RlpEncoder, SortedInsert, ...
  V3/              # port of @uniswap/v3-sdk
    Entities/        # Pool, Position, Route, Trade, Tick, TickListDataProvider, ...
    Utils/           # TickMath, SqrtPriceMath, SwapMath, FullMath, LiquidityMath, ...
    SwapRouter, SwapQuoter, NonfungiblePositionManager, Payments, Multicall, Staker, SelfPermit
test/UniswapSharp.Testing/   # xUnit + AwesomeAssertions, mirrors the src tree
UniswapSharp.sln
```

Namespaces and file names deliberately mirror the upstream TypeScript so the two can be
read side by side.

## Build and test

```bash
dotnet build -c Release
dotnet test  -c Release
```

The projects target `net10.0` and require the .NET 10 SDK; no `DOTNET_ROLL_FORWARD` is needed.

CI (`.github/workflows/ci.yml`, which calls the reusable `_test.yml`) restores, builds in
Release, and runs the tests on ubuntu/windows/macos — publishing a PR test-result check, a
coverage comment, and a `$GITHUB_STEP_SUMMARY` table. CodeQL runs via `codeql.yml`.

## Dependencies

- **Nethereum** 6.1.0 (`Nethereum.ABI`, `.Contracts`, `.Util`, `.Web3`) - ABI encoding,
  contract calls, address / keccak utilities. An explicit `Newtonsoft.Json` 13.0.4 pin overrides
  the vulnerable 11.0.2 that `Nethereum.Hex` still drags in transitively (NU1903).
- **ExtendedNumerics.BigRational** 3000.0.2.132 - exact rational arithmetic for the fraction and price types
- **xUnit v3** + **AwesomeAssertions** (test project only)

## Porting methodology

This is a line-for-line port of the upstream SDK. When implementing or fixing anything:

1. **Find the upstream source of truth first.** The reference is the official monorepo
   [Uniswap/sdks](https://github.com/Uniswap/sdks), packages `sdk-core` and `v3-sdk`.
   Read the matching `.ts` file before writing C#.
2. **Port the tests too.** Every upstream module has a `.test.ts` beside it. Port those
   cases to xUnit so behaviour is verified against the reference, not assumed.
3. **Match numeric behaviour to the digit.** Rounding, tick boundaries and fixed-point
   math must agree with upstream exactly. Prefer `BigInteger` / `BigRational` over `double`;
   never use floating point in protocol math.
4. **Keep the suite green.** Run the tests after each change; do not add code without a
   test that pins its behaviour.

File mapping for the outstanding stubs (paths relative to `sdks/v3-sdk/src/` upstream):

| C# file | Upstream reference |
|---|---|
| `V3/SwapQuoter.cs` | `quoter.ts` (+ `quoter.test.ts`) |
| `V3/NonfungiblePositionManager.cs` | `nonfungiblePositionManager.ts` |
| `V3/Payments.cs` | `payments.ts` |
| `V3/Utils/PositionLibrary.cs` (`SubIn256`) | `utils/tickLibrary.ts` (`subIn256`) |
| `V3/Utils/PriceTick.cs` | `utils/priceTickConversions.ts` |

## Conventions

- Nullable reference types and implicit usings are enabled; keep new code warning-clean.
- Public types mirror upstream names (PascalCase equivalents of the TS exports).
- Amounts and prices flow through the `Fraction` types - keep floating point out of protocol math.
- Work on a branch, keep commits small, and run the tests before each commit.

## Software-engineering process (required)

- **Always use PRs.** Branch off `main` (`feat/…`, `fix/…`, `chore/…`, `docs/…`, `ci/…`, `test/…`),
  small Conventional-Commit commits, PR into `main`, CI green, squash-merge, linear history.
  Never push directly to `main`.
- **Test-first, always.** Port/write the failing test before the implementation. Keep the suite
  green (`dotnet test -c Release`).
- **Match upstream to the digit.** `BigInteger`/`BigRational` only; never floating point in
  protocol math. See [docs/PORTING.md](docs/PORTING.md) for the mapping + methodology.
- **Definition of done (ported module):** upstream `.ts` + `.test.ts` ported and green;
  `docs/PORTING.md` row updated; `dotnet format --verify-no-changes` clean; CI green.
- Full contributor guide: [CONTRIBUTING.md](CONTRIBUTING.md).

## Outstanding work

Phase A (repository foundations) is complete: .NET 10, CI with PR reporting/coverage/CodeQL/Dependabot,
branch protection + security hardening, community-health files, docs, and NuGet packaging are all in
place. Remaining work is **Phase B** (V3 feature-parity port) and beyond.

1. **Phase B — first pass: dependency-update sweep (test-verified)** - DONE. All six deferred major
   bumps landed, each behind a green suite (194/194) and with numeric parity verified to the digit for
   the runtime deps:
   - **Runtime (protocol-math critical):** `Nethereum.ABI` / `.Contracts` / `.Util` / `.Web3`
     `4.21.4` → `6.1.0` (#28, plus an explicit `Newtonsoft.Json` `13.0.4` pin to clear the transitive
     NU1903 / GHSA-5crp-9r3c-p9vr); `ExtendedNumerics.BigRational` `2023.1000.2.328` → `3000.0.2.132` (#29).
   - **Test infrastructure:** `coverlet.collector` `6.0.2` → `10.0.1`, `Microsoft.NET.Test.Sdk`
     `17.11.0` → `18.7.0`, full `xunit` v2 → `xunit.v3` `3.2.2` (+ `xunit.runner.visualstudio` `3.1.5`,
     dropped `XunitContext`; converted the `async void` tests to `async Task`) (#27).
   - **CI:** `actions/setup-dotnet` → `v5.4.0` (node24) across all workflows to clear the Node-20
     deprecation, stale `# v4`/`# v2`/`# v3` action version comments corrected, and `codeql-action/analyze`
     aligned to `init` at v4.37.0 (#26).
2. **Seven `NotImplementedException` stubs** - the calldata / action builders: `SwapQuoter`,
   `NonfungiblePositionManager`, `Payments` (three methods), plus `PositionLibrary.SubIn256`
   and `PriceTick`. Port from the upstream references in the table above, with tests.
3. **`CurrencyAmount.ToExact` hardening** - FIXED for the known case; Phase B hardens it test-first
   against the full upstream `CurrencyAmount` suite.
4. **NuGet packaging** - DONE (metadata, SourceLink, symbols, MinVer, tag-driven release). To publish,
   add the `NUGET_API_KEY` secret and push a `v*` tag; `1.0.0` is reserved for V3-parity-complete.
5. **README + usage example** - DONE (see `README.md` + `docs/PORTING.md`); keep in sync as stubs land.
6. **V4 (later phase)** - Uniswap V4 reuses V3's concentrated-liquidity math (ticks,
   sqrt-price) and adds the singleton `PoolManager`, hooks and flash accounting. It is
   additive on top of this codebase, not a rewrite. Reference: `sdks/v4-sdk`.
