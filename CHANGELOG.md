# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Repository foundations: CI with PR test reporting + coverage, CodeQL, Dependabot,
  community-health files, contributor & porting guides, NuGet packaging.

### Changed
- Test assertions migrated from FluentAssertions to **AwesomeAssertions** (Apache-2.0
  community fork) to avoid FluentAssertions v8's commercial license. Test-only; no
  effect on the shipped package.

### Fixed
- `CurrencyAmount.ToExact()` now honours its format string (trims trailing zeros).
