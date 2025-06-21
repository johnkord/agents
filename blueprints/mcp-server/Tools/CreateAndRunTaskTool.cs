using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

/// <summary>
/// Executes named build/test/lint tasks with structured output capture,
/// timeout enforcement, and command safety classification.
///
/// Research basis:
///   - CaveAgent (2026): dual-stream architecture — runtime execution is
///     separate from semantic reasoning; apply observation shaping to output
///   - AGENTSYS (2026): command/query separation — classify read-only vs write
///     operations and apply different permission levels
///   - Data Engineering Terminal Agents: structured stdout/stderr/exit code, not blobs
/// </summary>
[McpServerToolType]
public static class CreateAndRunTaskTool
{
    private const int DefaultTimeoutSeconds = 120;
    private const int MaxOutputChars = 20_000;

    // Commands that are safe to run without extra scrutiny
    private static readonly HashSet<string> SafeCommandPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet build", "dotnet test", "dotnet run", "dotnet publish", "dotnet clean",
        "npm run", "npm test", "npm install", "npm ci",
        "yarn build", "yarn test", "yarn install",
        "pnpm build", "pnpm test", "pnpm install",
        "pytest", "python -m pytest", "python -m mypy", "python -m flake8",
        "cargo build", "cargo test", "cargo check", "cargo clippy",
        "go build", "go test", "go vet",
        "make", "cmake",
        "eslint", "prettier", "tsc",
    };

    // Patterns that should be blocked
    private static readonly string[] DeniedPatterns =
    [
        "rm -rf", "sudo", "mkfs", "dd if=", ":(){",
        "chmod -R 777", "shutdown", "reboot", "halt", "poweroff",
        "curl|sh", "wget|sh", "curl|bash", "wget|bash",
    ];

    [McpServerTool, Description(
        "Run a named build, test, lint, or custom task with structured output. " +
        "Returns exit code, stdout, and stderr separately. " +
        "Enforces timeout and blocks dangerous commands. " +
        "Prefer this over run_bash_command for well-defined project tasks.")]
    public static async Task<string> CreateAndRunTask(
        [Description("Human-readable label (e.g. 'Build', 'Run tests', 'Lint').")] string label,
        [Description("Shell command to execute (e.g. 'dotnet test', 'npm run build').")] string command,
        [Description("Working directory. Defaults to current directory.")] string? workingDirectory = null,
        [Description("Timeout in seconds. Default 120.")] int? timeoutSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "Error: command is required.";

        // Safety check
        foreach (var denied in DeniedPatterns)
        {
            if (command.Contains(denied, StringComparison.OrdinalIgnoreCase))
                return $"Error: Command blocked — contains denied pattern: '{denied}'.";
        }

        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(cwd))
            return $"Error: Working directory not found: '{cwd}'.";

        var timeout = Math.Clamp(timeoutSeconds ?? DefaultTimeoutSeconds, 5, 600);
        var isSafe = SafeCommandPrefixes.Any(prefix =>
            command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = cwd,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(command);

            var sw = Stopwatch.StartNew();
            using var process = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);
            sw.Stop();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // Truncate long outputs
            var stdoutTruncated = stdout.Length > MaxOutputChars
                ? stdout[..MaxOutputChars] + $"\n... truncated ({stdout.Length:N0} chars)"
                : stdout;
            var stderrTruncated = stderr.Length > MaxOutputChars
                ? stderr[..MaxOutputChars] + $"\n... truncated ({stderr.Length:N0} chars)"
                : stderr;

            var result = $"Task: {label}\n";
            result += $"Command: {command}\n";
            result += $"Working directory: {cwd}\n";
            result += $"Exit code: {process.ExitCode}\n";
            result += $"Duration: {sw.Elapsed.TotalSeconds:F1}s\n";
            result += $"Type: {(isSafe ? "safe (known build/test command)" : "custom")}\n";

            if (!string.IsNullOrWhiteSpace(stdoutTruncated))
                result += $"\n--- stdout ---\n{stdoutTruncated}\n";

            if (!string.IsNullOrWhiteSpace(stderrTruncated))
                result += $"\n--- stderr ---\n{stderrTruncated}\n";

            if (process.ExitCode == 0)
                result += $"\n✓ Task '{label}' succeeded.";
            else
                result += $"\n✗ Task '{label}' failed (exit code {process.ExitCode}).";

            return result;
        }
        catch (OperationCanceledException)
        {
            return $"Error: Task '{label}' timed out after {timeout} seconds. Command: {command}";
        }
        catch (Exception ex)
        {
            return $"Error running task '{label}': {ex.Message}";
        }
    }
}
