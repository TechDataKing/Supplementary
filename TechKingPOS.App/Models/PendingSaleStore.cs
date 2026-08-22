using TechKingPOS.App.Models;

public static class PendingSaleStore
{
    public static List<SaleItem> CartItems { get; set; } = new();
    public static PaymentResult Payment { get; set; }
    public static string ReceiptNumber { get; set; }
    public static string Cashier { get; set; }
    public static bool Completed { get; set; } = false;
    


}