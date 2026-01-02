using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace E2E.Api.Tests.Utils
{
    public class DataverseClient
    {
        private readonly TestConfig _cfg;
        private readonly HttpClient _http = new();
        private readonly TokenCache _tokenCache;

        public DataverseClient(TestConfig cfg)
        {
            _cfg = cfg;
            _tokenCache = new TokenCache(cfg.Token);
        }

        // ---------------------------
        // Auth & setup
        // ---------------------------

        private async Task<string> GetTokenAsync()
        {
            if (_tokenCache.IsValid)
                return _tokenCache.Token!;

            var url =
                $"https://login.microsoftonline.com/{_cfg.Auth.DataverseTenant}/oauth2/v2.0/token";

            var form = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = _cfg.Auth.DataverseClientId,
                    ["client_secret"] = _cfg.Auth.DataverseClientSecret,
                    ["scope"] = $"{_cfg.Auth.DynamicsUrl}/.default",
                    ["grant_type"] = "client_credentials",
                }
            );

            using var resp = await _http.PostAsync(url, form);
            resp.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e)
                ? e.GetInt32()
                : 3600;

            _tokenCache.Set(token!, expiresIn);
            return token!;
        }

        private async Task<HttpClient> AuthenticatedClientAsync()
        {
            var token = await GetTokenAsync();

            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token
            );
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );

            return _http;
        }

        // ---------------------------
        // Generic CRUD helpers
        // ---------------------------
        public async Task<Guid> CreateAsync(string entityLogicalName, object payload)
        {
            var client = await AuthenticatedClientAsync();
            var url = $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/{entityLogicalName}s";

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(url, content);

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new DataverseException(resp.StatusCode, raw);
            }

            var location = resp.Headers.Location?.ToString();
            var id = location?.Split('(').Last().TrimEnd(')');
            return Guid.Parse(id ?? throw new InvalidOperationException("Missing record id"));
        }

        public async Task<Guid> GetByNameAsync(
            string entityLogicalName,
            string nameField,
            string nameValue,
            string idField
        )
        {
            var safeValue = nameValue.Replace("'", "''");
            var filter = $"{nameField} eq '{safeValue}'";

            var rows = await GetMultipleAsync(
                entityLogicalName,
                filter: filter,
                select: idField,
                top: 1
            );

            if (rows.Count == 0)
                throw new InvalidOperationException(
                    $"No {entityLogicalName} found with {nameField} = '{nameValue}'"
                );

            return rows[0].GetProperty(idField).GetGuid();
        }

        public async Task<JsonElement> GetAsync(
            string entityLogicalName,
            Guid id,
            string? select = null
        )
        {
            var client = await AuthenticatedClientAsync();
            var url = $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/{entityLogicalName}s({id})";
            if (!string.IsNullOrEmpty(select))
                url += $"?$select={select}";

            using var resp = await client.GetAsync(url);

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new DataverseException(resp.StatusCode, raw);
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            return doc.RootElement.Clone();
        }

        public async Task<List<JsonElement>> GetMultipleByFetchXmlAsync(
            string entitySetName,
            string fetchXml
        )
        {
            var client = await AuthenticatedClientAsync();

            var encodedFetch = Uri.EscapeDataString(fetchXml);
            var url =
                $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/{entitySetName}?fetchXml={encodedFetch}";

            using var resp = await client.GetAsync(url);

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new DataverseException(resp.StatusCode, raw);

            using var doc = JsonDocument.Parse(raw);
            return doc
                .RootElement.GetProperty("value")
                .EnumerateArray()
                .Select(e => e.Clone())
                .ToList();
        }

        public async Task<List<JsonElement>> GetMultipleAsync(
            string entityLogicalName,
            string? filter = null,
            string? select = null,
            int? top = null
        )
        {
            var client = await AuthenticatedClientAsync();

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(filter))
                queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
            if (!string.IsNullOrEmpty(select))
                queryParams.Add($"$select={Uri.EscapeDataString(select)}");
            if (top.HasValue)
                queryParams.Add($"$top={top.Value}");

            var url = $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/{entityLogicalName}s";
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            using var resp = await client.GetAsync(url);

            var raw = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new DataverseException(resp.StatusCode, raw);

            using var doc = JsonDocument.Parse(raw);

            return doc
                .RootElement.GetProperty("value")
                .EnumerateArray()
                .Select(e => e.Clone())
                .ToList();
        }

        public async Task UpdateAsync(string entityLogicalName, Guid id, object payload)
        {
            var client = await AuthenticatedClientAsync();
            var url = $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/{entityLogicalName}s({id})";

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var resp = await client.SendAsync(req);

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new DataverseException(resp.StatusCode, raw);
            }
        }

        public async Task DeleteAsync(string entityLogicalName, Guid id)
        {
            var client = await AuthenticatedClientAsync();
            var url = $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/{entityLogicalName}s({id})";
            using var resp = await client.DeleteAsync(url);

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                throw new DataverseException(resp.StatusCode, raw);
            }
        }

        // ---------------------------
        //  Utility helpers
        // ---------------------------

        public async Task<List<JsonElement>> ExecuteFetchXmlAsync(string fetchXml)
        {
            var client = await AuthenticatedClientAsync();
            var url = $"{_cfg.Auth.DynamicsUrl}/api/data/v9.2/leap_ExecuteFetchXml";

            var payload = new { FetchXml = fetchXml };
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );
            using var resp = await client.PostAsync(url, content);
            resp.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            return doc.RootElement.GetProperty("value").EnumerateArray().ToList();
        }
    }
}
