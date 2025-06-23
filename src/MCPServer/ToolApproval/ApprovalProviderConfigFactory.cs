using System;
using System.Text.Json;

namespace MCPServer.ToolApproval;

internal static class ApprovalProviderConfigFactory
{
    public static ApprovalProviderConfiguration FromEnvironment()
    {
        // 1) Try full JSON config first (unchanged behaviour)
        var json = Environment.GetEnvironmentVariable("APPROVAL_PROVIDER_CONFIG");
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<ApprovalProviderConfiguration>(json);
                if (cfg != null)
                    return cfg;
            }
            catch
            {
                // Ignore parse errors – we’ll fall back to discrete vars
            }
        }

        // 2) NEW: fall back to discrete environment variables such as
        //    APPROVAL_PROVIDER_TYPE, REST_BASE_URL, etc.
        return ApprovalProviderConfigurationExtensions.FromEnvironment();
    }
}
