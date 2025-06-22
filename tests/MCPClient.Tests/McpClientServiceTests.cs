using Microsoft.Extensions.Logging;
using MCPClient;
using Xunit;

namespace MCPClient.Tests;

public class McpClientServiceTests
{
    private readonly ILoggerFactory _loggerFactory;

    public McpClientServiceTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
    }

    [Fact]
    public void McpTransportType_HasCorrectValues()
    {
        // Verify enum values are as expected
        Assert.Equal(0, (int)McpTransportType.Stdio);
        Assert.Equal(1, (int)McpTransportType.Http);
    }

    [Theory]
    [InlineData("stdio", McpTransportType.Stdio)]
    [InlineData("STDIO", McpTransportType.Stdio)]
    [InlineData("http", McpTransportType.Http)]
    [InlineData("HTTP", McpTransportType.Http)]
    [InlineData("sse", McpTransportType.Http)]
    [InlineData("SSE", McpTransportType.Http)]
    public void ParseTransportType_ValidInputs_ReturnsCorrectType(string input, McpTransportType expected)
    {
        // Act
        var result = McpClientService.ParseTransportType(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("websocket")]
    public void ParseTransportType_InvalidInputs_ThrowsArgumentException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => McpClientService.ParseTransportType(input));
    }

    [Fact]
    public void McpClientService_Constructor_CreatesInstance()
    {
        // Act
        var service = new McpClientService(_loggerFactory);

        // Assert
        Assert.NotNull(service);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WithStdioTransport_RequiresCommandAndArguments()
    {
        // Arrange
        var service = new McpClientService(_loggerFactory);

        // Act & Assert - Missing command
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.ConnectAsync(McpTransportType.Stdio, "TestServer", null, null, null));

        // Act & Assert - Missing arguments
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.ConnectAsync(McpTransportType.Stdio, "TestServer", "dotnet", null, null));
    }

    [Fact]
    public async Task ConnectAsync_WithHttpTransport_RequiresServerUrl()
    {
        // Arrange
        var service = new McpClientService(_loggerFactory);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.ConnectAsync(McpTransportType.Http, "TestServer", null, null, null));
    }

    [Fact]
    public async Task ConnectAsync_UnsupportedTransportType_ThrowsArgumentException()
    {
        // Arrange
        var service = new McpClientService(_loggerFactory);
        var invalidTransportType = (McpTransportType)999;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            service.ConnectAsync(invalidTransportType, "TestServer", "cmd", new[] { "arg" }, "http://localhost"));
    }

    [Fact]
    public async Task ListToolsAsync_NotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new McpClientService(_loggerFactory);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListToolsAsync());
    }

    [Fact]
    public async Task CallToolAsync_NotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new McpClientService(_loggerFactory);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            service.CallToolAsync("test", new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task DisposeAsync_DisposesCorrectly()
    {
        // Arrange
        var service = new McpClientService(_loggerFactory);

        // Act
        await service.DisposeAsync();

        // Assert - Should not throw
        Assert.False(service.IsConnected);
    }
}