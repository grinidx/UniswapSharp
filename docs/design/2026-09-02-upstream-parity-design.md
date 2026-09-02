# Design — upstream parity sweep, `6081b3e` → `35c4e35`

Date: 2026-09-02
Status: approved, not yet implemented

## 1. Problem

`docs/PORTING.md` pins the upstream source of truth at
[`Uniswap/sdks@6081b3e`](https://github.com/Uniswap/sdks/commit/6081b3e7169a761188cd5e77675be9e5da5d331e)
(2026-07-09). Upstream `main` is now at `35c4e35` (2026-09-01) — **54 commits ahead**.

The drift is not cosmetic. It includes four genuine bug fixes that our port has inherited,
one breaking signature change, and roughly 1,600 lines of new feature surface. Left alone,
UniswapSharp silently produces wrong output for the cases those fixes address.

### 1.1 Size of the drift

34 non-test source files changed, **+5,277 / −333**, plus ~2,200 lines of upstream
`.test.ts` changes. Six of our twelve modules are affected:

| Module | Upstream package | Files | Nature |
|---|---|---|---|
| `LiquidityLauncher` | `liquidity-launcher-sdk` | 15 | ~70% of the work: new launch surface, fee/tick math, ~15 deployment redeploys, one breaking change |
| `UniversalRouter` | `universal-router-sdk` | 11 | New direct-transfer surface + three encoding fixes |
| `UniswapX` | `uniswapx-sdk` | 3 | `OrderQuoter` rewrite, batch ordering fix, API rename, new chain |
| `Router` | `router-sdk` | 2 | `MixedRouteSDK.midPrice` correctness fix |
| `Core` | `sdk-core` | 2 | Address/chain registry corrections |
| `SmartWallet` | `smart-wallet-sdk` | 1 | Monad support |

Our file tree already maps 1:1 onto upstream's, so every changed upstream file has an
unambiguous target. Five upstream files are new and need new C# files:
`InstantLaunch.cs`, `InstantLaunchFees.cs`, `QuickLaunch.cs`, `Reads.cs`,
and `UniversalRouter/Utils/DirectTransfers.cs`.

## 2. Goals and non-goals

**Goals**

- Reach behavioural parity with upstream `35c4e35` across all six drifted modules.
- Port the upstream `.test.ts` vectors alongside each change, per the CLAUDE.md
  porting methodology — behaviour verified against the reference, not assumed.
- Keep `main` releasable at every step: each PR is independently green.
- Move the `docs/PORTING.md` pin to `35c4e35` once, at the end.

**Non-goals**

- No refactoring beyond what the ported changes require.
- No new capability that upstream does not have.
- No release tag. The work accumulates under `[Unreleased]`; see §5.

## 3. Approach: five sequenced PRs

One PR per coherent slice, ordered by dependency. Each is test-first and green before merge.

`docs/PORTING.md` gets a checklist under §7 tracking which slices have landed; individual
mapping-table rows are updated only where a table exists for the package — `sdk-core` has
none, so PR 1 only ticks its box. PR 5 removes the checklist and bumps the §1 pin.

### PR 1 — `Core` address and chain registry

Foundation: every other module resolves addresses and chain IDs through these tables, so
a wrong value here propagates. Data-only, no logic.

- Sepolia `tickLensAddress` was set to the multicall address (#654). A real bug.
- Canonical `PermissionedHooks` addresses for mainnet and sepolia (#657), which adds a new
  `permissionedV4HooksAddress` field to the `ChainAddresses` shape.
- Permissioned-pools address correction for mainnet and sepolia (#692).
- Arc average block time `0.48` → `0.5`. `SecondsToBlocks` divides by it, so the stale
  value also made `ceil(8 / 0.48) = 17` where upstream now expects `16`.

Vectors: upstream `sdk-core/src/chains.test.ts`.

### PR 2 — small independent fixes

Four unrelated single-file changes. Each is too small to justify its own PR; none of them
interact, so reviewing them together costs nothing.

- **`Router`** — `MixedRouteSDK.midPrice` was wrong across native/wrapped boundaries
  (#706, ROUTE-886). Upstream replaced the `reduce` that tracked `nextInput` by token
  identity with one that orients each pool against the already-resolved `path[i + 1]`,
  because the constructor pushes the pool's own currency object when bridging a
  native/wrapped boundary. The same commit fixes `partitionMixedRouteByProtocol`, which
  failed to end a section at a native/wrapped boundary inside one protocol — a wrap or
  unwrap is needed there. Vectors: `route.test.ts` (+62) and `utils/index.test.ts` (+64).
- **`UniswapX`** — `quoteBatch` / `validateBatch` returned results out of the caller's
  order (#685); a new multicall helper carries the ordering. Also the
  `MulticallSameContractManyFunctions` → `...ManyCalls` rename (#686) and Ink (57073)
  for the DutchV3 rollout (#682). Vectors: `utils/multicall.test.ts` (+227),
  `constants.test.ts`, `V3DutchOrderBuilder.test.ts`.
- **`SmartWallet`** — Monad support (#637). Vectors: `constants.test.ts`.
- **`UniversalRouter`** — sepolia Universal Router 2.1.1 address (#658). Data-only.

### PR 3 — `UniversalRouter` encoding

These three changes all touch swap-step encoding and the command tables around it. Landing
them separately would mean encoding calldata against a half-updated command set, so they go
together.

- V4 exact-out legs are floored with an explicit `TAKE` amount (#640, ROUTE-1394).
- `ACROSS_V4_DEPOSIT_V3` command input is encoded as a single tuple, not flat args (#690).
- New `DirectTransfers.cs` (291 lines) allowing direct transfers as an option on swap
  steps (#638), with the `ValidateEncodeSwaps` and `RouterCommands` changes it depends on.

Vectors: upstream `test/unit/across.test.ts`, the swap-step unit tests, and the
`test/forge/interop.json` calldata fixtures — these are byte-exact and must match to the digit.

### PR 4 — `LiquidityLauncher` registry and config

Registries first, then the config math that reads them.

- Deployment registries: `Addresses`, `Abis`, `Chains`, `LockRecipientBytecode`,
  and `Constants` (LBPStrategy v3.1.0 addresses, Arc block time). Roughly 15 commits of
  redeploys and new chains, through Arc (5042) and the chain-4663 rebuild.
- Config math: `Fees`, `Price`; new-pool tick spacing derived as
  `max(round(fee / 100), 1)` (#699).
- **Breaking:** `BuildPositionDefinitions` now takes the raised `currency` and launched
  `token` addresses so it can apply v4 currency ordering (#651). When `currency` sorts as
  `currency0` — always so for native-ETH launches — the pool price is the reciprocal of the
  CCA currency-per-token price, so custom asymmetric ranges must be mirrored onto the
  reciprocal band (offsets negated and swapped). Previously they landed on the mirror image
  of the intended band. Full-range positions use the `(MIN_TICK, MAX_TICK)` sentinel and are
  unaffected.

  We match upstream's signature exactly rather than keeping a compatibility overload. The
  repo's stated rule is a line-for-line port; preserving a shape upstream considers wrong
  would diverge our API for the sake of a version number.

Vectors: `addresses.test.ts` (+380), `config/positions.test.ts` (+71), `config/fees.test.ts`,
`config/price.test.ts`, `abis.test.ts`, `lock.test.ts`.

### PR 5 — `LiquidityLauncher` new launch surface

The largest single slice, and dependent on PR 4's registries and config.

- `QuickLaunch.cs` (611 lines) — canonical quick-launch preset, pure `IsQuickLaunch`
  matcher, graduation decoupled from the floor (graduation FDV $10k, floor FDV $1k),
  classification gated on graduation FDV, graduation pool tick spacing 25.
- `InstantLaunch.cs` (455) — deployment registry, transaction builder, strategy
  generations `8e40a35` / `3e05da8` / `c3f9506`, pool shape at tick spacing 25.
- `InstantLaunchFees.cs` (121) — fee math.
- `Reads.cs` (60) — CCA auction read helpers, creator-fee and autocompound position
  recipient accessors.
- The `Encode` and `Build` deltas these depend on: the CCA auction interaction helpers
  (#636) and the Instant Launch transaction builder (#660). Upstream's `index.ts` barrel
  export has no C# counterpart — namespaces replace it, as recorded in `docs/PORTING.md`.

Vectors: `quickLaunch.test.ts` (+661), `instantLaunch.test.ts` (+408),
`instantLaunchFees.test.ts` (+88), `encode.test.ts`, `build.test.ts`, `config/blocks.test.ts`
(the Arc block time added in PR 4 is asserted here).

Finally, PR 5 moves the `docs/PORTING.md` pin from `6081b3e` to `35c4e35` and records the
sweep in `CHANGELOG.md` under `[Unreleased]`.

## 4. Testing

Unchanged from the repo's existing methodology, which this design does not revisit:

- Port the upstream `.test.ts` cases to xUnit before writing the C# implementation.
- `BigInteger` / `BigRational` only; no floating point in protocol math.
- Calldata and address output must match upstream byte for byte.
- `dotnet test -c Release` green and `dotnet format --verify-no-changes` clean before each
  merge; CI verifies on ubuntu, windows and macos.

The upstream reference clone at `/home/devops/uniswap-sdks-official` is currently checked
out at the old pin. It gets fast-forwarded to `35c4e35` at the start of implementation so
the `.ts` sources being read match the target.

## 5. Versioning

`BuildPositionDefinitions` (#651) is a breaking signature change. We ship a single NuGet
package, so a breaking change anywhere makes the next release a major: **2.0.0**.

Nothing breaks on merge — MinVer stamps from tags, so the version only moves when a `v*`
tag is pushed. The five PRs accumulate under `[Unreleased]`, and the release decision is
separate from this work.

## 6. Risks

- **PR 3 and PR 5 carry the real risk.** Both encode calldata that must be byte-exact.
  Mitigated by porting the upstream fixtures (`interop.json`, the launch test vectors)
  rather than hand-writing expectations.
- **PR 4's breaking change silently changes output** for existing callers who pass custom
  asymmetric ranges. It is the correct output, but it is different output. The CHANGELOG
  entry must say so plainly.
- **Upstream keeps moving.** Fifty-four commits landed in eight weeks, most of them in
  `liquidity-launcher-sdk`. This design targets a fixed commit; further drift is a later
  sweep, not a moving target for this one.
