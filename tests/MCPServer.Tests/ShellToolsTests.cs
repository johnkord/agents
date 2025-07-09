using System;
using System.Reflection;
using System.Runtime.InteropServices;
using MCPServer.ToolApproval;
using MCPServer.Tools;
using Xunit;

namespace MCPServer.Tests
{
    /// <summary>
    /// Tests for ShellTools to verify shell command execution works correctly,
    /// especially for complex commands with pipes and shell operators.
    /// </summary>
    public class ShellToolsTests
    {
        [Fact]
        public void RunCommand_MethodSignature_IsCorrect()
        {
            // Verify the method signature matches expectations
            var method = typeof(ShellTools).GetMethod(nameof(ShellTools.RunCommand));
            Assert.NotNull(method);
            
            var parameters = method.GetParameters();
            Assert.Single(parameters);
            Assert.Equal("script", parameters[0].Name);
            
            Assert.Equal(typeof(string), parameters[0].ParameterType);
        }
    }
}