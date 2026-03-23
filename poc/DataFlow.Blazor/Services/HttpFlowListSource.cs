namespace DataFlow.Blazor.Services;

using System.Net.Http.Json;
using System.Text.Json;
using DataFlow.Blazor.Api;

/// <summary>
/// Fetches the flow list from GET /flows on the ASP.NET Core host.
/// </summary>
public class HttpFlowListSource : IFlowListSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public HttpFlowListSource(HttpClient http)
    {
        _http = http;
    }

    public async Task<FlowSummaryDto[]> GetFlowsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _http.GetFromJsonAsync<FlowSummaryDto[]>(
            $"/flows?page={page}&pageSize={pageSize}", JsonOptions, cancellationToken);
        return result ?? [];
    }
}
