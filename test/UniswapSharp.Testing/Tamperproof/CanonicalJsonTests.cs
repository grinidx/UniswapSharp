using System.Text;
using AwesomeAssertions;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Testing.Tamperproof;

// Ported from sdks/tamperproof-transactions/src/utils/canonicalJson.test.ts.
// JS object literals are modelled as Dictionary<string, object?> (insertion order irrelevant — the
// canonicalizer sorts keys); the JS `undefined` value is CanonicalJson.Undefined.
public class CanonicalJsonTests
{
    private static Dictionary<string, object?> Obj(params (string Key, object? Value)[] entries)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in entries)
        {
            d[k] = v;
        }

        return d;
    }

    [Fact]
    public void CanonicalStringify_SortsObjectKeys()
    {
        var input = Obj(("b", 2), ("a", 1));
        CanonicalJson.CanonicalStringify(input).Should().Be("{\"a\":1,\"b\":2}");
    }

    [Fact]
    public void CanonicalStringify_DropsUndefined()
    {
        var input = Obj(("a", 1), ("b", CanonicalJson.Undefined));
        CanonicalJson.CanonicalStringify(input).Should().Be("{\"a\":1}");
    }

    [Fact]
    public void CanonicalStringify_NestedObjectsSorted()
    {
        var input = Obj(("z", Obj(("b", 2), ("a", 1))), ("y", 0));
        CanonicalJson.CanonicalStringify(input).Should().Be("{\"y\":0,\"z\":{\"a\":1,\"b\":2}}");
    }

    [Fact]
    public void CanonicalStringify_CanonicalizesObjectsInArraysPreservingOrder()
    {
        var input = new object[]
        {
            Obj(("b", 2), ("a", 1)),
            Obj(("d", 4), ("c", 3)),
        };
        CanonicalJson.CanonicalStringify(input).Should().Be("[{\"a\":1,\"b\":2},{\"c\":3,\"d\":4}]");
    }

    [Fact]
    public void CanonicalStringify_PreservesNullBooleanNumberString()
    {
        var input = Obj(("n", null), ("t", true), ("f", false), ("num", 42), ("s", "x"));
        CanonicalJson.CanonicalStringify(input).Should().Be("{\"f\":false,\"n\":null,\"num\":42,\"s\":\"x\",\"t\":true}");
    }

    [Fact]
    public void SerializeRequestPayload_EncodesCanonicalJsonAsUtf8()
    {
        var payload = Obj(("method", "m"), ("params", Obj(("b", 2), ("a", 1))));
        byte[] expected = Encoding.UTF8.GetBytes(CanonicalJson.CanonicalStringify(payload));
        byte[] actual = CanonicalJson.SerializeRequestPayload(payload);
        actual.Should().Equal(expected);
    }

    [Fact]
    public void SerializeRequestPayload_DropsUndefinedBeforeEncoding()
    {
        var payload = Obj(("method", "m"), ("params", Obj(("a", 1), ("u", CanonicalJson.Undefined))));
        byte[] encoded = CanonicalJson.SerializeRequestPayload(payload);
        Encoding.UTF8.GetString(encoded).Should().Be("{\"method\":\"m\",\"params\":{\"a\":1}}");
    }
}
