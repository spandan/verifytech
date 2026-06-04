using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.Core.Configuration;

/// <summary>Legacy wrapper — prefer <see cref="SecureEndpointResolver"/>.</summary>
public sealed class EndpointConfigurationService
{
    private readonly SecureEndpointResolver _resolver = new();

    public (AgentRuntimeSettings Settings, AppLaunchOptions Launch) Resolve(string[] args) =>
        _resolver.Resolve(args);
}
