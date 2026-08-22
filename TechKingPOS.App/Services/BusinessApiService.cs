using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using TechKingPOS.App.Models;


namespace TechKingPOS.App.Services
{
    public static class BusinessApiService
    {
        private static readonly HttpClient client = new();
        private const string RegisterUrl = ApiRoutes.RegisterBusiness;

public static async Task<BusinessRegistrationResponse> RegisterAsync(BusinessRegistrationModel model)
{
    var json = JsonSerializer.Serialize(model);

    var content = new StringContent(
        json,
        Encoding.UTF8,
        "application/json");

    var httpResponse = await client.PostAsync(RegisterUrl, content);

    var result = await httpResponse.Content.ReadAsStringAsync();

    MessageBox.Show(result, "SERVER RESPONSE");

    if (!httpResponse.IsSuccessStatusCode)
    {
        throw new Exception(result);
    }

    var response = JsonSerializer.Deserialize<BusinessRegistrationResponse>(
        result,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

    if (response == null)
        throw new Exception("Server returned an empty response.");

    return response;
}
    }
}