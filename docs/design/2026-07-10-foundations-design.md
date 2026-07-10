# UniswapSharp — Phase A: Repository Foundations

- **Date:** 2026-07-10
- **Status:** Approved (pending written-spec review)
- **Author:** Daniel Grimes (@grinidx)
- **Scope:** Repository hygiene, engineering process, CI/reporting, OSS configuration,
  documentation, and NuGet packaging **only**. The actual Uniswap V3 feature-parity port
  is **Phase B** and gets its own spec/plan.

---

## 1. Context

UniswapSharp is a C#/.NET port of the official Uniswap TypeScript SDKs
([`Uniswap/sdks`](https://github.com/Uniswap/sdks)) — packages `sdk-core` and `v3-sdk`.
V3 core (entities + math) is ported and largely tested (~194 xUnit tests). Before starting
the remaining V3 parity work, we want the repository itself to be in an excellent,
publish-ready state: clean git history, best-practice engineering process, working CI with
good test reporting, locked-down open-source configuration, strong docs, and NuGet packaging.

### 1.1 Review findings (current state)

- **Diverged git history.** Three lines diverge from commit `1ac4fed`:
  - `main`/`origin/main` (`87cd7c5`) — canonical: has the *rename-to-UniswapSharp* commit and `CLAUDE.md`.
  - `origin/dev` (`5af81b1`) — 4 commits on the **old `Uniswap.Sdk` naming**; its only behavioural
    change is `92a2065 "fix tests"`, a 2-line fix making `CurrencyAmount.ToExact()` honour its
    format string (trims trailing zeros — this is the known-failing test). The other three commits
    are pure reformatting of files `main` has since renamed.
  - `uniswap-v3-parity` — stale, sits exactly on `1ac4fed`.
- **CLAUDE.md** is accurate for `main`'s layout but light on PR/engineering discipline and has no
  migration/porting guide beyond a small stub-mapping table.
- **No README** anywhere.
- **CI (`.github/workflows/_test.yml`) bugs:**
  - `assemblyfilters: "-PinguApps.Appwrite.Shared.Tests"` — copy-paste leftover from an unrelated repo.
  - **No `pull_request:` trigger**, yet the coverage-comment and test-result steps are gated on
    `github.event_name == 'pull_request'` → **PR test reporting never runs.**
  - Reusable `workflow_call` workflow with no caller; only runs on direct push to main/dev.
- **Missing OSS files:** `CONTRIBUTING`, `SECURITY`, `CODE_OF_CONDUCT`, issue/PR templates,
  `dependabot.yml`, `CODEOWNERS`, `.editorconfig`, `CHANGELOG`.
- **Packaging:** `.csproj` has no NuGet metadata; not publishable.
- **License:** repo is MIT (© Diffused Networks). Upstream is **MIT © Uniswap Labs**. Compatible,
  but attribution must be retained for a derivative work.
- **Good:** repo is public + MIT; actions are SHA-pinned; `gh` is authenticated as `grinidx` with
  `repo`+`workflow` scopes (can configure branch protection & Actions permissions programmatically);
  upstream `sdk-core`/`v3-sdk`/`v4-sdk` are cloned locally.

## 2. Goals / Non-goals

**Goals**
1. Clean, understandable git state with a documented trunk + feature-branch/PR workflow.
2. `CLAUDE.md` and `CONTRIBUTING.md` encode best-practice engineering process (PRs, TDD, small commits).
3. A great `README` with a compile-verified usage example.
4. CI that builds, tests cross-platform, and produces **good test reporting** (PR comment,
   `$GITHUB_STEP_SUMMARY`, coverage) plus CodeQL security scanning.
5. Locked-down open-source repo configuration (branch protection, least-privilege Actions token,
   secret scanning, Dependabot) and the standard community-health files.
6. A migration/porting guide documenting upstream, this repo, and the mapping between them.
7. NuGet packaging metadata + an automated tag-driven release pipeline (secret added later by maintainer).
8. Proper license attribution to Uniswap Labs.

**Non-goals (Phase B, separate spec)**
- Implementing the 7 `NotImplementedException` stubs; the definitive test-first `ToExact` hardening;
  the full V3 gap audit; porting the remaining upstream test suites; actually publishing to NuGet;
  any V4 work.

## 3. Locked decisions

| # | Decision | Choice |
|---|---|---|
| 1 | Git base | **Reconcile `dev` into `main` first** (bring its real change in, retire `dev`); trunk + PR model |
| 2 | CI test reporting | **Self-contained**: PR comment + `$GITHUB_STEP_SUMMARY` + coverage (no external service) |
| 3 | NuGet publishing | **Automated on `v*` tag** (MinVer + `release.yml`, `NUGET_API_KEY` secret) |
| 4 | Plan scope | **Foundations now, port next** (Phase B separate) |
| 5 | `ToExact` fix | **Preserve dev's fix in Phase A** (green baseline); **harden test-first in Phase B** |
| 6 | PR granularity | **~5 focused PRs** |
| 7 | Extras | **CodeQL + cross-platform matrix + CHANGELOG + FUNDING.yml** |
| 8 | License | **Stay MIT**, add Uniswap Labs attribution (derivative work) |
| 9 | Target framework | **Retarget `net8.0` → `net10.0`** (latest .NET, per directive; validated green, removes the `DOTNET_ROLL_FORWARD` workaround). Folded into PR-1. Single-target; multi-targeting `net8.0;net10.0` is a possible future option if broader consumer reach is wanted. |

## 4. Detailed design

### 4.1 Git reconciliation & branching model
- **Verify** the `dev`↔`main` delta modulo rename+formatting to prove `ToExact` is the *only*
  behavioural change hidden in the large "formatting" diffs; salvage anything else if found.
- **Apply** the `ToExact` fix (`decimal.ToString(format)`) onto `main`'s renamed
  `CurrencyAmount.cs` + its test so the baseline is fully green. The three manual formatting
  commits are **superseded** (not cherry-picked) by adopting `.editorconfig` + `dotnet format`.
- **Retire** `origin/dev` and the stale `uniswap-v3-parity` branch after reconciliation.
- **Branching model:** `main` is protected trunk; all work on short-lived feature branches →
  PR → squash-merge; CI green required; linear history. No direct pushes to `main`.

### 4.2 CLAUDE.md upgrade
Keep existing content; add: explicit PR-based workflow (branch → small conventional commits →
PR → CI green → squash-merge), TDD/porting discipline, a "definition of done" for a ported module
(upstream `.ts` + `.test.ts` ported → green → mapping table updated → PR), the roll-forward test
command, and links to `docs/PORTING.md` and `CONTRIBUTING.md`.

### 4.3 README
Badges (CI status, license, target framework, NuGet-once-published); what/why; parity status;
install (`dotnet add package UniswapSharp`); a **compile-verified quickstart** (define tokens →
build a `Pool` → build a `Route` → construct/inspect a `Trade`); attribution to Uniswap Labs;
links to upstream and `CONTRIBUTING`; and a "not affiliated with Uniswap Labs / financial-risk —
verify before on-chain use" disclaimer. The exact example is validated by compiling it before merge.

### 4.4 CI overhaul + test reporting
- **`ci.yml`** (caller): triggers on `push` to `main`, `pull_request` to `main`, and manual dispatch;
  `concurrency` cancels in-progress runs for the same ref; top-level `permissions: contents: read`.
- **`_test.yml`** (reusable) fixes: remove the `PinguApps` filter; correct/simplify coverage;
  `setup-dotnet` installs 10.0; **cross-platform matrix**
  (`ubuntu-latest`, `windows-latest`, `macos-latest`).
- **Reporting (self-contained):**
  - `dotnet test --logger trx --logger GitHubActions` — the GitHub Actions logger annotates failing
    tests inline on the PR diff.
  - `EnricoMi/publish-unit-test-result-action` — a check + PR comment with pass/fail/skip counts and
    a list of failing tests (now actually fires because we add the `pull_request` trigger).
  - Coverage via coverlet → ReportGenerator → `irongut/CodeCoverageSummary` → sticky PR comment
    (`marocchino/sticky-pull-request-comment`).
  - A results + coverage table written to **`$GITHUB_STEP_SUMMARY`** so *every* run (not only PRs)
    shows a summary. Reporting jobs opt into `checks: write` + `pull-requests: write` at job level only.
  - Report from a single OS leg (ubuntu) to avoid duplicate comments; the matrix still gates merge.
- **CodeQL** (`codeql.yml`): C# analysis on PR + weekly schedule; `security-events: write` only.
- **Dependabot** (`dependabot.yml`): `nuget` + `github-actions` ecosystems, weekly.
- All third-party actions SHA-pinned.

### 4.5 OSS config + locked-down permissions
**Files:** `CONTRIBUTING.md` (setup, build/test, branching/PR flow, porting method, coding standards),
`CODE_OF_CONDUCT.md` (Contributor Covenant), `SECURITY.md` (private disclosure to dan@dbhq.uk;
crypto/financial-risk note), `.github/ISSUE_TEMPLATE/` (bug, feature, **porting-gap**),
`.github/PULL_REQUEST_TEMPLATE.md`, `CODEOWNERS` (@grinidx), `.github/dependabot.yml`,
`.editorconfig`, `CHANGELOG.md` (Keep a Changelog), `.github/FUNDING.yml`.

**Repo settings (via `gh api`, idempotent, read-back verified):**
- **Branch protection on `main`:** require the CI status check to pass + branch up to date;
  block force-pushes and deletions; require linear history; require conversation resolution.
  Required approvals = **0** (solo maintainer can't self-approve; documented how to raise later).
  Enable **after** `ci.yml` exists so the required check name resolves.
- **Actions token default = read-only**; disable "Actions can approve PRs".
- Enable **secret scanning + push protection** and **Dependabot alerts + security updates**.

### 4.6 Migration / porting guide — `docs/PORTING.md`
Documents three things:
1. **Upstream** — `Uniswap/sdks`, packages `sdk-core` + `v3-sdk` (+ `v4-sdk` later), pinned to
   commit `6081b3e` (2026-07-09) for reproducibility; local clone path.
2. **This repo** — namespace/layout mapping (`@uniswap/sdk-core` → `UniswapSharp.Core`,
   `@uniswap/v3-sdk` → `UniswapSharp.V3`).
3. **The port** — a **file-by-file mapping table** (upstream `.ts` ↔ C# file ↔ status:
   ported/partial/stub/test-ported), generated by diffing the upstream `v3-sdk/src` tree against
   `src/`; type/idiom conventions (`JSBI`/`BigInt` → `BigInteger`; Decimal.js `Fraction` →
   `Fraction`/`BigRational`; `invariant()` → guard/exception; `.test.ts` mocha/jest → xUnit +
   FluentAssertions; **no floating point in protocol math**); the **test-first porting workflow**;
   a running **"intentional divergences"** list; and how to re-sync when upstream advances.

### 4.7 NuGet packaging + automated release
- **`Directory.Build.props`** (repo root): `PackageId=UniswapSharp`, description, authors,
  `PackageLicenseExpression=MIT`, project/repository URLs, tags, **packed README**, `Deterministic`,
  `ContinuousIntegrationBuild` (CI), `GenerateDocumentationFile`, `IncludeSymbols` +
  `SymbolPackageFormat=snupkg`, **SourceLink** (`Microsoft.SourceLink.GitHub`), and **MinVer** for
  tag-driven versioning.
- **`release.yml`:** on `push` tag `v*` → build/test/pack → push `.nupkg`+`.snupkg` to NuGet.org via
  `NUGET_API_KEY` secret → attach artifacts to the GitHub Release. `permissions: contents: write` only.
- Initial version `0.1.0-alpha`; **`1.0.0` is reserved for V3-parity-complete** (Phase B).
- Verification packs locally and inspects the `.nupkg` (README, XML docs, symbols present); no
  publish happens in Phase A.

### 4.8 License attribution
Keep MIT. `LICENSE` carries dual copyright — `© 2021 Uniswap Labs` (original SDKs) and
`© 2024–2026 Diffused Networks` (the port) — plus a short note that this is a derivative work of
`Uniswap/sdks`. Mirror the attribution in `README` and `docs/PORTING.md`. `PackageLicenseExpression`
stays `MIT`.

## 5. PR breakdown (≈5 focused PRs, into `main`)

Ordering respects dependencies (CI must exist before branch protection requires it).

1. **`chore/reconcile-dev`** — verify dev delta; apply `ToExact` fix + its test; green baseline;
   add this design doc. Then delete the `dev` and `uniswap-v3-parity` branches.
   *(Optional split: a follow-up mechanical `style/editorconfig-format` PR adds `.editorconfig`
   and runs `dotnet format`, kept separate so the functional diff stays reviewable.)*
2. **`ci/test-reporting`** — `ci.yml` + fixed `_test.yml` + matrix + reporting + CodeQL + dependabot
   + concurrency + least-privilege. Verified by opening the PR and confirming the report renders.
3. **`chore/oss-config`** — community-health files + `CODEOWNERS` + `CHANGELOG` + `FUNDING`
   (`.editorconfig` lands with the formatting split in PR-1); then apply repo settings
   (branch protection, Actions token, scanning) via `gh api` and read them back.
4. **`docs/readme-and-porting`** — `README`, `CLAUDE.md` upgrade, `docs/PORTING.md`, `LICENSE`
   attribution + NOTICE.
5. **`build/nuget-packaging`** — `Directory.Build.props` + MinVer + SourceLink + `release.yml`;
   verified via `dotnet pack` + package inspection.

## 6. Verification (Phase A definition of done)
- `dotnet test -c Release` → **all green** (194/194 after `ToExact`), building on `net10.0`.
- `dotnet format --verify-no-changes` → clean.
- `dotnet pack -c Release` → valid `.nupkg` + `.snupkg` with README, XML docs, and symbols.
- A real PR visibly renders the **test-result check + coverage comment + step summary**.
- `gh api` read-back confirms branch protection, read-only Actions token, and scanning are enabled.
- Green CI on ubuntu/windows/macos.

## 7. Risks & mitigations
- **Hidden semantic changes in dev's "formatting" commits** → mitigated by an explicit
  content diff (dev↔main modulo rename/format) before discarding them.
- **Branch protection locking out the solo maintainer** → required approvals set to 0; status-check
  gate only; documented how to raise when contributors join.
- **`gh` token missing a needed permission** → each setting applied idempotently with read-back;
  any that fail are surfaced with the exact manual step.
- **Packed README/usage example drifting from real API** → the quickstart is compiled before merge.

## 8. Phase B preview (out of scope here)
Fix/harden `ToExact` test-first; implement the 7 stubs (`SwapQuoter`,
`NonfungiblePositionManager`, `Payments`×3, `PositionLibrary.SubIn256`, `PriceTick`) against
upstream references; audit V3 for remaining gaps; port all remaining upstream test suites;
finalize packaging and publish `1.0.0`; then (later) V4. Phase B gets its own brainstorm → spec → plan.

## Appendix
- **Upstream pinned commit:** `6081b3e7169a761188cd5e77675be9e5da5d331e` (2026-07-09), `Uniswap/sdks`.
- **Local upstream clone:** `/home/devops/uniswap-sdks-official/sdks/` (`sdk-core`, `v3-sdk`, `v4-sdk`).
- **Repo:** https://github.com/grinidx/UniswapSharp (public, MIT, default branch `main`).
