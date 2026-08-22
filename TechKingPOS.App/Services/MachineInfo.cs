using System.Management;
using System.Security.Cryptography;
using System.Text;

public static class MachineInfo
{
    public static string GetMachineId()
    {
        // We'll combine CPU + motherboard + disk
        string cpu = GetCpuId();
        string board = GetMotherboardId();
        string disk = GetDiskId();

        string raw = $"{cpu}-{board}-{disk}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes).Substring(0, 16); // 16 chars
    }

    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select ProcessorId from Win32_Processor");
            foreach (var item in searcher.Get())
                return item["ProcessorId"]?.ToString() ?? "CPU";
        }
        catch { }
        return "CPU";
    }

    private static string GetMotherboardId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select SerialNumber from Win32_BaseBoard");
            foreach (var item in searcher.Get())
                return item["SerialNumber"]?.ToString() ?? "BOARD";
        }
        catch { }
        return "BOARD";
    }

    private static string GetDiskId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select SerialNumber from Win32_DiskDrive");
            foreach (var item in searcher.Get())
                return item["SerialNumber"]?.ToString() ?? "DISK";
        }
        catch { }
        return "DISK";
    }
}
