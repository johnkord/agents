using System.Diagnostics;

namespace LifeAgent.App;

public static class Diagnostics
{
    public const string ServiceName = "LifeAgent";
    public const string ServiceVersion = "0.1.0";
    public static readonly ActivitySource Source = new(ServiceName, ServiceVersion);
}
