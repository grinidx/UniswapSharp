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

Full swap quoting and position math work against tick data; see the tests under
`test/UniswapSharp.Testing/V3` for end-to-end examples.

## Building & testing

```bash
dotnet build -c Release
dotnet test  -c Release
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
