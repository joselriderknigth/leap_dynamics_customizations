// Dataverse.Assert/DataverseClient.cs
using System.Net.Http.Headers;
using System.Text.Json;

public class DataverseClient {
  private readonly HttpClient _http = new();
  private readonly DvCfg _cfg;
  public DataverseClient(DvCfg cfg) { _cfg = cfg; }

  private async Task<string> TokenAsync() {
    var url = $"https://login.microsoftonline.com/{_cfg.TenantId}/oauth2/v2.0/token";
    var form = new Dictionary<string,string> {
      ["client_id"] = _cfg.ClientId,
      ["client_secret"] = _cfg.ClientSecret,
      ["scope"] = $"{_cfg.ResourceUrl}/.default",
      ["grant_type"] = "client_credentials"
    };
    using var resp = await _http.PostAsync(url, new FormUrlEncodedContent(form));
    var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    return json.RootElement.GetProperty("access_token").GetString()!;
  }

  private async Task<HttpClient> AuthedAsync() {
    var token = await TokenAsync();
    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    _http.DefaultRequestHeaders.Accept.Clear();
    _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return _http;
  }

  public async Task<int> GetIntAsync(string entity, Guid id, string column) {
    var hc = await AuthedAsync();
    var url = $"{_cfg.ResourceUrl}/api/data/v9.2/{entity}s({id})?$select={column}";
    var json = JsonDocument.Parse(await hc.GetStringAsync(url));
    return json.RootElement.GetProperty(column).GetInt32();
  }

  public async Task<int> CountAsync(string entitySetName, string filterOData) {
    var hc = await AuthedAsync();
    var url = $"{_cfg.ResourceUrl}/api/data/v9.2/{entitySetName}?$filter={Uri.EscapeDataString(filterOData)}&$count=true";
    hc.DefaultRequestHeaders.Add("Prefer","odata.include-annotations=\"*\",odata.maxpagesize=1");
    var resp = await hc.GetAsync(url);
    resp.EnsureSuccessStatusCode();
    // odata.count is returned in payload when Prefer header includes count; parse it:
    var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
    return json.RootElement.GetProperty("@odata.count").GetInt32();
  }
}
