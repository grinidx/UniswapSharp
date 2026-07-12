using System.Numerics;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>A permit2 nonce split into its word and bit position (uniswapx-sdk <c>SplitNonce</c>).</summary>
public sealed record SplitNonce(BigInteger Word, BigInteger BitPos);

/// <summary>Parameters to cancel one or more permit2 nonces (uniswapx-sdk <c>CancelParams</c>).</summary>
public sealed record CancelParams(BigInteger Word, BigInteger Mask);

/// <summary>
/// On-chain permit2 nonce lookup, injected into <see cref="NonceManager"/> so the deterministic logic can be
/// tested with a fake and does not require a live provider (mirrors upstream <c>permit2.nonceBitmap</c>).
/// </summary>
public interface INonceLookup
{
    /// <summary>Returns the permit2 nonce bitmap for <paramref name="address"/> at word <paramref name="word"/>.</summary>
    Task<BigInteger> NonceBitmapAsync(string address, BigInteger word);
}

/// <summary>Port of uniswapx-sdk <c>utils/NonceManager.ts</c>: tracks permit2 nonces for addresses.</summary>
public sealed class NonceManager
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

    private readonly INonceLookup _permit2;
    private readonly Dictionary<string, BigInteger> _currentWord = new();
    private readonly Dictionary<string, BigInteger> _currentBitmap = new();

    public NonceManager(INonceLookup permit2)
    {
        _permit2 = permit2;
    }

    /// <summary>Finds the next unused nonce, marks it used within this instance, and returns it.</summary>
    public async Task<BigInteger> UseNonceAsync(string address)
    {
        var (word, bitmap) = await GetNextOpenWordAsync(address);
        int bitPos = GetFirstUnsetBit(bitmap);

        _currentWord[address] = word;
        _currentBitmap[address] = SetBit(bitmap, bitPos);

        return BuildNonce(word, bitPos);
    }

    /// <summary>Returns whether <paramref name="nonce"/> has been used on chain for <paramref name="address"/>.</summary>
    public async Task<bool> IsUsedAsync(string address, BigInteger nonce)
    {
        var split = SplitNonce(nonce);
        BigInteger bitmap = await _permit2.NonceBitmapAsync(address, split.Word);
        return bitmap / BigInteger.Pow(2, (int)split.BitPos) % 2 == 1;
    }

    private async Task<(BigInteger Word, BigInteger Bitmap)> GetNextOpenWordAsync(string address)
    {
        BigInteger currentWord = _currentWord.TryGetValue(address, out var w) ? w : BigInteger.Zero;
        BigInteger bitmap = _currentBitmap.TryGetValue(address, out var b)
            ? b
            : await _permit2.NonceBitmapAsync(address, currentWord);

        while (bitmap == MaxUint256)
        {
            currentWord += 1;
            bitmap = await _permit2.NonceBitmapAsync(address, currentWord);
        }

        return (currentWord, bitmap);
    }

    // ---- pure helpers (uniswapx-sdk module functions) ----

    /// <summary>Splits a permit2 nonce into the word and bit position.</summary>
    public static SplitNonce SplitNonce(BigInteger nonce) => new(nonce / 256, nonce % 256);

    /// <summary>Builds a permit2 nonce from the given word and bit position.</summary>
    public static BigInteger BuildNonce(BigInteger word, int bitPos) => word * 256 + bitPos;

    /// <summary>Returns the position of the first unset bit, or -1 if all bits are set.</summary>
    public static int GetFirstUnsetBit(BigInteger bitmap)
    {
        for (int i = 0; i < 256; i++)
        {
            if (bitmap / BigInteger.Pow(2, i) % 2 == 0)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Returns <paramref name="bitmap"/> with the given bit set (no-op if already set).</summary>
    public static BigInteger SetBit(BigInteger bitmap, int bitPos)
    {
        BigInteger mask = BigInteger.Pow(2, bitPos);
        if (bitmap / mask % 2 == 1)
        {
            return bitmap;
        }
        return bitmap + mask;
    }

    /// <summary>Returns the parameters to cancel a single nonce.</summary>
    public static CancelParams GetCancelSingleParams(BigInteger nonceToCancel)
    {
        var split = SplitNonce(nonceToCancel);
        BigInteger mask = BigInteger.Pow(2, (int)split.BitPos);
        return new CancelParams(split.Word, mask);
    }

    /// <summary>Returns the parameters to cancel multiple nonces, grouped by word.</summary>
    public static List<CancelParams> GetCancelMultipleParams(IReadOnlyList<BigInteger> noncesToCancel)
    {
        var byWord = new SortedDictionary<BigInteger, List<SplitNonce>>();
        foreach (var nonce in noncesToCancel)
        {
            var split = SplitNonce(nonce);
            if (!byWord.TryGetValue(split.Word, out var list))
            {
                list = new List<SplitNonce>();
                byWord[split.Word] = list;
            }
            list.Add(split);
        }

        var result = new List<CancelParams>();
        foreach (var (word, splits) in byWord)
        {
            BigInteger mask = BigInteger.Zero;
            foreach (var split in splits)
            {
                mask |= BigInteger.Pow(2, (int)split.BitPos);
            }
            result.Add(new CancelParams(word, mask));
        }
        return result;
    }
}
