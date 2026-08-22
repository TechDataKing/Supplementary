namespace TechKingPOS.App.Models
{
    public class LicenseRoot
    {
        public List<LicenseRecord> licenses { get; set; }
    }

    public class LicenseRecord
    {
        public string key { get; set; }
        public bool activated { get; set; }
        public int expiryDays { get; set; }
    }

    public class GitHubFile
    {
        public string content { get; set; }
    }
}
