namespace TechKingPOS.App.Services.AI.OCR
{
    public interface IOCRProvider
    {
        Task<string> ExtractAsync(byte[] imageBytes);
        string Name { get; }
    }
}