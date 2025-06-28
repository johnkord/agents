using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using OpenAIIntegration.Model;
using AgentAlpha;
using AgentAlpha.Configuration;

namespace AgentAlpha.Tests;

public class ResponseOutputItemHandlingTests
{
    [Fact]
    public void ToolFilterConfig_ShouldIncludeTool_WorksCorrectly()
    {
        // Test default configuration (no filters)
        var defaultConfig = new ToolFilterConfig();
        Assert.True(defaultConfig.ShouldIncludeTool("any_tool"));

        // Test whitelist only
        var whitelistConfig = new ToolFilterConfig();
        whitelistConfig.Whitelist.Add("read_file");
        whitelistConfig.Whitelist.Add("write_file");
        Assert.True(whitelistConfig.ShouldIncludeTool("read_file"));
        Assert.True(whitelistConfig.ShouldIncludeTool("write_file"));
        Assert.False(whitelistConfig.ShouldIncludeTool("list_directory"));

        // Test blacklist only
        var blacklistConfig = new ToolFilterConfig();
        blacklistConfig.Blacklist.Add("dangerous_tool");
        Assert.False(blacklistConfig.ShouldIncludeTool("dangerous_tool"));
        Assert.True(blacklistConfig.ShouldIncludeTool("safe_tool"));

        // Test blacklist takes precedence over whitelist
        var combinedConfig = new ToolFilterConfig();
        combinedConfig.Whitelist.Add("add");
        combinedConfig.Blacklist.Add("add");
        Assert.False(combinedConfig.ShouldIncludeTool("add"));
    }

    [Fact]
    public void ToolFilterConfig_FromEnvironment_ParsesCorrectly()
    {
        // Save original environment variables
        var originalWhitelist = Environment.GetEnvironmentVariable("MCP_TOOL_WHITELIST");
        var originalBlacklist = Environment.GetEnvironmentVariable("MCP_TOOL_BLACKLIST");

        try
        {
            // Test whitelist parsing
            Environment.SetEnvironmentVariable("MCP_TOOL_WHITELIST", "read_file,write_file, list_directory ");
            Environment.SetEnvironmentVariable("MCP_TOOL_BLACKLIST", null);
            
            var config = ToolFilterConfig.FromEnvironment();
            Assert.Contains("read_file", config.Whitelist);
            Assert.Contains("write_file", config.Whitelist);
            Assert.Contains("list_directory", config.Whitelist);
            Assert.Empty(config.Blacklist);

            // Test blacklist parsing
            Environment.SetEnvironmentVariable("MCP_TOOL_WHITELIST", null);
            Environment.SetEnvironmentVariable("MCP_TOOL_BLACKLIST", "delete_file, dangerous_tool");
            
            config = ToolFilterConfig.FromEnvironment();
            Assert.Empty(config.Whitelist);
            Assert.Contains("delete_file", config.Blacklist);
            Assert.Contains("dangerous_tool", config.Blacklist);

            // Test both
            Environment.SetEnvironmentVariable("MCP_TOOL_WHITELIST", "safe_tool");
            Environment.SetEnvironmentVariable("MCP_TOOL_BLACKLIST", "unsafe_tool");
            
            config = ToolFilterConfig.FromEnvironment();
            Assert.Contains("safe_tool", config.Whitelist);
            Assert.Contains("unsafe_tool", config.Blacklist);
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable("MCP_TOOL_WHITELIST", originalWhitelist);
            Environment.SetEnvironmentVariable("MCP_TOOL_BLACKLIST", originalBlacklist);
        }
    }

    [Theory]
    [InlineData("function_call", typeof(FunctionToolCall))]
    [InlineData("file_search_call", typeof(FileSearchToolCall))]
    [InlineData("web_search_call", typeof(WebSearchToolCall))]
    [InlineData("computer_call", typeof(ComputerToolCall))]
    [InlineData("reasoning", typeof(ReasoningItem))]
    [InlineData("image_generation_call", typeof(ImageGenerationCall))]
    [InlineData("code_interpreter_call", typeof(CodeInterpreterToolCall))]
    [InlineData("local_shell_call", typeof(LocalShellToolCall))]
    [InlineData("mcp_call", typeof(McpToolCall))]
    [InlineData("mcp_list_tools", typeof(McpListTools))]
    [InlineData("mcp_approval_request", typeof(McpApprovalRequest))]
    [InlineData("message", typeof(OutputMessage))]
    public void ResponseOutputItemConverter_DeserializesCorrectTypes(string type, Type expectedType)
    {
        var json = $@"{{""type"": ""{type}"", ""id"": ""test_id""}}";
        var item = JsonSerializer.Deserialize<ResponseOutputItem>(json);
        
        Assert.NotNull(item);
        Assert.IsType(expectedType, item);
        Assert.Equal(type, item.Type);
    }

    [Fact]
    public void FunctionToolCall_DeserializesWithArguments()
    {
        var json = @"{
            ""type"": ""function_call"",
            ""id"": ""call_123"",
            ""name"": ""add"",
            ""arguments"": {""a"": 2, ""b"": 3},
            ""status"": ""completed""
        }";
        
        var item = JsonSerializer.Deserialize<ResponseOutputItem>(json);
        
        Assert.IsType<FunctionToolCall>(item);
        var funcCall = (FunctionToolCall)item;
        Assert.Equal("call_123", funcCall.Id);
        Assert.Equal("add", funcCall.Name);
        Assert.Equal("completed", funcCall.Status);
        Assert.NotNull(funcCall.Arguments);
    }
}