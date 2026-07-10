# UniswapSharp Phase A — Repository Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Get the UniswapSharp repo into an excellent, publish-ready state — clean git,
best-practice engineering process, working CI with good test reporting, locked-down OSS
configuration, strong docs, and NuGet packaging — before the V3 parity port (Phase B).

**Architecture:** Five focused PRs into `main`, in dependency order: (1) reconcile `dev`'s real
change + green baseline, (2) CI + reporting + CodeQL, (3) OSS config + repo lockdown, (4) docs,
(5) NuGet packaging + release. Each PR is independently reviewable and leaves the repo green.

**Tech Stack:** .NET 10 (`net10.0`, latest), xUnit + FluentAssertions, GitHub Actions, `gh` CLI, MinVer,
SourceLink, coverlet/ReportGenerator.

## Global Constraints

- Target framework is `net10.0` (latest .NET; requires the .NET 10 SDK — the only SDK on this box).
  Baseline test command: `dotnet test -c Release` (no `DOTNET_ROLL_FORWARD` needed once retargeted).
- Never use floating point in protocol math — `BigInteger` / `BigRational` only. (Phase A touches
  only `ToExact` formatting; do not alter other math.)
- All third-party GitHub Actions MUST be SHA-pinned (`uses: owner/repo@<sha> # vX`).
- License stays **MIT**; retain Uniswap Labs attribution (derivative work).
- Package id: `UniswapSharp`. Repo: `https://github.com/grinidx/UniswapSharp` (public).
- Upstream source of truth: `Uniswap/sdks`, commit `6081b3e7169a761188cd5e77675be9e5da5d331e`,
  local clone `/home/devops/uniswap-sdks-official/sdks/`.
- Work on feature branches off `main`; small commits; PR per task-group; CI green before merge.
- Solution: `UniswapSharp.sln`; library: `src/UniswapSharp/UniswapSharp.csproj`; tests:
  `test/UniswapSharp.Testing/UniswapSharp.Testing.csproj`.

---

## Task 1: Retarget to .NET 10 + reconcile `dev` + green baseline (PR `chore/reconcile-dev`)

**Files:**
- Modify: `src/UniswapSharp/UniswapSharp.csproj:4` (TFM)
- Modify: `test/UniswapSharp.Testing/UniswapSharp.Testing.csproj:4` (TFM)
- Modify: `src/UniswapSharp/Core/Entities/Fractions/CurrencyAmount.cs:81-84`
- Modify: `test/UniswapSharp.Testing/Core/Entities/Fractions/CurrencyAmountTests.cs:124-128`
- Already present on branch: `docs/design/2026-07-10-foundations-design.md`, `...-plan.md`

**Interfaces:**
- Produces: `CurrencyAmount<T>.ToExact(string format = "0.#############################")` returning a
  culture-invariant string that honours `format` (trims trailing zeros).
- Produces: both projects target `net10.0` (the whole solution builds/tests on the .NET 10 SDK with
  no `DOTNET_ROLL_FORWARD`). This was validated ahead of time — the retarget is known-green.

- [ ] **Step 1: Retarget both projects to `net10.0`**

In `src/UniswapSharp/UniswapSharp.csproj` and `test/UniswapSharp.Testing/UniswapSharp.Testing.csproj`,
change the target framework line:
```xml
    <TargetFramework>net10.0</TargetFramework>
```
(from `net8.0`. No other csproj changes in this task — packaging metadata is Task 5.)

- [ ] **Step 2: Confirm the retargeted baseline — builds, one known failure**

Run: `cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver && dotnet test -c Release`
Expected: builds on `net10.0`; **193 passed, 1 failed** — `CurrencyAmountTests.ToExact_IsCorrectFor18Decimals`
(`"0.001230000000000000"` vs expected `"0.00123"`). (No `DOTNET_ROLL_FORWARD` needed.)

- [ ] **Step 3: Verify `dev` has no hidden behavioural change beyond `ToExact`**

Run:
```bash
cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver
# Compare dev's tree to its base (1ac4fed), ignoring whitespace, restricted to src (not tests):
git diff -w 1ac4fed origin/dev -- 'src/**/*.cs' | grep -E '^[+-]' | grep -vE '^[+-]{3} ' | grep -viE 'ToString|ToExact' | head -40
```
Expected: no non-whitespace source change other than the `ToExact` line. If other lines appear,
STOP and evaluate each before proceeding (salvage any real fix).

- [ ] **Step 4: Apply the `ToExact` fix (preserve dev's change, keep invariant culture)**

In `src/UniswapSharp/Core/Entities/Fractions/CurrencyAmount.cs`, replace the body of `ToExact`:
```csharp
    public string ToExact(string format = "0.#############################")
    {
        return ((decimal)Quotient / (decimal)DecimalScale).ToString(format, CultureInfo.InvariantCulture);
    }
```
(`using System.Globalization;` is already present at the top and stays — `CultureInfo` is still used.)

- [ ] **Step 5: Fix the companion test to use the default format**

In `test/UniswapSharp.Testing/Core/Entities/Fractions/CurrencyAmountTests.cs`, the
`ToExact_DoesNotThrowForSigFigsGreaterThanCurrencyDecimals` test currently passes an invalid
format literal. Change line 128 from:
```csharp
            Assert.Equal("1000", amount.ToExact("{00000}"));
```
to:
```csharp
            Assert.Equal("1000", amount.ToExact());
```

- [ ] **Step 6: Run the full suite — expect all green**

Run: `dotnet test -c Release`
Expected: **194 passed, 0 failed.**

- [ ] **Step 7: Commit**

```bash
git add src/UniswapSharp/UniswapSharp.csproj test/UniswapSharp.Testing/UniswapSharp.Testing.csproj \
        src/UniswapSharp/Core/Entities/Fractions/CurrencyAmount.cs \
        test/UniswapSharp.Testing/Core/Entities/Fractions/CurrencyAmountTests.cs \
        docs/design/2026-07-10-foundations-design.md docs/design/2026-07-10-foundations-plan.md
git commit -m "chore: retarget to .NET 10; fix ToExact format; reconcile dev; add Phase A design/plan

Retargets both projects from net8.0 to net10.0 (latest .NET; validated green).
CurrencyAmount.ToExact now formats with the supplied (invariant-culture) format
string instead of a fixed F<decimals>, trimming trailing zeros. Reconciles the only
behavioural change on origin/dev (92a2065). Baseline now 194/194 green.
Phase B will harden ToExact test-first against the full upstream suite."
```

- [ ] **Step 8: Push and open the PR**

```bash
git push -u origin chore/repo-foundations
gh pr create --base main --title "chore: retarget to .NET 10 + reconcile dev ToExact fix (green baseline)" \
  --body "Retargets to net10.0 (latest), applies the only real change from origin/dev (ToExact format fix) for a 194/194 baseline, and adds the Phase A foundations design + plan. Retires origin/dev after merge."
```
Expected: PR created. (CI does not yet report richly — Task 2 fixes that.)

- [ ] **Step 9: After this PR merges, retire the stale branches**

```bash
git push origin --delete dev || true
git branch -D uniswap-v3-parity 2>/dev/null || true
git push origin --delete uniswap-v3-parity 2>/dev/null || true
```
Expected: `dev` (and the stale parity branch, if pushed) removed. Note: run only after merge.

---

### Task 1b (optional split): `.editorconfig` + `dotnet format` (PR `style/editorconfig-format`)

Kept separate so the large auto-format diff never obscures a functional review.

**Files:**
- Create: `.editorconfig`
- Modify: all `*.cs` (mechanical formatting only)

- [ ] **Step 1: Create `.editorconfig`** at repo root:
```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space

[*.{cs,csx}]
indent_size = 4
# Namespaces, usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_using_directive_placement = outside_namespace:warning
# Prefer file-scoped namespaces (matches existing code)
csharp_style_namespace_declarations = file_scoped:warning
# Braces and newlines
csharp_new_line_before_open_brace = all
csharp_prefer_braces = true:suggestion
# 'this.' preferences
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
# var usage — leave as-is (codebase mixes); do not enforce
csharp_style_var_for_built_in_types = false:none
csharp_style_var_when_type_is_apparent = false:none

[*.{yml,yaml,json,md}]
indent_size = 2

[*.{csproj,props,targets}]
indent_size = 2
```

- [ ] **Step 2: Run the formatter**

Run: `cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver && dotnet format UniswapSharp.sln`
Expected: files reformatted, exit 0.

- [ ] **Step 3: Confirm still green + idempotent**

Run: `dotnet test -c Release && dotnet format UniswapSharp.sln --verify-no-changes`
Expected: 194 passed; `--verify-no-changes` exits 0.

- [ ] **Step 4: Commit + PR**

```bash
git add .editorconfig && git add -u   # stage new .editorconfig + tracked reformats only (no scratch)
git commit -m "style: add .editorconfig and apply dotnet format (no behavioural change)"
git push -u origin style/editorconfig-format
gh pr create --base main --title "style: add .editorconfig + dotnet format" --body "Mechanical formatting only. Supersedes the ad-hoc formatting commits from origin/dev. 194/194 green; dotnet format --verify-no-changes clean."
```

---

## Task 2: CI overhaul + test reporting + CodeQL (PR `ci/test-reporting`)

**Branch:** `git checkout -b ci/test-reporting main` (after Task 1 merges).

**Files:**
- Create: `.github/workflows/ci.yml`
- Rewrite: `.github/workflows/_test.yml`
- Create: `.github/workflows/codeql.yml`
- Create: `.github/dependabot.yml`

- [ ] **Step 1: Rewrite the reusable test workflow** `.github/workflows/_test.yml`:
```yaml
name: Build & Test
on:
  workflow_call:

permissions:
  contents: read

jobs:
  build-test:
    name: Build & Test (${{ matrix.os }})
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    permissions:
      contents: read
      checks: write
      pull-requests: write
    steps:
      - name: Checkout
        uses: actions/checkout@692973e3d937129bcbf40652eb9f2f61becf3332 # v4

      - name: Setup .NET
        uses: actions/setup-dotnet@6bd8b7f7774af54e05809fcc5431931b3eb1ddee # v4
        with:
          dotnet-version: '10.0.x'

      - name: Cache NuGet
        uses: actions/cache@0c45773b623bea8c8e75f6c82b208c3cf94ea4f9 # v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Build.props') }}
          restore-keys: ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Test
        run: >
          dotnet test -c Release --no-build
          --logger "trx;LogFileName=test-results.trx"
          --logger "GitHubActions"
          --results-directory ${{ github.workspace }}/TestResults
          --collect:"XPlat Code Coverage"

      # --- Reporting (single OS leg to avoid duplicate comments) ---
      - name: Publish test results
        if: always() && runner.os == 'Linux'
        uses: EnricoMi/publish-unit-test-result-action@82082dac68ad6a19d980f8ce817e108b9f496c2a # v2.17.1
        with:
          check_name: "Test Results"
          trx_files: "${{ github.workspace }}/TestResults/**/*.trx"

      - name: Coverage report
        if: always() && runner.os == 'Linux'
        uses: danielpalme/ReportGenerator-GitHub-Action@e3af7259842d9c814021ea121f85526e0872b25f # v5.3.9
        with:
          reports: "${{ github.workspace }}/TestResults/**/coverage.cobertura.xml"
          targetdir: "${{ github.workspace }}/coveragereport"
          reporttypes: "Cobertura;MarkdownSummaryGithub"

      - name: Write coverage to job summary
        if: always() && runner.os == 'Linux'
        shell: bash
        run: cat "${{ github.workspace }}/coveragereport/SummaryGithub.md" >> "$GITHUB_STEP_SUMMARY" || true

      - name: Coverage PR comment
        if: always() && runner.os == 'Linux' && github.event_name == 'pull_request'
        uses: marocchino/sticky-pull-request-comment@331f8f5b4215f0445d3c07b4967662a32a2d3e31 # v2
        with:
          header: coverage
          recreate: true
          path: "${{ github.workspace }}/coveragereport/SummaryGithub.md"

      - name: Upload test artifacts
        if: always() && runner.os == 'Linux'
        uses: actions/upload-artifact@50769540e7f4bd5e21e526ee35c689e35e0d6874 # v4
        with:
          name: test-results
          path: |
            ${{ github.workspace }}/TestResults/**/*.trx
            ${{ github.workspace }}/coveragereport/**
          retention-days: 7
```

- [ ] **Step 2: Create the caller workflow** `.github/workflows/ci.yml`:
```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  test:
    uses: ./.github/workflows/_test.yml
    permissions:
      contents: read
      checks: write
      pull-requests: write
```

- [ ] **Step 3: Create CodeQL workflow** `.github/workflows/codeql.yml`:
```yaml
name: CodeQL
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    - cron: '27 4 * * 1'

permissions:
  contents: read

jobs:
  analyze:
    name: Analyze (C#)
    runs-on: ubuntu-latest
    permissions:
      security-events: write
      contents: read
      actions: read
    steps:
      - name: Checkout
        uses: actions/checkout@692973e3d937129bcbf40652eb9f2f61becf3332 # v4
      - name: Setup .NET
        uses: actions/setup-dotnet@6bd8b7f7774af54e05809fcc5431931b3eb1ddee # v4
        with:
          dotnet-version: '10.0.x'
      - name: Initialize CodeQL
        uses: github/codeql-action/init@afb54ba388a7dca6ecae48f608c4ff05ff4cc77a # v3
        with:
          languages: csharp
          build-mode: manual
      - name: Build
        run: dotnet build -c Release
      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@afb54ba388a7dca6ecae48f608c4ff05ff4cc77a # v3
        with:
          category: "/language:csharp"
```

- [ ] **Step 4: Create Dependabot config** `.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10
    commit-message:
      prefix: "build"
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
    commit-message:
      prefix: "ci"
```

- [ ] **Step 5: Validate YAML locally**

Run:
```bash
cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver
for f in .github/workflows/ci.yml .github/workflows/_test.yml .github/workflows/codeql.yml .github/dependabot.yml; do
  python3 -c "import sys,yaml; yaml.safe_load(open('$f')); print('OK', '$f')" ; done
```
Expected: `OK` for all four.

- [ ] **Step 6: Commit + PR**

```bash
git add .github/
git commit -m "ci: rework CI into caller+reusable workflow with PR test reporting, coverage, matrix, CodeQL, dependabot

- Adds pull_request trigger so reporting actually runs
- PR test-result check + sticky coverage comment + \$GITHUB_STEP_SUMMARY
- Cross-platform matrix (ubuntu/windows/macos); reporting from the Linux leg
- Least-privilege permissions; concurrency cancel-in-progress
- Removes the stale PinguApps.Appwrite coverage filter
- Adds CodeQL (C#) and Dependabot (nuget + actions)"
git push -u origin ci/test-reporting
gh pr create --base main --title "ci: PR test reporting, coverage, cross-platform matrix, CodeQL" \
  --body "Fixes the broken reporting (no pull_request trigger; stale assembly filter). Adds a proper caller workflow, \$GITHUB_STEP_SUMMARY, sticky coverage comment, cross-platform matrix, CodeQL and Dependabot."
```

- [ ] **Step 7: Verify reporting renders on the PR (evidence, not assertion)**

After CI runs, confirm on the PR: a **"Test Results"** check with 194 passed, a **coverage** sticky
comment, and a coverage table in the run's **Summary** tab. Run:
```bash
gh pr checks --watch
gh run view --log | grep -i "passed\|failed" | head
```
Expected: green checks; test totals visible. If the coverage glob finds nothing, adjust the
`reports:` path to the actual `coverage.cobertura.xml` location shown in the run log, and re-push.

---

## Task 3: OSS config + repo lockdown (PR `chore/oss-config`)

**Branch:** `git checkout -b chore/oss-config main` (after Task 2 merges).

**Files:**
- Create: `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`
- Create: `.github/CODEOWNERS`, `.github/FUNDING.yml`, `.github/PULL_REQUEST_TEMPLATE.md`
- Create: `.github/ISSUE_TEMPLATE/bug_report.yml`, `feature_request.yml`, `porting_gap.yml`, `config.yml`
- Post-merge: apply repo settings via `gh api`

- [ ] **Step 1: `CODE_OF_CONDUCT.md`** — fetch the official Contributor Covenant v2.1 and set the
  contact. Run:
```bash
cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver
curl -fsSL https://raw.githubusercontent.com/EthicalSource/contributor_covenant/release/content/version/2/1/code_of_conduct.md \
  | sed 's/\[INSERT CONTACT METHOD\]/dan@dbhq.uk/g' > CODE_OF_CONDUCT.md
grep -c "dan@dbhq.uk" CODE_OF_CONDUCT.md   # expect >= 1
```
If offline, write the Contributor Covenant 2.1 text manually with the contact `dan@dbhq.uk`.

- [ ] **Step 2: `CONTRIBUTING.md`**:
```markdown
# Contributing to UniswapSharp

Thanks for your interest! UniswapSharp is a faithful C#/.NET port of the official Uniswap
TypeScript SDKs (`@uniswap/sdk-core`, `@uniswap/v3-sdk`). Correctness against upstream is the
top priority.

## Development setup

- .NET 10 SDK (the project targets `net10.0`).
- Build: `dotnet build -c Release`
- Test: `dotnet test -c Release`
- Format: `dotnet format UniswapSharp.sln` (CI runs `--verify-no-changes`).

## Workflow (we always use PRs)

1. Branch off `main`: `git checkout -b <type>/<short-desc>` (`feat/`, `fix/`, `chore/`, `docs/`, `ci/`, `test/`).
2. Keep commits small and use Conventional Commit messages (`feat:`, `fix:`, `docs:` …).
3. Open a PR into `main`. CI must be green; keep history linear (squash-merge).
4. No direct pushes to `main`.

## Porting methodology (test-first)

This is a line-for-line port. When implementing or fixing anything:

1. Find the upstream source of truth first — `Uniswap/sdks`, packages `sdk-core` / `v3-sdk`.
   Read the matching `.ts` **and** its `.test.ts`. See [docs/PORTING.md](docs/PORTING.md).
2. Port the upstream test cases to xUnit first; watch them fail.
3. Implement until green. Match numeric behaviour **to the digit** — `BigInteger` / `BigRational`
   only; never floating point in protocol math.
4. Update the mapping table in `docs/PORTING.md`.

## Definition of done for a ported module

- [ ] Upstream `.ts` ported to C# with matching public surface (PascalCase).
- [ ] Upstream `.test.ts` cases ported to xUnit and passing.
- [ ] `docs/PORTING.md` mapping row updated (status).
- [ ] `dotnet format --verify-no-changes` clean; CI green.

By contributing you agree your work is licensed under the repository's MIT license.
```

- [ ] **Step 3: `SECURITY.md`**:
```markdown
# Security Policy

UniswapSharp produces calldata and performs protocol math for on-chain, financial transactions.
Please treat security seriously.

## Reporting a vulnerability

**Do not open a public issue for security problems.** Email **dan@dbhq.uk** with details and, if
possible, a reproduction. You'll get an acknowledgement within 5 business days. Please allow
reasonable time for a fix before public disclosure.

## Scope & disclaimer

This library is provided under the MIT license, **as is**, with no warranty. It is **not
affiliated with Uniswap Labs**. Always verify generated calldata and computed amounts against an
independent source before broadcasting transactions. Numeric parity with upstream is validated by
tests but does not constitute a security audit.

## Supported versions

Until `1.0.0`, only the latest published version receives fixes.
```

- [ ] **Step 4: `.github/CODEOWNERS`**:
```
# Default owner for everything in this repo
*       @grinidx
```

- [ ] **Step 5: `.github/FUNDING.yml`**:
```yaml
github: [grinidx]
```

- [ ] **Step 6: `.github/PULL_REQUEST_TEMPLATE.md`**:
```markdown
## What & why

<!-- Short description. Link the upstream .ts/.test.ts if this is a port. -->

## Checklist

- [ ] Upstream reference read (`docs/PORTING.md`) — if this is a port
- [ ] Tests added/ported and passing (`dotnet test -c Release`)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] No floating point introduced in protocol math
- [ ] `docs/PORTING.md` mapping updated (if applicable)
- [ ] CHANGELOG updated (for user-facing changes)
```

- [ ] **Step 7: Issue templates.** Create `.github/ISSUE_TEMPLATE/config.yml`:
```yaml
blank_issues_enabled: false
contact_links:
  - name: Security report
    url: https://github.com/grinidx/UniswapSharp/security/policy
    about: Please report vulnerabilities privately per SECURITY.md, not as an issue.
```
`.github/ISSUE_TEMPLATE/bug_report.yml`:
```yaml
name: Bug report
description: Something is incorrect or crashes
labels: [bug]
body:
  - type: textarea
    id: what-happened
    attributes:
      label: What happened?
      description: Include the smallest code snippet that reproduces it.
    validations:
      required: true
  - type: input
    id: version
    attributes:
      label: UniswapSharp version / commit
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: Expected vs actual
      description: If it's a numeric/parity issue, include the upstream value you expected.
    validations:
      required: true
```
`.github/ISSUE_TEMPLATE/feature_request.yml`:
```yaml
name: Feature request
description: Suggest an addition or improvement
labels: [enhancement]
body:
  - type: textarea
    id: proposal
    attributes:
      label: Proposal
    validations:
      required: true
  - type: input
    id: upstream
    attributes:
      label: Upstream reference (if applicable)
      description: Link to the @uniswap/* module this relates to.
```
`.github/ISSUE_TEMPLATE/porting_gap.yml`:
```yaml
name: Porting gap
description: A module/behaviour that differs from or is missing vs upstream
labels: [porting]
body:
  - type: input
    id: module
    attributes:
      label: Upstream module
      description: e.g. sdks/v3-sdk/src/utils/priceTickConversions.ts
    validations:
      required: true
  - type: textarea
    id: gap
    attributes:
      label: The gap
      description: What upstream does vs what UniswapSharp does (with the failing value if numeric).
    validations:
      required: true
```

- [ ] **Step 8: `CHANGELOG.md`** (Keep a Changelog):
```markdown
# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Repository foundations: CI with PR test reporting + coverage, CodeQL, Dependabot,
  community-health files, contributor & porting guides, NuGet packaging.

### Fixed
- `CurrencyAmount.ToExact()` now honours its format string (trims trailing zeros).
```

- [ ] **Step 9: Commit + PR**

```bash
git add CODE_OF_CONDUCT.md CONTRIBUTING.md SECURITY.md CHANGELOG.md .github/
git commit -m "chore: add community-health files, templates, CODEOWNERS, funding, changelog"
git push -u origin chore/oss-config
gh pr create --base main --title "chore: OSS community-health files + templates" \
  --body "Adds CODE_OF_CONDUCT, CONTRIBUTING (with porting methodology), SECURITY, CHANGELOG, CODEOWNERS, FUNDING, PR + issue templates."
```

- [ ] **Step 10: After merge — lock down repo settings via `gh api` (idempotent, read-back)**

Run (each command prints its result):
```bash
REPO=grinidx/UniswapSharp

# 1) Actions default token = read-only; disallow Actions approving PRs
gh api -X PUT repos/$REPO/actions/permissions/workflow \
  -f default_workflow_permissions=read -F can_approve_pull_request_reviews=false
gh api repos/$REPO/actions/permissions/workflow   # read-back

# 2) Secret scanning + push protection + Dependabot security updates
gh api -X PATCH repos/$REPO \
  -f 'security_and_analysis[secret_scanning][status]=enabled' \
  -f 'security_and_analysis[secret_scanning_push_protection][status]=enabled'
gh api -X PUT repos/$REPO/vulnerability-alerts        # Dependabot alerts
gh api -X PUT repos/$REPO/automated-security-fixes    # Dependabot security PRs

# 3) Branch protection on main (require CI check; no force-push/deletion; linear history)
gh api -X PUT repos/$REPO/branches/main/protection --input - <<'JSON'
{
  "required_status_checks": { "strict": true, "contexts": ["test"] },
  "enforce_admins": false,
  "required_pull_request_reviews": { "required_approving_review_count": 0 },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": true
}
JSON
gh api repos/$REPO/branches/main/protection | python3 -c "import sys,json;d=json.load(sys.stdin);print('linear:',d['required_linear_history']['enabled'],'force:',d['allow_force_pushes']['enabled'])"
```
Expected: workflow token `read`; branch protection shows `linear: True force: False`. If the
`test` context name doesn't match the actual check, list checks with
`gh api repos/$REPO/commits/main/check-runs -q '.check_runs[].name'` and update `contexts`.
Surface any command that errors (e.g. missing token scope) with the exact manual step.

---

## Task 4: Docs — README, CLAUDE.md, PORTING, LICENSE (PR `docs/readme-and-porting`)

**Branch:** `git checkout -b docs/readme-and-porting main` (after Task 3 merges).

**Files:**
- Create: `README.md`, `docs/PORTING.md`, `NOTICE`
- Modify: `LICENSE`, `CLAUDE.md`

- [ ] **Step 1: Update `LICENSE`** to dual copyright (keep MIT body). Replace the top copyright block:
```
MIT License

Copyright (c) 2021 Uniswap Labs (original TypeScript SDKs — https://github.com/Uniswap/sdks)
Copyright (c) 2024-2026 Diffused Networks (C#/.NET port — UniswapSharp)
```
(Leave the standard MIT permission/warranty paragraphs unchanged.)

- [ ] **Step 2: Create `NOTICE`**:
```
UniswapSharp
Copyright (c) 2024-2026 Diffused Networks

This product is a derivative work — a C#/.NET port of the official Uniswap SDKs
(https://github.com/Uniswap/sdks), specifically the @uniswap/sdk-core and @uniswap/v3-sdk
packages, which are licensed under the MIT License, Copyright (c) 2021 Uniswap Labs.

UniswapSharp is not affiliated with, endorsed by, or sponsored by Uniswap Labs.
```

- [ ] **Step 3: Draft the README quickstart example and verify it compiles.** Create a throwaway
  project that references the library and contains the snippet:
```bash
cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver
mkdir -p /tmp/readmecheck && cd /tmp/readmecheck
dotnet new console -o app >/dev/null
dotnet add app reference "$OLDPWD/src/UniswapSharp/UniswapSharp.csproj" >/dev/null
```
Put this in `/tmp/readmecheck/app/Program.cs`:
```csharp
using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Constants;

// 1. Define tokens (Ethereum mainnet)
var usdc = new Token(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
var weth = new Token(1, "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", 18, "WETH", "Wrapped Ether");

// 2. Deterministic V3 pool address — no RPC required
string poolAddress = Pool.GetAddress(usdc, weth, FeeAmount.MEDIUM);
Console.WriteLine($"USDC/WETH 0.3% pool: {poolAddress}");

// 3. Construct a pool at a chosen price (exact rational math, no floating point)
var sqrtPriceX96 = EncodeSqrtRatioX96.Encode(BigInteger.Parse("2000000000"), BigInteger.Parse("1000000000000000000000"));
int tick = TickMath.GetTickAtSqrtRatio(sqrtPriceX96);
var pool = new Pool(usdc, weth, FeeAmount.MEDIUM, sqrtPriceX96, BigInteger.Zero, tick);

// 4. Read a price with the desired significant digits
Console.WriteLine($"1 WETH = {pool.PriceOf(weth).ToSignificant(6)} USDC");
```
Run: `dotnet run --project app`
Expected: prints a checksummed pool address and a price line. **If it does not compile/run, fix the
snippet against the real API until it does** (this is the compile-verification gate). Record the
final working snippet for the README, then `rm -rf /tmp/readmecheck`.

- [ ] **Step 4: Create `README.md`** using the verified snippet:
```markdown
# UniswapSharp

[![CI](https://github.com/grinidx/UniswapSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/grinidx/UniswapSharp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)

A C#/.NET port of the official Uniswap SDKs — the [`@uniswap/sdk-core`](https://github.com/Uniswap/sdks/tree/main/sdks/sdk-core)
and [`@uniswap/v3-sdk`](https://github.com/Uniswap/sdks/tree/main/sdks/v3-sdk) TypeScript packages.
Model currencies, tokens, pools, positions, routes and trades; run the tick / sqrt-price / swap /
liquidity math; and encode calldata for the Uniswap V3 periphery — all with exact `BigInteger` /
`BigRational` arithmetic (no floating point in protocol math).

> **Status:** V3 core (entities + math) is ported and unit-tested against upstream. A few calldata
> builders are being completed; not yet published to NuGet. See [docs/PORTING.md](docs/PORTING.md).

## Install

```bash
dotnet add package UniswapSharp
```
_(Available once the first version is published; until then, build from source.)_

## Quickstart

```csharp
<PASTE THE VERIFIED SNIPPET FROM STEP 3 HERE>
```

Full swap quoting and position math work against tick data; see the tests under
`test/UniswapSharp.Testing/V3` for end-to-end examples.

## Building & testing

```bash
dotnet build -c Release
dotnet test  -c Release
# If only a newer runtime is installed:
dotnet test -c Release
```

## How it maps to upstream

Namespaces and file names deliberately mirror the TypeScript so the two read side by side:
`@uniswap/sdk-core` → `UniswapSharp.Core`, `@uniswap/v3-sdk` → `UniswapSharp.V3`. The full
file-by-file mapping and porting methodology are in [docs/PORTING.md](docs/PORTING.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). We work test-first, match upstream to the digit, and land
everything through PRs. Please also read the [Code of Conduct](CODE_OF_CONDUCT.md) and
[Security Policy](SECURITY.md).

## License & attribution

MIT — see [LICENSE](LICENSE). UniswapSharp is a derivative work of the MIT-licensed
[`Uniswap/sdks`](https://github.com/Uniswap/sdks) (© Uniswap Labs) and is **not affiliated with
Uniswap Labs**. This software is provided as is; verify calldata and amounts independently before
broadcasting on-chain transactions.
```

- [ ] **Step 5: Create `docs/PORTING.md`.** Generate the file-by-file mapping seed, then hand-fill status:
```bash
cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver
echo "Upstream v3-sdk src files:"; find /home/devops/uniswap-sdks-official/sdks/v3-sdk/src -name '*.ts' ! -name '*.test.ts' | sed 's#.*/v3-sdk/src/##' | sort
echo "Our V3 files:"; find src/UniswapSharp/V3 -name '*.cs' | sed 's#src/UniswapSharp/##' | sort
```
Write `docs/PORTING.md` with these sections (fill the mapping table from the output above; mark each
row ported / partial / stub / test-ported):
```markdown
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
| `*.test.ts` (mocha/jest) | xUnit `[Fact]`/`[Theory]` + FluentAssertions |
| named exports (camelCase) | public members (PascalCase) |
**Rule:** never use floating point in protocol math; match upstream numeric output to the digit.

## 4. File-by-file mapping (v3-sdk)
| Upstream `sdks/v3-sdk/src/…` | UniswapSharp `src/UniswapSharp/V3/…` | Tests ported | Status |
|---|---|---|---|
| … (fill from the command output above) … | … | … | … |

### Outstanding stubs (Phase B)
| C# file | Upstream reference |
|---|---|
| `V3/SwapQuoter.cs` | `quoter.ts` (+ `quoter.test.ts`) |
| `V3/NonfungiblePositionManager.cs` | `nonfungiblePositionManager.ts` |
| `V3/Payments.cs` | `payments.ts` |
| `V3/Utils/PositionLibrary.cs` (`SubIn256`) | `utils/tickLibrary.ts` (`subIn256`) |
| `V3/Utils/PriceTick.cs` | `utils/priceTickConversions.ts` |

## 5. Porting workflow (test-first)
1. Read upstream `.ts` + `.test.ts`. 2. Port the test cases to xUnit; watch them fail.
3. Implement until green, matching numbers to the digit. 4. Update the table above. 5. PR into `main`.

## 6. Intentional divergences
- `CurrencyAmount.ToExact` formats via `decimal` + a .NET format string (invariant culture) rather
  than Decimal.js `toFixed`. (Phase B will harden precision test-first.)
- _(append new entries as they arise)_

## 7. Re-syncing with upstream
Record the new upstream commit, `git -C /home/devops/uniswap-sdks-official log --oneline <old>..<new> -- sdks/v3-sdk sdks/sdk-core`,
port the deltas test-first, and bump the pinned commit above.
```

- [ ] **Step 6: Upgrade `CLAUDE.md`.** Append a "Software-engineering process" section after
  "Conventions" and fix the outstanding-work note (ToExact is now fixed). Add:
```markdown
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
```
Then edit the "Outstanding work" item #1 to read: *"`CurrencyAmount.ToExact` trailing-zero
formatting — FIXED; Phase B hardens it test-first against the full upstream `CurrencyAmount` suite."*

- [ ] **Step 7: Verify links + still-green, commit + PR**

```bash
dotnet test -c Release   # expect 194 passed
git add README.md docs/PORTING.md NOTICE LICENSE CLAUDE.md
git commit -m "docs: add README + porting guide, upgrade CLAUDE.md, add Uniswap Labs attribution"
git push -u origin docs/readme-and-porting
gh pr create --base main --title "docs: README, PORTING guide, CLAUDE.md process, license attribution" \
  --body "Adds a compile-verified quickstart README, a full upstream↔repo porting guide, an SE-process section in CLAUDE.md, and dual-copyright MIT attribution to Uniswap Labs (+ NOTICE)."
```

---

## Task 5: NuGet packaging + release (PR `build/nuget-packaging`)

**Branch:** `git checkout -b build/nuget-packaging main` (after Task 4 merges).

**Files:**
- Create: `Directory.Build.props`, `.github/workflows/release.yml`
- Modify: `src/UniswapSharp/UniswapSharp.csproj`

- [ ] **Step 1: Create `Directory.Build.props`** at repo root (shared build + deterministic settings):
```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <!-- Do not pack anything unless a project opts in -->
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Add package metadata to** `src/UniswapSharp/UniswapSharp.csproj`. Set the first
  `PropertyGroup` and add package refs so it becomes:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>true</IsPackable>
    <PackageId>UniswapSharp</PackageId>
    <Authors>Diffused Networks</Authors>
    <Company>Diffused Networks</Company>
    <Description>A C#/.NET port of the official Uniswap SDKs (sdk-core and v3-sdk): tokens, pools, positions, routes, trades, tick/sqrt-price/swap math, and V3 periphery calldata — with exact BigInteger/BigRational arithmetic.</Description>
    <PackageTags>uniswap;defi;ethereum;v3;amm;web3;nethereum;dex</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/grinidx/UniswapSharp</PackageProjectUrl>
    <RepositoryUrl>https://github.com/grinidx/UniswapSharp</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <!-- Suppress missing-XML-doc warnings from becoming noise on a port -->
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ExtendedNumerics.BigRational" Version="2023.1000.2.328" />
    <PackageReference Include="Nethereum.ABI" Version="4.21.4" />
    <PackageReference Include="Nethereum.Contracts" Version="4.21.4" />
    <PackageReference Include="Nethereum.Util" Version="4.21.4" />
    <PackageReference Include="Nethereum.Web3" Version="4.21.4" />
    <PackageReference Include="MinVer" Version="6.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Verify `dotnet pack` produces a valid package**

Run:
```bash
cd /home/devops/.paseo/worktrees/0khul817/skillful-beaver
dotnet pack src/UniswapSharp/UniswapSharp.csproj -c Release -o /tmp/nupkg
ls -1 /tmp/nupkg
unzip -l /tmp/nupkg/UniswapSharp.*.nupkg | grep -Ei "README.md|UniswapSharp.dll|UniswapSharp.xml|.nuspec"
```
Expected: a `UniswapSharp.<version>.nupkg` and a `.snupkg`; the nupkg contains `README.md`,
`lib/net10.0/UniswapSharp.dll`, `lib/net10.0/UniswapSharp.xml`, and a `.nuspec`. MinVer will pick a
version like `0.0.0-alpha.0.N` (no tag yet) — that's expected.

- [ ] **Step 4: Create the release workflow** `.github/workflows/release.yml`:
```yaml
name: Release
on:
  push:
    tags: ['v*']

permissions:
  contents: read

jobs:
  publish:
    name: Pack & publish to NuGet
    runs-on: ubuntu-latest
    permissions:
      contents: write   # attach assets to the GitHub Release
    steps:
      - name: Checkout
        uses: actions/checkout@692973e3d937129bcbf40652eb9f2f61becf3332 # v4
        with:
          fetch-depth: 0   # MinVer needs full history/tags

      - name: Setup .NET
        uses: actions/setup-dotnet@6bd8b7f7774af54e05809fcc5431931b3eb1ddee # v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore, build, test
        run: |
          dotnet restore
          dotnet build -c Release --no-restore
          dotnet test  -c Release --no-build

      - name: Pack
        run: dotnet pack src/UniswapSharp/UniswapSharp.csproj -c Release -o ./artifacts --no-build

      - name: Push to NuGet
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          dotnet nuget push "./artifacts/*.nupkg" --api-key "$NUGET_API_KEY" \
            --source https://api.nuget.org/v3/index.json --skip-duplicate

      - name: Attach artifacts to GitHub Release
        uses: softprops/action-gh-release@c95fe1489396fe8a9eb87c0abf8aa5b2ef267fda # v2
        with:
          files: ./artifacts/*
```

- [ ] **Step 5: Validate YAML + still-green**

Run:
```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml')); print('OK')"
dotnet test -c Release   # expect 194 passed
```
Expected: `OK`; 194 passed.

- [ ] **Step 6: Commit + PR**

```bash
git add Directory.Build.props src/UniswapSharp/UniswapSharp.csproj .github/workflows/release.yml
git commit -m "build: NuGet package metadata, MinVer + SourceLink, tag-driven release workflow"
git push -u origin build/nuget-packaging
gh pr create --base main --title "build: NuGet packaging + automated release on tag" \
  --body "Adds package metadata (README packed, SourceLink, symbols), MinVer tag-driven versioning, and a release.yml that packs + pushes to NuGet.org on a v* tag (NUGET_API_KEY secret). dotnet pack verified locally; 194/194 green."
```

- [ ] **Step 7: Document the publish prerequisite (no publish in Phase A)**

Note in the PR description that publishing requires the maintainer to add a `NUGET_API_KEY` repo
secret (`gh secret set NUGET_API_KEY`) and push a tag (`git tag v0.1.0-alpha && git push origin v0.1.0-alpha`).
`1.0.0` is reserved for V3-parity-complete (Phase B). Do **not** publish now.

---

## Self-review (completed against the spec)

- **Spec §4.1 git reconciliation** → Task 1 (+ branch retirement Step 8). ✔
- **§4.2 CLAUDE.md** → Task 4 Step 6. ✔
- **§4.3 README** → Task 4 Steps 3–4 (compile-verified). ✔
- **§4.4 CI + reporting + matrix + CodeQL + dependabot** → Task 2. ✔
- **§4.5 OSS files + repo lockdown** → Task 3. ✔
- **§4.6 PORTING guide** → Task 4 Step 5. ✔
- **§4.7 NuGet packaging + release** → Task 5. ✔
- **§4.8 license attribution** → Task 4 Steps 1–2. ✔
- **Extras (CodeQL, matrix, CHANGELOG, FUNDING)** → Task 2 / Task 3. ✔
- **Verification (§6)** → per-task verify steps + Task 2 Step 7 (reporting evidence), Task 5 Step 3 (pack). ✔

No placeholders; branch names, file paths, action SHAs, and `gh api` payloads are concrete.
`test` is the required status-check context name (the job id in `ci.yml`); Task 3 Step 10 includes a
fallback to correct it if GitHub reports a different check name.
```
