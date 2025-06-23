using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPServer.ToolApproval;
using Xunit;

namespace MCPServer.Tests.ToolApproval
{
    public class ApprovalProviderTests
    {
        [Fact]
        public void ConsoleApprovalProvider_HasCorrectName()
        {
            var provider = new ConsoleApprovalProvider();
            Assert.Equal("Console", provider.ProviderName);
        }

        [Fact]
        public void FileApprovalProvider_HasCorrectName()
        {
            var provider = new FileApprovalProvider();
            Assert.Equal("File", provider.ProviderName);
        }

        [Fact]
        public void RestApprovalProvider_HasCorrectName()
        {
            var provider = new RestApprovalProvider("http://localhost:5000");
            Assert.Equal("REST", provider.ProviderName);
        }

        [Fact]
        public void ApprovalProviderFactory_CreatesConsoleProvider()
        {
            var config = new ApprovalProviderConfiguration
            {
                ProviderType = ApprovalProviderType.Console
            };

            var provider = ApprovalProviderFactory.CreateProvider(config);
            
            Assert.IsType<ConsoleApprovalProvider>(provider);
        }

        [Fact]
        public void ApprovalProviderFactory_CreatesFileProvider()
        {
            var config = new ApprovalProviderConfiguration
            {
                ProviderType = ApprovalProviderType.File,
                FileProvider = new FileProviderConfig
                {
                    ApprovalDirectory = "/tmp/test-approvals"
                }
            };

            var provider = ApprovalProviderFactory.CreateProvider(config);
            
            Assert.IsType<FileApprovalProvider>(provider);
        }

        [Fact]
        public void ApprovalProviderFactory_CreatesRestProvider()
        {
            var config = new ApprovalProviderConfiguration
            {
                ProviderType = ApprovalProviderType.Rest,
                RestProvider = new RestProviderConfig
                {
                    BaseUrl = "http://test.example.com"
                }
            };

            var provider = ApprovalProviderFactory.CreateProvider(config);
            
            Assert.IsType<RestApprovalProvider>(provider);
        }

        [Fact]
        public void ApprovalProviderFactory_ThrowsForUnknownType()
        {
            var config = new ApprovalProviderConfiguration
            {
                ProviderType = (ApprovalProviderType)999
            };

            Assert.Throws<ArgumentException>(() => 
                ApprovalProviderFactory.CreateProvider(config));
        }

        [Fact]
        public async Task FileApprovalProvider_CreatesDirectoryIfNotExists()
        {
            var tempDir = $"/tmp/test-approvals-{Guid.NewGuid()}";
            var provider = new FileApprovalProvider(tempDir);

            // Directory should be created during construction
            Assert.True(System.IO.Directory.Exists(tempDir));

            // Clean up
            System.IO.Directory.Delete(tempDir, true);
        }

        private static ApprovalInvocationToken CreateTestToken()
        {
            return new ApprovalInvocationToken(
                Guid.NewGuid(),
                "test_tool",
                new Dictionary<string, object?> { { "param1", "value1" } },
                DateTimeOffset.UtcNow);
        }
    }
}