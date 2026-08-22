namespace TechKingPOS.App.Models
{
    public class ImageInputResult
    {
        public string FilePath { get; set; }
        public byte[] ImageBytes { get; set; }
        public string Source { get; set; } // File, Phone, Camera
    }
}