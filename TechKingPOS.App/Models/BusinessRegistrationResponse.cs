namespace TechKingPOS.App.Models
{
    public class BusinessRegistrationResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = "";

        public string BusinessCode { get; set; } = "";

        public int BusinessId { get; set; }
    }
}