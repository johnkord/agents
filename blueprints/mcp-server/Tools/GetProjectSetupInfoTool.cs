using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace McpServer.Tools;

/// <summary>
/// Detects project type(s) by scanning for marker files and returns actionable
/// setup information: detected type, config files, build/run/test commands,
/// package manager, and language version.
///
/// Research basis:
///   - Agent Skills Architecture (2026): progressive disclosure — return actionable
///     metadata, not overwhelming detail
///   - Theory of Code Space (2026): return confidence-ranked list (monorepos may
///     be multiple types simultaneously)
/// </summary>
[McpServerToolType]
public static class GetProjectSetupInfoTool
{
    [McpServerTool, Description(
        "Analyzes a directory to detect project type(s) and returns setup information " +
        "including detected frameworks, config files, build/run/test commands, and package manager. " +
        "Useful for understanding an unfamiliar project or verifying your assumptions about the tech stack.")]
    public static string GetProjectSetupInfo(
        [Description("Directory to analyze. Defaults to current working directory.")] string? directoryPath = null)
    {
        var dir = directoryPath ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir))
            return $"Error: Directory not found: '{dir}'.";

        var detections = new List<ProjectDetection>();

        // Check each detector against the directory
        foreach (var detector in Detectors)
        {
            var markerFiles = detector.MarkerFiles
                .Where(f => f.Contains('*')
                    ? Directory.GetFiles(dir, f, SearchOption.TopDirectoryOnly).Length > 0
                    : File.Exists(Path.Combine(dir, f)))
                .ToList();

            if (markerFiles.Count > 0)
            {
                detections.Add(new ProjectDetection
                {
                    Type = detector.Type,
                    Language = detector.Language,
                    FoundMarkers = markerFiles,
                    BuildCommand = detector.BuildCommand,
                    RunCommand = detector.RunCommand,
                    TestCommand = detector.TestCommand,
                    PackageManager = detector.PackageManager,
                    ConfigFiles = detector.ConfigFiles.Where(f => File.Exists(Path.Combine(dir, f))).ToList(),
                });
            }
        }

        if (detections.Count == 0)
        {
            // Fallback: list what files ARE in the directory to help the agent
            var topFiles = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Take(20)
                .ToList();
            return $"No recognized project type detected in '{dir}'.\nTop-level files: {string.Join(", ", topFiles)}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Project analysis for: {dir}");
        sb.AppendLine($"Detected {detections.Count} project type(s):\n");

        foreach (var det in detections)
        {
            sb.AppendLine($"  [{det.Type}] ({det.Language})");
            sb.AppendLine($"    Marker files: {string.Join(", ", det.FoundMarkers)}");
            if (det.ConfigFiles.Count > 0)
                sb.AppendLine($"    Config files: {string.Join(", ", det.ConfigFiles)}");
            sb.AppendLine($"    Package manager: {det.PackageManager}");
            sb.AppendLine($"    Build: {det.BuildCommand}");
            sb.AppendLine($"    Run: {det.RunCommand}");
            sb.AppendLine($"    Test: {det.TestCommand}");
            sb.AppendLine();
        }

        // Detect monorepo structure
        var subdirs = Directory.GetDirectories(dir)
            .Where(d => !Path.GetFileName(d).StartsWith('.'))
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                return name is not ("bin" or "obj" or "node_modules" or "packages" or ".git" or ".vs");
            })
            .ToList();

        if (subdirs.Count > 3 && detections.Count >= 2)
            sb.AppendLine("  Note: Multiple project types detected — this may be a monorepo.");

        return sb.ToString().TrimEnd();
    }

    // ── Project type detectors ─────────────────────────────────────────────

    private sealed record ProjectDetection
    {
        public string Type { get; init; } = "";
        public string Language { get; init; } = "";
        public List<string> FoundMarkers { get; init; } = [];
        public string BuildCommand { get; init; } = "";
        public string RunCommand { get; init; } = "";
        public string TestCommand { get; init; } = "";
        public string PackageManager { get; init; } = "";
        public List<string> ConfigFiles { get; init; } = [];
    }

    private sealed record Detector
    {
        public string Type { get; init; } = "";
        public string Language { get; init; } = "";
        public string[] MarkerFiles { get; init; } = [];
        public string BuildCommand { get; init; } = "";
        public string RunCommand { get; init; } = "";
        public string TestCommand { get; init; } = "";
        public string PackageManager { get; init; } = "";
        public string[] ConfigFiles { get; init; } = [];
    }

    private static readonly Detector[] Detectors =
    [
        new()
        {
            Type = "dotnet", Language = "C#",
            MarkerFiles = ["*.sln", "*.csproj", "*.slnx"],
            BuildCommand = "dotnet build",
            RunCommand = "dotnet run",
            TestCommand = "dotnet test",
            PackageManager = "NuGet",
            ConfigFiles = ["appsettings.json", "appsettings.Development.json", "Directory.Build.props", "global.json", "nuget.config"],
        },
        new()
        {
            Type = "node", Language = "JavaScript/TypeScript",
            MarkerFiles = ["package.json"],
            BuildCommand = "npm run build",
            RunCommand = "npm start",
            TestCommand = "npm test",
            PackageManager = "npm/yarn/pnpm",
            ConfigFiles = ["tsconfig.json", ".eslintrc.json", ".prettierrc", "vite.config.ts", "webpack.config.js", "jest.config.js"],
        },
        new()
        {
            Type = "python", Language = "Python",
            MarkerFiles = ["pyproject.toml", "setup.py", "setup.cfg", "requirements.txt"],
            BuildCommand = "pip install -e .",
            RunCommand = "python -m <module>",
            TestCommand = "pytest",
            PackageManager = "pip/uv/poetry",
            ConfigFiles = ["pyproject.toml", "setup.cfg", ".flake8", "mypy.ini", "tox.ini", "Pipfile"],
        },
        new()
        {
            Type = "rust", Language = "Rust",
            MarkerFiles = ["Cargo.toml"],
            BuildCommand = "cargo build",
            RunCommand = "cargo run",
            TestCommand = "cargo test",
            PackageManager = "Cargo",
            ConfigFiles = ["Cargo.lock", "rust-toolchain.toml", ".cargo/config.toml"],
        },
        new()
        {
            Type = "go", Language = "Go",
            MarkerFiles = ["go.mod"],
            BuildCommand = "go build ./...",
            RunCommand = "go run .",
            TestCommand = "go test ./...",
            PackageManager = "Go modules",
            ConfigFiles = ["go.sum", ".golangci.yml"],
        },
        new()
        {
            Type = "java-maven", Language = "Java",
            MarkerFiles = ["pom.xml"],
            BuildCommand = "mvn compile",
            RunCommand = "mvn exec:java",
            TestCommand = "mvn test",
            PackageManager = "Maven",
            ConfigFiles = ["pom.xml", ".mvn/wrapper/maven-wrapper.properties"],
        },
        new()
        {
            Type = "java-gradle", Language = "Java/Kotlin",
            MarkerFiles = ["build.gradle", "build.gradle.kts"],
            BuildCommand = "./gradlew build",
            RunCommand = "./gradlew run",
            TestCommand = "./gradlew test",
            PackageManager = "Gradle",
            ConfigFiles = ["settings.gradle", "settings.gradle.kts", "gradle.properties"],
        },
        new()
        {
            Type = "ruby", Language = "Ruby",
            MarkerFiles = ["Gemfile"],
            BuildCommand = "bundle install",
            RunCommand = "ruby <main.rb>",
            TestCommand = "bundle exec rspec",
            PackageManager = "Bundler",
            ConfigFiles = ["Gemfile.lock", ".rubocop.yml", "Rakefile"],
        },
        new()
        {
            Type = "docker", Language = "Container",
            MarkerFiles = ["Dockerfile", "docker-compose.yml", "docker-compose.yaml", "compose.yml", "compose.yaml"],
            BuildCommand = "docker build .",
            RunCommand = "docker compose up",
            TestCommand = "docker compose run test",
            PackageManager = "Docker",
            ConfigFiles = [".dockerignore", "docker-compose.override.yml"],
        },
        new()
        {
            Type = "terraform", Language = "HCL",
            MarkerFiles = ["*.tf", "main.tf"],
            BuildCommand = "terraform init && terraform plan",
            RunCommand = "terraform apply",
            TestCommand = "terraform validate",
            PackageManager = "Terraform",
            ConfigFiles = ["terraform.tfvars", ".terraform.lock.hcl", "backend.tf"],
        },
    ];
}
