namespace TechKingPOS.App.Models
{
    public class SyncQueueModel
    {
        // Queue Id
        public int Id { get; set; }

        // Business Information
        public string BusinessCode { get; set; } = "";

        public int BranchId { get; set; }

        public string DeviceId { get; set; } = "";

        // Record Information
        public string TableName { get; set; } = "";

        public int RecordId { get; set; }

        public string Action { get; set; } = "";

        // Serialized record (JSON)
        public string JsonData { get; set; } = "";

        // Queue Processing
        public int Priority { get; set; } = 1;

        public string Status { get; set; } = "Pending";

        public int Attempts { get; set; }

        public string LastError { get; set; } = "";

        // Dates
        public DateTime CreatedAt { get; set; }

        public DateTime? SyncedAt { get; set; }

        public DateTime? NextRetryAt { get; set; }
    }
}