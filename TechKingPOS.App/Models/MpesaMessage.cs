public class MpesaMessage
{
    public int Id { get; set; }
    public string Code { get; set; }
    public decimal Amount { get; set; }

    public string Sender { get; set; }   // NAME ONLY (UI)
    public string Phone { get; set; }    // hidden/audit

    public DateTime Time { get; set; }
    public bool IsSelected { get; set; } = false;
}