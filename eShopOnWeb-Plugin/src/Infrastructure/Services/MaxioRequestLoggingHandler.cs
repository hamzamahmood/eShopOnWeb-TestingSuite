using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Services;

/// <summary>
/// Logs the method/path/status of every outbound Maxio call for end-to-end traceability (api-integration
/// quality gate 10). Never logs headers or bodies, so the Basic-auth credential is never captured.
/// </summary>
public class MaxioRequestLoggingHandler : DelegatingHandler
{
    private readonly IAppLogger<MaxioRequestLoggingHandler> _logger;

    public MaxioRequestLoggingHandler(IAppLogger<MaxioRequestLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        _logger.LogInformation("Maxio {Method} {Path} -> {StatusCode}", request.Method, request.RequestUri?.AbsolutePath ?? "(unknown)", (int)response.StatusCode);
        return response;
    }
}
