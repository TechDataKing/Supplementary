using TechKingPOS.App.Data;
using TechKingPOS.App.Models;

using System.Windows;

namespace TechKingPOS.App.Services
{
    public static class LicenseService
    {
        // 🔍 Determine current license status
public static LicenseStatus GetStatus(AppSetting settings)
{
    // ================= ACTIVATED LICENSE =================
    if (settings.LicenseActivated)
    {
        if (settings.LicenseExpiryDate.HasValue &&
            DateTime.Now <= settings.LicenseExpiryDate.Value)
        {
            return LicenseStatus.Active;
        }

        return LicenseStatus.Expired;
    }

    // ================= TRIAL MODE =================

    // First run → start trial
    if (!settings.LicenseInstallDate.HasValue)
    {
        settings.LicenseInstallDate = DateTime.Now;
        settings.LicenseExpiryDate = DateTime.Now.AddDays(30);

        SettingsRepository.Save(settings); // persist trial start

        return LicenseStatus.Trial;
    }

    // Still inside 30-day trial
    if (DateTime.Now <= settings.LicenseInstallDate.Value.AddDays(30))
    {
        return LicenseStatus.Trial;
    }

    // Trial expired
    return LicenseStatus.Expired;
}

        // ✅ Convenience helpers
        public static bool IsActive(AppSetting s) =>
            GetStatus(s) == LicenseStatus.Active;

        public static bool IsExpired(AppSetting s) =>
            GetStatus(s) == LicenseStatus.Expired;

        public static bool IsTrial(AppSetting s) =>
            GetStatus(s) == LicenseStatus.Trial;

        // 🌐 Online activation via GitHub
        public static async Task<bool> ActivateAsync(string enteredKey, AppSetting settings)
{
    var client = new GitHubLicenseClient();
    var licenses = await client.GetLicensesAsync();

    string machineId = MachineInfo.GetMachineId();

    var license = licenses.FirstOrDefault(l => l.key == enteredKey && !l.activated);

    if (license == null)
        return false;

    settings.LicenseActivated = true;
    settings.LicenseExpiryDate = DateTime.UtcNow.AddDays(license.expiryDays);
    settings.LicenseMachineId = machineId;

    // Save locally
    SettingsRepository.Save(settings);
    SettingsCache.ApplyChanges(settings);

    // Mark key as used and bind to machine in GitHub
    //await client.MarkAsActivatedAsync(enteredKey, licenses, machineId);

    return true;
}

    }
}
