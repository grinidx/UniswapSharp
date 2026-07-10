using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.Testing.Core.Entities.Fractions;

public class PercentTests
{
    [Fact]
    public void Constructor_DefaultsDenominatorToOne()
    {
        Assert.Equal(new Percent(1), new Percent(1, 1));
    }

    [Fact]
    public void Add_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(3, 100), new Percent(1, 100).Add(new Percent(2, 100)));
    }

    [Fact]
    public void Add_DifferentDenominators_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(150, 2500), new Percent(1, 25).Add(new Percent(2, 100)));
    }

    [Fact]
    public void Subtract_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(-1, 100), new Percent(1, 100).Subtract(new Percent(2, 100)));
    }

    [Fact]
    public void Subtract_DifferentDenominators_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(50, 2500), new Percent(1, 25).Subtract(new Percent(2, 100)));
    }

    [Fact]
    public void Multiply_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(2, 10000), new Percent(1, 100).Multiply(new Percent(2, 100)));
    }

    [Fact]
    public void Multiply_DifferentDenominators_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(2, 2500), new Percent(1, 25).Multiply(new Percent(2, 100)));
    }

    [Fact]
    public void Divide_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(100, 200), new Percent(1, 100).Divide(new Percent(2, 100)));
    }

    [Fact]
    public void Divide_DifferentDenominators_ReturnsCorrectPercent()
    {
        Assert.Equal(new Percent(100, 50), new Percent(1, 25).Divide(new Percent(2, 100)));
    }

    [Fact]
    public void ToSignificant_ReturnsValueScaledBy100()
    {
        Assert.Equal("1.54", new Percent(154, 10_000).ToSignificant(3));
    }

    [Fact]
    public void ToFixed_ReturnsValueScaledBy100()
    {
        Assert.Equal("1.54", new Percent(154, 10_000).ToFixed(2));
    }
}