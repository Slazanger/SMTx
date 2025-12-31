using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SMTx.Models;

namespace SMTx.Services;

public class JsonDataService : IDataService
{
    private readonly string _baseUrl;
    private readonly HttpClient? _httpClient;

    public JsonDataService(string baseUrl = "", HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<List<RenderSolarSystem>> LoadSolarSystemsAsync()
    {
        var systems = new List<RenderSolarSystem>();

        var url = string.IsNullOrEmpty(_baseUrl) 
            ? "data/solar-systems.json" 
            : $"{_baseUrl}/data/solar-systems.json";

        try
        {
            string json;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                json = await _httpClient!.GetStringAsync(url);
            }
            else
            {
                // For relative URLs, always use HTTP (browser mode)
                // File system access doesn't work in WebAssembly
                json = await _httpClient!.GetStringAsync(url);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            systems = JsonSerializer.Deserialize<List<RenderSolarSystem>>(json, options) ?? systems;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading solar systems from JSON: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"URL attempted: {url}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"Error loading solar systems from JSON: {ex.Message}");
            Console.WriteLine($"URL attempted: {url}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return systems;
    }

    public async Task<List<StargateLink>> LoadStargateLinksAsync()
    {
        var links = new List<StargateLink>();

        var url = string.IsNullOrEmpty(_baseUrl) 
            ? "data/stargate-links.json" 
            : $"{_baseUrl}/data/stargate-links.json";

        try
        {
            string json;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                json = await _httpClient!.GetStringAsync(url);
            }
            else
            {
                // For relative URLs, always use HTTP (browser mode)
                // File system access doesn't work in WebAssembly
                json = await _httpClient!.GetStringAsync(url);
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            links = JsonSerializer.Deserialize<List<StargateLink>>(json, options) ?? links;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading stargate links from JSON: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"URL attempted: {url}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"Error loading stargate links from JSON: {ex.Message}");
            Console.WriteLine($"URL attempted: {url}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return links;
    }
}

