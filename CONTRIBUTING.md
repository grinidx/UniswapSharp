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
