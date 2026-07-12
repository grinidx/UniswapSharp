# UniswapSharp

[![CI](https://github.com/grinidx/UniswapSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/grinidx/UniswapSharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/UniswapSharp.svg)](https://www.nuget.org/packages/UniswapSharp)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)

A C#/.NET port of the official Uniswap SDK monorepo — the [`Uniswap/sdks`](https://github.com/Uniswap/sdks)
TypeScript packages. Model currencies, tokens, pools, positions, routes and trades; run the tick /
sqrt-price / swap / liquidity math; build calldata for the V3/V4 periphery and the Universal Router;
and hash/sign Permit2 and UniswapX orders — all with exact `BigInteger` / `BigRational` arithmetic
(**no floating point in protocol math**).

Namespaces and file names deliberately mirror the upstream TypeScript so the two read side by side,
and behaviour is verified **to the digit** against the upstream `.test.ts` vectors.

> **Status:** the full monorepo surface is ported and green — **1,694 xUnit tests, 0 failing**, on
> Linux / Windows / macOS. See [docs/PORTING.md](docs/PORTING.md) for the file-by-file mapping,
> methodology, and every intentional divergence.

## Packages

Everything ships in the single **`UniswapSharp`** NuGet package, organised into namespaces that mirror
the upstream packages:

| Namespace | Upstream package | What's in it |
|---|---|---|
| `UniswapSharp.Core` | `@uniswap/sdk-core` | `Token`, `Ether`, `Fraction`/`Percent`/`Price`/`CurrencyAmount`, chain registry, addresses, WETH9 |
| `UniswapSharp.V2` | `@uniswap/v2-sdk` | `Pair` (CREATE2 + fee math), `Route`, `Trade`, `Router` |
| `UniswapSharp.V3` | `@uniswap/v3-sdk` | `Pool`, `Position`, `Route`, `Trade`, `Tick`, tick/sqrt-price/swap/liquidity math, periphery calldata |
| `UniswapSharp.V4` | `@uniswap/v4-sdk` | currency-based `Pool`/`Position`/`Route`/`Trade`, hooks, `V4Planner`, `PositionManager` |
| `UniswapSharp.Router` | `@uniswap/router-sdk` | mixed v2+v3+v4 routes, aggregated `Trade`, SwapRouter02 calldata |
| `UniswapSharp.UniversalRouter` | `@uniswap/universal-router-sdk` | `RoutePlanner`, `RouterTradeAdapter`, `SwapRouter`, signed-route EIP-712 |
| `UniswapSharp.UniswapX` | `@uniswap/uniswapx-sdk` | Dutch/Priority/Relay/V3/Hybrid orders, decay math, builders, trades, EIP-712 witness hashing |
| `UniswapSharp.Permit2` | `@uniswap/permit2-sdk` | `SignatureTransfer`, `AllowanceTransfer`, byte-exact EIP-712 typed-data encoder |
| `UniswapSharp.SmartWallet` | `@uniswap/smart-wallet-sdk` | ERC-7821 call planners + encoders |
| `UniswapSharp.LiquidityLauncher` | `@uniswap/liquidity-launcher-sdk` | launch-config math, CREATE2 poolId/salts, calldata encoding |
| `UniswapSharp.Flashtestations` | `@uniswap/flashtestations-sdk` | TEE workload-ID (keccak) + block verification (injectable RPC) |
| `UniswapSharp.Tamperproof` | `@uniswap/tamperproof-transactions` | EIP-7754 sign/verify (RSA/ECDSA/Ed25519), canonical JSON, DNS-over-HTTPS (injectable) |

## Install

```bash
dotnet add package UniswapSharp
```

Targets **.NET 10** (`net10.0`). Depends on Nethereum (ABI/keccak/CREATE2/EIP-712) and
ExtendedNumerics.BigRational for exact rational arithmetic.

## Quickstart

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

// 3. Construct a pool at a chosen price: 1000 WETH / 2,000,000 USDC reserves (~2000 USDC per WETH)
var sqrtPriceX96 = EncodeSqrtRatioX96.Encode(BigInteger.Parse("1000000000000000000000"), BigInteger.Parse("2000000000000"));
int tick = TickMath.GetTickAtSqrtRatio(sqrtPriceX96);
var pool = new Pool(usdc, weth, FeeAmount.MEDIUM, sqrtPriceX96, BigInteger.Zero, tick);

// 4. Read a price with the desired significant digits
Console.WriteLine($"1 WETH = {pool.PriceOf(weth).ToSignificant(6)} USDC");
```

Output:

```
USDC/WETH 0.3% pool: 0x8ad599c3A0ff1De082011EFDDc58f1908eb6e6D8
1 WETH = 2000 USDC
```

Swap quoting, position math, and calldata builders across all the namespaces above work the same way.
The test suite under `test/UniswapSharp.Testing/` mirrors the source tree and doubles as an
end-to-end example library — e.g. `V3/SwapRouterTests`, `V4/`, `Router/`, `UniversalRouter/`,
`UniswapX/`, and `Permit2/` show calldata and signing round-trips against the upstream vectors.

## Building & testing

```bash
dotnet build -c Release
dotnet test  -c Release
```

The projects target `net10.0` and require the .NET 10 SDK.

## How it maps to upstream

Each `UniswapSharp.*` namespace corresponds one-to-one with an upstream package (see the table above).
The complete file-by-file mapping, the test-first porting methodology, and every intentional
divergence (e.g. injectable RPC/DNS where upstream uses live network I/O) are documented in
[docs/PORTING.md](docs/PORTING.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). We work test-first, match upstream to the digit, and land
everything through PRs. Please also read the [Code of Conduct](CODE_OF_CONDUCT.md) and
[Security Policy](SECURITY.md).

## License & attribution

MIT — see [LICENSE](LICENSE). UniswapSharp is a derivative work of the MIT-licensed
[`Uniswap/sdks`](https://github.com/Uniswap/sdks) (© Uniswap Labs) and is **not affiliated with
Uniswap Labs**. This software is provided as is; verify calldata and amounts independently before
broadcasting on-chain transactions.
