using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services;

public static class ApiClient
{
    private static readonly HttpClient client = new();

public static async Task<ApiResponse> PostAsync(
    string url,
    object data)
{
    var json = JsonSerializer.Serialize(data);

    var content = new StringContent(
        json,
        Encoding.UTF8,
        "application/json");

    using HttpResponseMessage response =
        await client.PostAsync(url, content);

    string result =
        await response.Content.ReadAsStringAsync();
        //MessageBox.Show(result, "SERVER RESPONSE");

    if (!response.IsSuccessStatusCode)
    {
        return new ApiResponse
        {
            Success = false,
            Message = $"HTTP {(int)response.StatusCode}"
        };
    }


    return JsonSerializer.Deserialize<ApiResponse>(
        result,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
}
}