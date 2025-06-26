using MCPServer.Tools;
using Xunit;

namespace MCPServer.Tests;

public class MathToolsTests
{
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(5.5, 4.5, 10)]
    [InlineData(double.MaxValue, 0, double.MaxValue)]
    [InlineData(double.MinValue, 0, double.MinValue)]
    public void Add_ShouldReturnCorrectSum(double a, double b, double expected)
    {
        // Act
        var result = MathTools.Add(a, b);

        // Assert
        Assert.Contains(expected.ToString(), result);
        Assert.Contains("sum", result.ToLower());
    }

    [Theory]
    [InlineData(5, 3, 2)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, -1, 0)]
    [InlineData(10.5, 5.5, 5)]
    [InlineData(double.MaxValue, 0, double.MaxValue)]
    public void Subtract_ShouldReturnCorrectDifference(double a, double b, double expected)
    {
        // Act
        var result = MathTools.Subtract(a, b);

        // Assert
        Assert.Contains(expected.ToString(), result);
        Assert.Contains("difference", result.ToLower());
    }

    [Theory]
    [InlineData(2, 3, 6)]
    [InlineData(0, 5, 0)]
    [InlineData(-1, 1, -1)]
    [InlineData(2.5, 4, 10)]
    [InlineData(1, double.MaxValue, double.MaxValue)]
    public void Multiply_ShouldReturnCorrectProduct(double a, double b, double expected)
    {
        // Act
        var result = MathTools.Multiply(a, b);

        // Assert
        Assert.Contains(expected.ToString(), result);
        Assert.Contains("product", result.ToLower());
    }

    [Theory]
    [InlineData(6, 2, 3)]
    [InlineData(0, 1, 0)]
    [InlineData(-4, 2, -2)]
    [InlineData(10.5, 2.5, 4.2)]
    [InlineData(double.MaxValue, 1, double.MaxValue)]
    public void Divide_ShouldReturnCorrectQuotient(double a, double b, double expected)
    {
        // Act
        var result = MathTools.Divide(a, b);

        // Assert
        Assert.Contains(expected.ToString(), result);
        Assert.Contains("quotient", result.ToLower());
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(-1, 0)]
    [InlineData(double.MaxValue, 0)]
    [InlineData(double.MinValue, 0)]
    public void Divide_ByZero_ShouldReturnError(double a, double b)
    {
        // Act
        var result = MathTools.Divide(a, b);

        // Assert
        Assert.Contains("Error", result);
        Assert.Contains("Cannot divide by zero", result);
    }

    [Fact]
    public void Add_ShouldHandleSpecialValues()
    {
        // Test infinity cases
        var result = MathTools.Add(double.PositiveInfinity, 1);
        Assert.Contains("Infinity", result);

        result = MathTools.Add(double.NegativeInfinity, 1);
        Assert.Contains("Infinity", result);
    }

    [Fact]
    public void Multiply_ShouldHandleZero()
    {
        var result = MathTools.Multiply(0, double.MaxValue);
        Assert.Contains("0", result);
        Assert.Contains("product", result.ToLower());
    }

    [Fact]
    public void Divide_ShouldHandleInfinity()
    {
        var result = MathTools.Divide(double.PositiveInfinity, 1);
        Assert.Contains("Infinity", result);
        Assert.Contains("quotient", result.ToLower());
    }
}