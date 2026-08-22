namespace TechKingPOS.App.Models
{
    public class CloudSettingsModel
    {
        public int Id { get; set; }

        public string BusinessCode { get; set; } = "";

        public int BusinessId { get; set; } 

        public int BranchId { get; set; }

        public string DeviceId { get; set; } = "";

        public string DeviceName { get; set; } = "";

        public string ApiUrl { get; set; } = "";

        public string? ApiKey { get; set; }

        public bool SyncEnabled { get; set; }

        public DateTime? LastUpload { get; set; }

        public DateTime? LastDownload { get; set; }

        public int SyncIntervalSeconds { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}