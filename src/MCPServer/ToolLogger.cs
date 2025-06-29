namespace MCPServer.Logging;

public static class ToolLogger
{
    public static void LogStart(string tool) =>
        Console.WriteLine($"[{DateTime.UtcNow:O}] [TOOL START] {tool}");

    public static void LogEnd(string tool) =>
        Console.WriteLine($"[{DateTime.UtcNow:O}] [TOOL END]   {tool}");

    public static void LogError(string tool, Exception ex) =>
        Console.WriteLine($"[{DateTime.UtcNow:O}] [TOOL ERR ] {tool}: {ex.Message}");
}
