namespace TechKingPOS.App.Models
{
    public class Permission
    {
        public int Id { get; set; }
        public string UserKey { get; set; } = "";
        public string? Description { get; set; }
    }
}
