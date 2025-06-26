using System;
using System.Reflection;
using System.Threading.Tasks;
using MCPServer.ToolApproval;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace MCPServer.Tests
{
    public static class RequiresApprovalAttributeTests
    {
        // ---------- helpers ----------
        private static McpServerTool DummyTool(string name = "dummy") =>
            McpServerTool.Create(
                () => "ok",                 // simpler delegate – no unknown types
                new() { Name = name });

        private static MethodInfo Method<T>(string name) =>
            typeof(T).GetMethod(name, BindingFlags.Static | BindingFlags.Public)!;

        private class DummyMethods
        {
            [RequiresApproval]                                // Required == true (default)
            public static void Dangerous() { }

            [RequiresApproval(false)]                         // explicitly disabled
            public static void Safe() { }

            public static void Plain() { }                    // no attribute
        }

        // ---------- tests ----------
        [Fact]
        public static void Attribute_DefaultsToRequiredTrue()
        {
            var attr = (RequiresApprovalAttribute)Attribute
                       .GetCustomAttribute(Method<DummyMethods>(nameof(DummyMethods.Dangerous)),
                                           typeof(RequiresApprovalAttribute))!;
            Assert.True(attr.Required);
        }

        [Fact]
        public static void Attribute_AllowsDisablingThroughCtor()
        {
            var attr = (RequiresApprovalAttribute)Attribute
                       .GetCustomAttribute(Method<DummyMethods>(nameof(DummyMethods.Safe)),
                                           typeof(RequiresApprovalAttribute))!;
            Assert.False(attr.Required);
        }

        [Fact]
        public static void WrapIfNeeded_ReturnsOriginal_WhenNotRequired()
        {
            var original = DummyTool();
            var wrapped  = ToolApprovalWrapper.WrapIfNeeded(
                             original,
                             Method<DummyMethods>(nameof(DummyMethods.Plain)));

            Assert.Same(original, wrapped);
        }

        [Fact]
        public static void WrapIfNeeded_Wraps_WhenRequired()
        {
            var original = DummyTool();
            var wrapped  = ToolApprovalWrapper.WrapIfNeeded(
                             original,
                             Method<DummyMethods>(nameof(DummyMethods.Dangerous)));

            Assert.NotSame(original, wrapped);
        }
    }
}
