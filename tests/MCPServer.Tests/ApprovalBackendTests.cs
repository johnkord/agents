using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MCPServer.ToolApproval;
using Xunit;

namespace MCPServer.Tests;

public class ApprovalBackendTests
{
    [Fact]
    public async Task ConsoleApprovalBackend_ReturnsCorrectName()
    {
        var backend = new ConsoleApprovalBackend();
        Assert.Equal("Console", backend.Name);
    }

    [Fact]
    public void RemoteApprovalBackend_RequiresConfig()
    {
        Assert.Throws<ArgumentNullException>(() => new RemoteApprovalBackend(null!));
    }

    [Fact]
    public void RemoteApprovalBackend_ReturnsCorrectName()
    {
        var config = new RemoteApprovalConfig
        {
            BaseUrl = "https://test.example.com"
        };
        
        using var backend = new RemoteApprovalBackend(config);
        Assert.Equal("Remote", backend.Name);
    }

    [Fact]
    public void ToolApprovalOptions_CreateBackend_Console()
    {
        var options = new ToolApprovalOptions
        {
            BackendType = ApprovalBackendType.Console
        };

        var backend = options.CreateBackend();
        Assert.IsType<ConsoleApprovalBackend>(backend);
    }

    [Fact]
    public void ToolApprovalOptions_CreateBackend_Remote_RequiresConfig()
    {
        var options = new ToolApprovalOptions
        {
            BackendType = ApprovalBackendType.Remote
        };

        Assert.Throws<InvalidOperationException>(() => options.CreateBackend());
    }

    [Fact]
    public void ToolApprovalOptions_CreateBackend_Remote_WithConfig()
    {
        var options = new ToolApprovalOptions
        {
            BackendType = ApprovalBackendType.Remote,
            RemoteConfig = new RemoteApprovalConfig
            {
                BaseUrl = "https://test.example.com"
            }
        };

        var backend = options.CreateBackend();
        Assert.IsType<RemoteApprovalBackend>(backend);
        
        if (backend is IDisposable disposable)
            disposable.Dispose();
    }

    [Fact]
    public async Task ToolApprovalManager_CanSetApprovalBackend()
    {
        var mockBackend = new MockApprovalBackend();
        var manager = ToolApprovalManager.Instance;
        
        // Set our mock backend
        manager.SetApprovalBackend(mockBackend);
        
        // Test that it's used
        var args = new Dictionary<string, object?> { ["test"] = "value" };
        var result = await manager.EnsureApprovedAsync("test_tool", args);
        
        Assert.True(result); // MockApprovalBackend always approves
        Assert.True(mockBackend.WasCalled);
        
        // Reset to default backend
        manager.SetApprovalBackend(new ConsoleApprovalBackend());
    }

    private class MockApprovalBackend : IApprovalBackend
    {
        public string Name => "Mock";
        public bool WasCalled { get; private set; }

        public Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(true); // Always approve for testing
        }
    }
}