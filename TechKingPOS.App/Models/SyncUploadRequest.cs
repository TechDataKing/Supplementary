namespace TechKingPOS.App.Models;

public class SyncUploadRequest
{
    public string BusinessCode { get; set; } = "";

    public string DeviceId { get; set; } = "";

    public string TableName { get; set; } = "";

    public string ActionType { get; set; }

    public string RecordId { get; set; } = "";

    public string JsonData { get; set; } = "";
}