using Forge.Core;

namespace Forge.Tests;

/// <summary>
/// Tests for VerificationState — tracks what has been verified in a session
/// to detect redundant verification actions.
///
/// Research basis:
///   - FuseSearch (arXiv:2601.19568): 34.9% redundant invocation rate
///   - Session 20260320-010300-906: agent ran dotnet build after tests
///     already passed, wasting 7,400 tokens and 8 seconds
/// </summary>
public class VerificationStateTests
{
    [Fact]
    public void InitialState_NothingVerified()
    {
        var state = new VerificationState();

        Assert.False(state.CompilationVerified);
        Assert.False(state.TestsVerified);
        Assert.Null(state.LastEditStep);
    }

    [Fact]
    public void IsEmpty_ReturnsTrueBeforeAnyEdit_AndFalseAfterEdit()
    {
        var state = new VerificationState();

        Assert.True(state.IsEmpty());

        state.RecordEdit(1);

        Assert.False(state.IsEmpty());
    }

    [Fact]
    public void RecordEdit_InvalidatesPriorVerification()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(1);
        Assert.True(state.CompilationVerified);
        Assert.True(state.TestsVerified);

        state.RecordEdit(2);

        Assert.False(state.CompilationVerified);
        Assert.False(state.TestsVerified);
        Assert.Equal(2, state.LastEditStep);
    }

    [Fact]
    public void RecordTestsPassed_ConfirmsBothTestsAndCompilation()
    {
        var state = new VerificationState();

        state.RecordTestsPassed(3);

        Assert.True(state.CompilationVerified);
        Assert.True(state.TestsVerified);
        Assert.Equal(3, state.LastTestStep);
        Assert.Equal(3, state.LastCompilationStep);
    }

    [Fact]
    public void RecordBuildPassed_ConfirmsCompilationOnly()
    {
        var state = new VerificationState();

        state.RecordBuildPassed(2);

        Assert.True(state.CompilationVerified);
        Assert.False(state.TestsVerified);
        Assert.Equal(2, state.LastCompilationStep);
    }

    [Fact]
    public void CheckRedundancy_BuildAfterTests_ReturnsHint()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(2);

        var hint = state.CheckRedundancy("run_bash_command", "{\"command\": \"dotnet build agents.sln\"}");

        Assert.NotNull(hint);
        Assert.Contains("already verified", hint);
        Assert.Contains("step 2", hint);
    }

    [Fact]
    public void CheckRedundancy_BuildAfterBuild_ReturnsHint()
    {
        var state = new VerificationState();
        state.RecordBuildPassed(1);

        var hint = state.CheckRedundancy("run_bash_command", "{\"command\": \"dotnet build\"}");

        Assert.NotNull(hint);
        Assert.Contains("already verified", hint);
    }

    [Fact]
    public void CheckRedundancy_TestsAfterTests_ReturnsHint()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(1);

        var hint = state.CheckRedundancy("run_tests", "{\"projectPath\": \"test.csproj\"}");

        Assert.NotNull(hint);
        Assert.Contains("already passed", hint);
    }

    [Fact]
    public void CheckRedundancy_BuildAfterEdit_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(1);
        state.RecordEdit(2); // Invalidates prior verification

        var hint = state.CheckRedundancy("run_bash_command", "{\"command\": \"dotnet build\"}");

        Assert.Null(hint);
    }

    [Fact]
    public void CheckRedundancy_TestsAfterEdit_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(1);
        state.RecordEdit(2);

        var hint = state.CheckRedundancy("run_tests", "{\"projectPath\": \"test.csproj\"}");

        Assert.Null(hint);
    }

    [Fact]
    public void CheckRedundancy_NonBuildCommand_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(1);

        var hint = state.CheckRedundancy("run_bash_command", "{\"command\": \"ls -la\"}");

        Assert.Null(hint);
    }

    [Fact]
    public void CheckRedundancy_ReadFile_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordTestsPassed(1);

        var hint = state.CheckRedundancy("read_file", "{\"filePath\": \"test.cs\"}");

        Assert.Null(hint);
    }

    [Fact]
    public void FullWorkflow_EditThenVerifyThenRedundantBuild()
    {
        var state = new VerificationState();

        // Step 1: Edit a file
        state.RecordEdit(1);
        Assert.False(state.CompilationVerified);

        // Step 2: Tests pass
        state.RecordTestsPassed(2);
        Assert.True(state.CompilationVerified);
        Assert.True(state.TestsVerified);

        // Step 3: Redundant build check
        var hint = state.CheckRedundancy("run_bash_command", "{\"command\": \"dotnet build agents.sln\"}");
        Assert.NotNull(hint);
        Assert.Contains("tests passed", hint);

        // Step 4: Another edit invalidates everything
        state.RecordEdit(4);
        Assert.False(state.CompilationVerified);
        Assert.False(state.TestsVerified);

        // Step 5: Build is now legitimate
        hint = state.CheckRedundancy("run_bash_command", "{\"command\": \"dotnet build agents.sln\"}");
        Assert.Null(hint);
    }

    // ── File re-read detection tests ───────────────────────────────────────

    [Fact]
    public void CheckFileReRead_FirstRead_ReturnsNull()
    {
        var state = new VerificationState();

        var hint = state.CheckFileReRead("/workspace/src/Auth.cs");

        Assert.Null(hint);
    }

    [Fact]
    public void CheckFileReRead_SecondRead_ReturnsHint()
    {
        var state = new VerificationState();
        state.RecordFileRead("/workspace/src/Auth.cs", 1);

        var hint = state.CheckFileReRead("/workspace/src/Auth.cs");

        Assert.NotNull(hint);
        Assert.Contains("step 1", hint);
        Assert.Contains("previously read", hint);
        Assert.DoesNotContain("compressed", hint); // Neutral wording — no false claims about compression
    }

    [Fact]
    public void CheckFileReRead_CaseInsensitive()
    {
        var state = new VerificationState();
        state.RecordFileRead("/workspace/src/Auth.cs", 1);

        var hint = state.CheckFileReRead("/workspace/src/auth.cs");

        Assert.NotNull(hint);
    }

    [Fact]
    public void CheckFileReRead_DifferentFile_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordFileRead("/workspace/src/Auth.cs", 1);

        var hint = state.CheckFileReRead("/workspace/src/Other.cs");

        Assert.Null(hint);
    }

    [Fact]
    public void InvalidateFileRead_AllowsReReadWithoutHint()
    {
        var state = new VerificationState();
        state.RecordFileRead("/workspace/src/Auth.cs", 1);
        state.InvalidateFileRead("/workspace/src/Auth.cs");

        var hint = state.CheckFileReRead("/workspace/src/Auth.cs");

        Assert.Null(hint);
    }

    [Fact]
    public void FullWorkflow_ReadThenEditThenReReadIsLegitimate()
    {
        var state = new VerificationState();

        // Step 1: Read the file
        state.RecordFileRead("/workspace/src/Auth.cs", 1);

        // Step 2: Edit the file — invalidates read cache for that file
        state.RecordEdit(2);
        state.InvalidateFileRead("/workspace/src/Auth.cs");

        // Step 3: Re-read is legitimate (file changed)
        var hint = state.CheckFileReRead("/workspace/src/Auth.cs");
        Assert.Null(hint);
    }

    // ── P0.2: FileRead content stub (mtime/size based) ─────────────────────────

    [Fact]
    public void TryGetReReadStub_NoPriorRead_ReturnsNull()
    {
        var state = new VerificationState();
        Assert.Null(state.TryGetReReadStub("/x/a.cs", 123L, 456L));
    }

    [Fact]
    public void TryGetReReadStub_MetadataUnknown_ReturnsNull()
    {
        // Legacy two-arg overload stores 0/0 — never produces a stub
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 1);
        Assert.Null(state.TryGetReReadStub("/x/a.cs", 123L, 456L));
    }

    [Fact]
    public void TryGetReReadStub_MetadataMatches_ReturnsStub()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 3, mtimeTicks: 123L, size: 456L);
        var stub = state.TryGetReReadStub("/x/a.cs", 123L, 456L);
        Assert.NotNull(stub);
        Assert.Contains("step 3", stub);
        Assert.Contains("SUPPRESSED", stub);
    }

    [Fact]
    public void TryGetReReadStub_MtimeChanged_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 3, mtimeTicks: 123L, size: 456L);
        Assert.Null(state.TryGetReReadStub("/x/a.cs", 999L, 456L));
    }

    [Fact]
    public void TryGetReReadStub_SizeChanged_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 3, mtimeTicks: 123L, size: 456L);
        Assert.Null(state.TryGetReReadStub("/x/a.cs", 123L, 999L));
    }

    [Fact]
    public void TryGetReReadStub_AfterInvalidate_ReturnsNull()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 3, mtimeTicks: 123L, size: 456L);
        state.InvalidateFileRead("/x/a.cs");
        Assert.Null(state.TryGetReReadStub("/x/a.cs", 123L, 456L));
    }

    [Fact]
    public void TryGetReReadStub_CaseInsensitivePath()
    {
        var state = new VerificationState();
        state.RecordFileRead("/Workspace/A.cs", 2, mtimeTicks: 10L, size: 20L);
        var stub = state.TryGetReReadStub("/workspace/a.cs", 10L, 20L);
        Assert.NotNull(stub);
    }

    // ── P1.4: adversarial stub + hard-block ───────────────────────────────

    [Fact]
    public void TryGetReReadStub_FirstStub_IsWarning_NotBlocked()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 1, mtimeTicks: 10L, size: 20L);
        var first = state.TryGetReReadStub("/x/a.cs", 10L, 20L, stubThresholdBeforeBlock: 2);
        Assert.NotNull(first);
        Assert.Contains("SUPPRESSED", first);
        Assert.DoesNotContain("BLOCKED", first);
        Assert.Equal(1, state.GetStubReturnCount("/x/a.cs"));
    }

    [Fact]
    public void TryGetReReadStub_AfterThresholdStubs_EscalatesToBlocked()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 1, mtimeTicks: 10L, size: 20L);

        var first = state.TryGetReReadStub("/x/a.cs", 10L, 20L, stubThresholdBeforeBlock: 2);
        var second = state.TryGetReReadStub("/x/a.cs", 10L, 20L, stubThresholdBeforeBlock: 2);
        var third = state.TryGetReReadStub("/x/a.cs", 10L, 20L, stubThresholdBeforeBlock: 2);

        Assert.Contains("SUPPRESSED", first);
        Assert.Contains("SUPPRESSED", second);
        Assert.Contains("BLOCKED", third);
        Assert.DoesNotContain("SUPPRESSED", third);
    }

    [Fact]
    public void TryGetReReadStub_EditResetsStubCounter()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 1, mtimeTicks: 10L, size: 20L);

        state.TryGetReReadStub("/x/a.cs", 10L, 20L);
        state.TryGetReReadStub("/x/a.cs", 10L, 20L);
        Assert.Equal(2, state.GetStubReturnCount("/x/a.cs"));

        // Edit invalidates the cache AND resets the stub counter
        state.InvalidateFileRead("/x/a.cs");
        Assert.Equal(0, state.GetStubReturnCount("/x/a.cs"));

        // Fresh read after edit — now the next stub starts the count from zero
        state.RecordFileRead("/x/a.cs", 5, mtimeTicks: 11L, size: 21L);
        var next = state.TryGetReReadStub("/x/a.cs", 11L, 21L, stubThresholdBeforeBlock: 2);
        Assert.NotNull(next);
        Assert.Contains("SUPPRESSED", next);
        Assert.DoesNotContain("BLOCKED", next);
    }

    [Fact]
    public void TryGetReReadStub_ThresholdHigh_NeverBlocks()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 1, mtimeTicks: 10L, size: 20L);

        for (int i = 0; i < 10; i++)
        {
            var r = state.TryGetReReadStub("/x/a.cs", 10L, 20L, stubThresholdBeforeBlock: 100);
            Assert.NotNull(r);
            Assert.Contains("SUPPRESSED", r);
        }
    }

    [Fact]
    public void TryGetReReadStub_StubCountsPerPath()
    {
        var state = new VerificationState();
        state.RecordFileRead("/x/a.cs", 1, mtimeTicks: 10L, size: 20L);
        state.RecordFileRead("/y/b.cs", 2, mtimeTicks: 30L, size: 40L);

        state.TryGetReReadStub("/x/a.cs", 10L, 20L);
        state.TryGetReReadStub("/x/a.cs", 10L, 20L);
        state.TryGetReReadStub("/y/b.cs", 30L, 40L);

        Assert.Equal(2, state.GetStubReturnCount("/x/a.cs"));
        Assert.Equal(1, state.GetStubReturnCount("/y/b.cs"));
    }
}
