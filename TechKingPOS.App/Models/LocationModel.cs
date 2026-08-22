using System;

namespace TechKingPOS.App.Models
{
    public class LocationModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentId { get; set; }
        public string Type { get; set; } = ""; // Store, Section, Shelf
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
