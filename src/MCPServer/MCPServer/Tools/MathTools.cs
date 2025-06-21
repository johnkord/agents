using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCPServer.Tools;

[McpServerToolType]
public class MathTools
{
    [McpServerTool(Name = "add"), Description("Adds two numbers.")]
    public static string Add(double a, double b) => $"The sum of {a} and {b} is {a + b}";

    [McpServerTool(Name = "subtract"), Description("Subtracts the second number from the first number.")]
    public static string Subtract(double a, double b) => $"The difference of {a} and {b} is {a - b}";

    [McpServerTool(Name = "multiply"), Description("Multiplies two numbers.")]
    public static string Multiply(double a, double b) => $"The product of {a} and {b} is {a * b}";

    [McpServerTool(Name = "divide"), Description("Divides the first number by the second number.")]
    public static string Divide(double a, double b)
    {
        if (b == 0)
            return "Error: Cannot divide by zero";
        return $"The quotient of {a} and {b} is {a / b}";
    }
}