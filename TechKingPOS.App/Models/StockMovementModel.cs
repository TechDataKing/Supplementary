using System;

namespace TechKingPOS.App.Models
{
    public class StockMovementModel
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public int FromLocationId { get; set; }
        public int ToLocationId { get; set; }
        public string? Reason { get; set; }
        public string PerformedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
