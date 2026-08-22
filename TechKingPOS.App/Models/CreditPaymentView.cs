namespace TechKingPOS.App.Models
{
public class CreditPaymentView
{
    public DateTime CreatedAt { get; set; }
    public string Description { get; set; }

    public decimal Debit { get; set; }   // DR
    public decimal Credit { get; set; }  // CR

    public decimal Balance { get; set; }
}
}
