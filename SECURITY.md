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
