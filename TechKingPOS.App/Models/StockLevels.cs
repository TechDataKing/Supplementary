using System;

namespace TechKingPOS.App.Models
{
    public class StockLevels
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int LocationId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
