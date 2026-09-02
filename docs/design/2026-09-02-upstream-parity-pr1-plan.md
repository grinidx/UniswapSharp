# Upstream Parity PR 1 — `Core` address and chain registry — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring `UniswapSharp.Core`'s address and chain-metadata tables to upstream `35c4e35`, correcting the sepolia TickLens address, the mainnet and sepolia permissioned-V4 addresses, and the Arc block time.

**Architecture:** Data-only. Three source files change (`ChainAddresses.cs` gains one property; `Addresses.cs` gets five corrected/added values; `Constants.cs` gets one corrected value) plus the two test files that pin them. No logic, no signatures, no new types beyond one property.

**Tech Stack:** C# / .NET 10, xunit.v3 4.x on Microsoft.Testing.Platform, AwesomeAssertions.

## Global Constraints

- Upstream source of truth: `Uniswap/sdks@35c4e35`, local clone at `/home/devops/uniswap-sdks-official`. Reference files: `sdks/sdk-core/src/addresses.ts`, `sdks/sdk-core/src/chains.ts`, `sdks/sdk-core/src/chains.test.ts`.
- Test-first: write or change the assertion, watch it fail, then change the source.
- Address string casing is copied verbatim from upstream. Upstream mixes checksummed and lowercase forms; do not normalise, because the ported tests compare strings exactly.
- `dotnet test -c Release` green and `dotnet format UniswapSharp.sln --verify-no-changes` clean before each commit.
- Branch off `main`: `fix/core-address-registry-parity`. Never commit to `main`.
- Do not touch `docs/PORTING.md`'s pinned commit — that moves in PR 5, not here.

---

### Task 1: Arc average block time `0.48` → `0.5`

Upstream `chains.ts` corrected Arc's cadence. Our `SecondsToBlocks` divides by it, so the
error propagates: `ceil(8 / 0.48) = 17` where upstream now expects `16`.

**Files:**
- Modify: `src/UniswapSharp/Core/Constants.cs:85`
- Test: `test/UniswapSharp.Testing/Core/ChainsTests.cs:15,33`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `Constants.AVERAGE_BLOCK_TIMES_SECONDS[ChainId.ARC] == 0.5`. No signature change; `GetAverageBlockTimeSecs(ChainId)` and `SecondsToBlocks(int, ChainId)` keep their existing shapes.

- [ ] **Step 1: Change the two assertions to the upstream values**

In `test/UniswapSharp.Testing/Core/ChainsTests.cs`, line 15:

```csharp
        Assert.Equal(0.5, Constants.GetAverageBlockTimeSecs(ChainId.ARC));
```

and line 33:

```csharp
        Assert.Equal(16, Constants.SecondsToBlocks(8, ChainId.ARC));          // ceil(8/0.5)
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test -c Release`
Expected: FAIL — two assertion failures in `ChainsTests`, reporting `0.48` where `0.5` was expected and `17` where `16` was expected.

- [ ] **Step 3: Correct the table entry**

In `src/UniswapSharp/Core/Constants.cs`, line 85:

```csharp
        { ChainId.ARC, 0.5 },
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test -c Release`
Expected: PASS, 1695 tests.

- [ ] **Step 5: Commit**

```bash
git add src/UniswapSharp/Core/Constants.cs test/UniswapSharp.Testing/Core/ChainsTests.cs
git commit -m "fix(core): correct Arc average block time to 0.5s

Upstream 35c4e35 revised Arc's cadence from 0.48s to 0.5s. SecondsToBlocks
divides by it, so the stale value also made ceil(8/0.48)=17 where upstream
now expects ceil(8/0.5)=16."
```

---

### Task 2: Sepolia `TickLensAddress` correction

Upstream #654: sepolia's `tickLensAddress` had been set to the **multicall** address. Any
caller resolving TickLens on sepolia was pointed at the wrong contract.

**Files:**
- Modify: `src/UniswapSharp/Core/Addresses.cs:256`
- Test: `test/UniswapSharp.Testing/Core/AddressesTests.cs` (append a new `[Fact]`)

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `Addresses.TICK_LENS_ADDRESSES[ChainId.SEPOLIA] == "0x0b343475d44EC2b4b8243EBF81dc888BF0A14b36"`. Reached through the existing `TICK_LENS_ADDRESSES` dictionary (`Dictionary<ChainId, string?>`, declared at `Addresses.cs:692`); no new accessor.

- [ ] **Step 1: Write the failing test**

Append to `test/UniswapSharp.Testing/Core/AddressesTests.cs`, inside the `AddressesTests` class:

```csharp
    [Fact]
    public void TickLensAddresses_ShouldReturnCorrectAddress_ForSepolia()
    {
        // Upstream #654: this was previously the multicall address, not TickLens.
        Assert.Equal("0x0b343475d44EC2b4b8243EBF81dc888BF0A14b36", Addresses.TICK_LENS_ADDRESSES[ChainId.SEPOLIA]);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test -c Release`
Expected: FAIL — actual is `0xd7f33bcdb21b359c8ee6f0251d30e94832baad07` (the multicall address).

- [ ] **Step 3: Correct the address**

In `src/UniswapSharp/Core/Addresses.cs`, line 256, inside the sepolia `ChainAddresses` initialiser:

```csharp
        TickLensAddress = "0x0b343475d44EC2b4b8243EBF81dc888BF0A14b36",
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test -c Release`
Expected: PASS, 1696 tests (one added).

- [ ] **Step 5: Commit**

```bash
git add src/UniswapSharp/Core/Addresses.cs test/UniswapSharp.Testing/Core/AddressesTests.cs
git commit -m "fix(core): point sepolia tickLensAddress at TickLens, not multicall

Ports upstream #654. The sepolia entry held 0xd7f33bcd..., which is the
multicall address, so anything resolving TickLens on sepolia got the wrong
contract."
```

---

### Task 3: Permissioned-V4 addresses for mainnet and sepolia

Upstream #657 and #692: both chains' `permissionedV4PositionManagerAddress` moved, and a
new `permissionedV4HooksAddress` was added alongside. This is the only task that changes a
type.

**Files:**
- Modify: `src/UniswapSharp/Core/ChainAddresses.cs:20` (add one property after `PermissionedV4PositionManagerAddress`)
- Modify: `src/UniswapSharp/Core/Addresses.cs:114` (mainnet), `:265` (sepolia)
- Test: `test/UniswapSharp.Testing/Core/AddressesTests.cs` (append two new `[Fact]`s)

**Interfaces:**
- Consumes: `Addresses.CHAIN_TO_ADDRESSES_MAP` (`Dictionary<ChainId, ChainAddresses>`, declared at `Addresses.cs:585`), already present.
- Produces: `ChainAddresses.PermissionedV4HooksAddress` as `public string? { get; set; }`, mirroring the existing nullable-string properties on that type. There is no derived `PERMISSIONED_V4_*` dictionary upstream or here, so callers read it off `CHAIN_TO_ADDRESSES_MAP`.

- [ ] **Step 1: Write the failing tests**

Append to `test/UniswapSharp.Testing/Core/AddressesTests.cs`, inside the `AddressesTests` class:

```csharp
    [Fact]
    public void PermissionedV4Addresses_ShouldReturnCorrectAddresses_ForMainnet()
    {
        ChainAddresses mainnet = Addresses.CHAIN_TO_ADDRESSES_MAP[ChainId.MAINNET];
        Assert.Equal("0x63Bd7e5D4EcfAA74d82AE1dE98F476C935a81973", mainnet.PermissionedV4PositionManagerAddress);
        Assert.Equal("0x499a724Ab630549f14C995EC41a8E04fA3fd28c0", mainnet.PermissionedV4HooksAddress);
    }

    [Fact]
    public void PermissionedV4Addresses_ShouldReturnCorrectAddresses_ForSepolia()
    {
        ChainAddresses sepolia = Addresses.CHAIN_TO_ADDRESSES_MAP[ChainId.SEPOLIA];
        Assert.Equal("0xf99D553912084c99F6299291b75Fe9B7119Aa1A7", sepolia.PermissionedV4PositionManagerAddress);
        Assert.Equal("0x51247E2291d290d17C08813A175AC86465EdE8c0", sepolia.PermissionedV4HooksAddress);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test -c Release`
Expected: FAIL at **compile time** — `'ChainAddresses' does not contain a definition for 'PermissionedV4HooksAddress'`. That is the expected first failure; the value mismatches surface once the property exists.

- [ ] **Step 3: Add the property**

In `src/UniswapSharp/Core/ChainAddresses.cs`, directly after line 20:

```csharp
    public string? PermissionedV4HooksAddress { get; set; }
```

- [ ] **Step 4: Run the tests to verify they now fail on the values**

Run: `dotnet test -c Release`
Expected: FAIL — four assertion failures: the two position-manager addresses still hold their old values, and both hooks addresses are `null`.

- [ ] **Step 5: Set the mainnet values**

In `src/UniswapSharp/Core/Addresses.cs`, replace line 114 (the last entry of the mainnet initialiser — note the trailing comma is now needed):

```csharp
        PermissionedV4PositionManagerAddress = "0x63Bd7e5D4EcfAA74d82AE1dE98F476C935a81973",
        PermissionedV4HooksAddress = "0x499a724Ab630549f14C995EC41a8E04fA3fd28c0"
```

- [ ] **Step 6: Set the sepolia values**

In `src/UniswapSharp/Core/Addresses.cs`, replace line 265 (the last entry of the sepolia initialiser — same trailing-comma note):

```csharp
        PermissionedV4PositionManagerAddress = "0xf99D553912084c99F6299291b75Fe9B7119Aa1A7",
        PermissionedV4HooksAddress = "0x51247E2291d290d17C08813A175AC86465EdE8c0"
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test -c Release`
Expected: PASS, 1698 tests.

- [ ] **Step 8: Verify formatting**

Run: `dotnet format UniswapSharp.sln --verify-no-changes`
Expected: no output, exit 0.

- [ ] **Step 9: Commit**

```bash
git add src/UniswapSharp/Core/ChainAddresses.cs src/UniswapSharp/Core/Addresses.cs test/UniswapSharp.Testing/Core/AddressesTests.cs
git commit -m "feat(core): add permissionedV4HooksAddress; update permissioned pool addresses

Ports upstream #657 and #692. Adds the canonical PermissionedHooks address
for mainnet and sepolia, and moves both chains' permissioned V4 position
manager to the current deployment."
```

---

### Task 4: Record the slice in `docs/PORTING.md`

`sdk-core` has **no** file-by-file mapping table — §4 covers v3-sdk only and §9 lists the
other packages as prose. So there is no row to edit. Instead, add a sweep-progress note
under §7, which is where re-syncing is documented. That note becomes the running checklist
for all five PRs, and PR 5 deletes it when it bumps the pin.

**Files:**
- Modify: `docs/PORTING.md` §7 "Re-syncing with upstream" (currently lines 142-144)

**Interfaces:**
- Consumes: the three completed source changes.
- Produces: the `### In-flight: parity sweep` heading that PRs 2-5 tick boxes in and PR 5 removes.

- [ ] **Step 1: Append the sweep note to §7**

Directly after the existing two-line body of §7 (`...and bump the pinned commit above.`), add:

```markdown

### In-flight: parity sweep `6081b3e` → `35c4e35`
Five dependency-ordered PRs, per [`docs/design/2026-09-02-upstream-parity-design.md`](design/2026-09-02-upstream-parity-design.md).
The pinned commit in §1 stays at `6081b3e` until the last one lands.

- [x] PR 1 — `Core` address + chain registry (sepolia TickLens, permissioned V4 + hooks, Arc block time)
- [ ] PR 2 — small independent fixes (`Router` midPrice, `UniswapX` batch ordering, `SmartWallet` Monad, UR sepolia address)
- [ ] PR 3 — `UniversalRouter` encoding (V4 exact-out TAKE floor, ACROSS tuple, direct transfers)
- [ ] PR 4 — `LiquidityLauncher` registry + config (incl. the breaking `BuildPositionDefinitions` signature)
- [ ] PR 5 — `LiquidityLauncher` launch surface; bump the §1 pin to `35c4e35`
```

- [ ] **Step 3: Commit**

```bash
git add docs/PORTING.md
git commit -m "docs(porting): note sdk-core address/chain rows ported to 35c4e35"
```

- [ ] **Step 4: Push and open the PR**

```bash
git push -u origin fix/core-address-registry-parity
gh pr create --base main \
  --title "fix(core): address and chain registry parity with upstream 35c4e35" \
  --body "First of the five PRs in docs/design/2026-09-02-upstream-parity-design.md.

Corrects the sepolia TickLens address (#654 — it was the multicall address),
adds \`PermissionedV4HooksAddress\` with the canonical mainnet and sepolia
values, moves both chains' permissioned V4 position manager to the current
deployment (#657, #692), and corrects Arc's average block time to 0.5s.

Data-only; no logic or signature changes beyond the one new nullable property."
```

- [ ] **Step 5: Wait for CI, then squash-merge**

Run: `gh pr checks --watch`
Expected: all three OS legs pass. Then `gh pr merge --squash --delete-branch`.
