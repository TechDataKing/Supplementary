using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using TechKingPOS.App.Models;


namespace TechKingPOS.App.Services
{
    public class GitHubLicenseClient
    {   
        private readonly HttpClient _client = new HttpClient();
        private readonly string _url = RepoUrl; // Or the actual GitHub API endpoint

        private const string RepoUrl = "https://raw.githubusercontent.com/TechDataKing/TechKingPOS-License-Server/main/licenses.json";

        public async Task<List<LicenseRecord>> GetLicensesAsync()
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TechKingPOS");

    var json = await client.GetStringAsync(RepoUrl);

    return JsonSerializer
        .Deserialize<LicenseRoot>(json)
        ?.licenses ?? new List<LicenseRecord>();
}


//     public async Task MarkAsActivatedAsync(
//     string keyToActivate,
//     List<LicenseRecord> licenses,
//     string sha)
// {
//     var license = licenses.First(l => l.key == keyToActivate);
//     license.activated = true;

//     var json = JsonSerializer.Serialize(
//         new LicenseRoot { licenses = licenses },
//         new JsonSerializerOptions { WriteIndented = true });

//     var payload = new
//     {
//         message = $"Activate license {keyToActivate}",
//         content = Convert.ToBase64String(
//             System.Text.Encoding.UTF8.GetBytes(json)),
//         sha
//     };

//     var content = new StringContent(
//         JsonSerializer.Serialize(payload),
//         System.Text.Encoding.UTF8,
//         "application/json");

//     await _client.PutAsync(_url, content);
// }
    }
}
