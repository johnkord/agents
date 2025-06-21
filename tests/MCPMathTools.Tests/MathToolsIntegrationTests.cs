using MCPServer.Tools;

namespace MCPMathTools.Tests;

public class MathToolsUnitTests
{
    [Fact]
    public void Add_ShouldReturnCorrectSum()
    {
        // Arrange
        var a = 5.0;
        var b = 3.0;
        var expected = "The sum of 5 and 3 is 8";

        // Act
        var result = MathTools.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Subtract_ShouldReturnCorrectDifference()
    {
        // Arrange
        var a = 10.0;
        var b = 4.0;
        var expected = "The difference of 10 and 4 is 6";

        // Act
        var result = MathTools.Subtract(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Multiply_ShouldReturnCorrectProduct()
    {
        // Arrange
        var a = 6.0;
        var b = 7.0;
        var expected = "The product of 6 and 7 is 42";

        // Act
        var result = MathTools.Multiply(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Divide_ShouldReturnCorrectQuotient()
    {
        // Arrange
        var a = 20.0;
        var b = 4.0;
        var expected = "The quotient of 20 and 4 is 5";

        // Act
        var result = MathTools.Divide(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Divide_WithZeroDivisor_ShouldReturnError()
    {
        // Arrange
        var a = 10.0;
        var b = 0.0;
        var expected = "Error: Cannot divide by zero";

        // Act
        var result = MathTools.Divide(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-5, 3, "The sum of -5 and 3 is -2")]
    [InlineData(0, 0, "The sum of 0 and 0 is 0")]
    [InlineData(1.5, 2.5, "The sum of 1.5 and 2.5 is 4")]
    [InlineData(100, -50, "The sum of 100 and -50 is 50")]
    public void Add_WithVariousInputs_ShouldReturnCorrectResults(double a, double b, string expected)
    {
        // Act
        var result = MathTools.Add(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(10, 5, "The difference of 10 and 5 is 5")]
    [InlineData(-10, -5, "The difference of -10 and -5 is -5")]
    [InlineData(0, 10, "The difference of 0 and 10 is -10")]
    [InlineData(3.7, 1.2, "The difference of 3.7 and 1.2 is 2.5")]
    public void Subtract_WithVariousInputs_ShouldReturnCorrectResults(double a, double b, string expected)
    {
        // Act
        var result = MathTools.Subtract(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(3, 4, "The product of 3 and 4 is 12")]
    [InlineData(-2, 5, "The product of -2 and 5 is -10")]
    [InlineData(0, 100, "The product of 0 and 100 is 0")]
    [InlineData(2.5, 4, "The product of 2.5 and 4 is 10")]
    public void Multiply_WithVariousInputs_ShouldReturnCorrectResults(double a, double b, string expected)
    {
        // Act
        var result = MathTools.Multiply(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(15, 3, "The quotient of 15 and 3 is 5")]
    [InlineData(-12, 4, "The quotient of -12 and 4 is -3")]
    [InlineData(7, 2, "The quotient of 7 and 2 is 3.5")]
    [InlineData(0, 5, "The quotient of 0 and 5 is 0")]
    public void Divide_WithValidDivisor_ShouldReturnCorrectResults(double a, double b, string expected)
    {
        // Act
        var result = MathTools.Divide(a, b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    public void Divide_WithZeroDivisor_ShouldAlwaysReturnError(double a, double b)
    {
        // Act
        var result = MathTools.Divide(a, b);

        // Assert
        Assert.Equal("Error: Cannot divide by zero", result);
    }
}