using TechKingPOS.App.Models;
using TechKingPOS.App.Services;

public class ReceiptPreviewData
{
    public List<SaleItem> Items { get; set; } = new();
    public PaymentResult Payment { get; set; }
    public string Cashier { get; set; }
    public string PaymentMethod { get; set; } = "";
}