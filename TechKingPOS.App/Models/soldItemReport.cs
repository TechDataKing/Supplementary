namespace TechKingPOS.App.Models
{
    public class SoldItemReport
    {   
        public int ItemId { get; set; }
        public string ReceiptNumber { get; set; } = "";
        public string ItemName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string PaymentMethod { get; set; }
        public string Cashier { get; set; }
        public decimal Total { get; set; }
        public int BranchId { get; set; }
        public string SaleType { get; set; }
        public decimal MarkedPrice { get; set;}
         public decimal CostPrice { get; set;}

    }
}
