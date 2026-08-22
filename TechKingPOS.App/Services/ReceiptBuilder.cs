using System.Text;
using System.Windows;
using TechKingPOS.App.Models;
using TechKingPOS.App.Security;


namespace TechKingPOS.App.Services
{

public enum PrintLayout
{
    Thermal,
    Invoice
}
    public static class ReceiptBuilder
    {
        private static int GetReceiptWidth()
        {
            var size = SettingsCache.Current?.PaperSize;

            return size switch
            {
                "58mm Thermal" => 32,
                "80mm Thermal" => 48,

                "A6" => 65,
                "A5" => 90,
                "A4" => 120,

                "Letter" => 110,
                "Legal" => 130,
                "DL Envelope" => 90,

                _ => 32
            };
        }

        private const int MinReceiptLines = 26;
        private const int FooterLines = 2;

        public static string Build(
            IEnumerable<SaleItem> items,
            PaymentResult payment,
            string receiptNumber,
            string cashier,
            ReceiptMode mode)
        {
            string label = mode switch
            {
                ReceiptMode.Original => "*** ORIGINAL ***",
                ReceiptMode.Copy => "*** COPY ***",
                ReceiptMode.Duplicate => "*** DUPLICATE ***",
                _ => ""
            };
            
            
            var sb = new StringBuilder();
            int width = GetReceiptWidth();
            decimal totalQty = items.Sum(i => i.Quantity);

            sb.AppendLine(Center(label, width));
decimal balanceBroughtForward = payment.BalanceBefore;
decimal balanceCarriedForward = payment.BalanceAfter;

if (payment.IsQuickCash)
{
    balanceBroughtForward = 0m;
    balanceCarriedForward = 0m;
}

// ================= HEADER =================
// ================= HEADER =================
var settings = SettingsCache.Current;
if (settings?.PrintLayout == "Invoice")
{
    return BuildInvoice(
        items,
        payment,
        receiptNumber,
        cashier,
        mode);
}

// LOGO PLACEHOLDER (only shows if enabled)
if (settings?.ShowLogoOnReceipt == true)
{
    sb.AppendLine(Center(" ", width));
    sb.AppendLine();
}

// BUSINESS NAME
if (!string.IsNullOrWhiteSpace(settings?.BusinessName))
{
    var nameLines = WrapText(settings.BusinessName.ToUpper(), width);

    foreach (var line in nameLines)
    {
        sb.AppendLine(Center(line, width));
    }
}

// LOCATION
if (!string.IsNullOrWhiteSpace(settings?.PhysicalAddress))
{
    var addrLines = WrapText(settings.PhysicalAddress, width);

    foreach (var line in addrLines)
    {
        sb.AppendLine(Center(line, width));
    }
}

sb.AppendLine();

// CONTACT DETAILS
if (!string.IsNullOrWhiteSpace(settings?.Phone))
{
    sb.AppendLine($" Tel: {Trim(settings.Phone, width - 6)}");
}

if (!string.IsNullOrWhiteSpace(settings?.SecondaryPhone))
{
    sb.AppendLine($" Alt: {Trim(settings.SecondaryPhone, width - 6)}");
}

if (!string.IsNullOrWhiteSpace(settings?.Email))
{
    sb.AppendLine($" Email: {Trim(settings.Email, width - 8)}");
}

sb.AppendLine();
sb.AppendLine(Line(width));

// RECEIPT META
sb.AppendLine($"Receipt #: {receiptNumber}");
sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}");

// CASHIER
if (settings?.ShowCashierOnReceipt == true)
{
    string cashierName = !string.IsNullOrWhiteSpace(cashier)
        ? cashier
        : (string.IsNullOrWhiteSpace(UserSession.UserName)
            ? "UNKNOWN"
            : UserSession.UserName.Trim().Split(' ')[0]);

    sb.AppendLine($"Cashier: {cashierName}");
}

sb.AppendLine(Line(width));

                    // ================= ITEMS =================
            sb.AppendLine("ITEM            QTY   PRICE   TOTAL");
            sb.AppendLine(Line(width));

            foreach (var i in items)
            {
                sb.AppendLine(
                    $"{Trim(i.Name, 15),-15}" +
                    $"{i.Quantity,5}" +
                    $"{i.Price,8:0}" +
                    $"{i.Total,8:0}"
                );
            }

            sb.AppendLine(Line(width));

            // ================= TOTALS =================
            sb.AppendLine($"Items:{totalQty,22}");
            sb.AppendLine($"Subtotal:{payment.Subtotal,18:0.00}");

            if (payment.Discount > 0)
                sb.AppendLine($"Discount:{-payment.Discount,18:0.00}");

            if (settings?.VatEnabled == true && payment.Vat > 0)
            {
                sb.AppendLine(
                    $"VAT ({settings.VatPercent:0.#}%):{payment.Vat,18:0.00}"
                );
            }
            sb.AppendLine(Line(width));
            sb.AppendLine($"TOTAL:{payment.Total,22:0.00}");


            sb.AppendLine(Line(width));

            // ================= PAYMENT =================
            BuildPaymentSection(sb, payment, balanceBroughtForward, balanceCarriedForward);
            sb.AppendLine(Line(width));
            sb.AppendLine();
            if (settings?.SignReceipt==true)
            {
            sb.AppendLine($"Prepared By: {cashier}");
            sb.AppendLine("Sign: ________________________");
            sb.AppendLine();
            sb.AppendLine("Received By:__________________");
            sb.AppendLine("Sign: ________________________");
            }
            // ===== FORCE MIN HEIGHT =====
            int contentLines = sb
                .ToString()
                .Split('\n', StringSplitOptions.None)
                .Length;

            int targetLines = MinReceiptLines - FooterLines;

            while (contentLines
 < targetLines)
            {
                sb.AppendLine();
                contentLines++;
            }
            

// ================= FOOTER =================
    if (!string.IsNullOrWhiteSpace(settings?.ReceiptFooter))
    {
        sb.AppendLine(Center(Trim(settings.ReceiptFooter, width),width));
    }
            sb.AppendLine(Center("© TechKing",width));

            return sb.ToString();
        }

        // =========================================================

        private static void BuildPaymentSection(
            StringBuilder sb,
            PaymentResult payment,
            decimal balanceBroughtForward,
            decimal balanceCarriedForward)
        {
            
            
            bool hasCash = payment.CashAmount > 0;
            bool hasMpesa = payment.MpesaAmount > 0;

            bool isCreditSale =
                payment.Balance > 0 &&
                !string.IsNullOrWhiteSpace(payment.CustomerName);

            

            // ================= DEBT PAYMENT =================
            if (payment.IsDebtPayment)
            {
                sb.AppendLine("PAYMENT: DEBT PAYMENT");

                sb.AppendLine(
                    $"BAL B/F:{balanceBroughtForward,20:0.00}");

                if (payment.Total > 0)
                {
                    sb.AppendLine(
                        $"GOODS:{payment.Total,24:0.00}");
                }

                sb.AppendLine(
                    $"PAID:{payment.CreditPaid,25:0.00}");

                sb.AppendLine(
                    $"BAL C/F:{balanceCarriedForward,22:0.00}");

                sb.AppendLine(
                    $"Customer: {payment.CustomerName}");

                sb.AppendLine(
                    $"Phone: {payment.Phone}");

                return;
            }

            // ================= CREDIT SALE =================
            if (isCreditSale)
            {
                sb.AppendLine("PAYMENT: CREDIT SALE");

                sb.AppendLine($"GOODS:{payment.Total,24:0.00}");
                sb.AppendLine($"PAID:{payment.AmountPaid,25:0.00}");

                if (balanceBroughtForward > 0)
                {
                    sb.AppendLine($"BAL B/F:{balanceBroughtForward,20:0.00}");
                }

                sb.AppendLine($"BAL C/F:{balanceCarriedForward,22:0.00}");

                sb.AppendLine($"Customer: {payment.CustomerName}");
                sb.AppendLine($"Phone: {payment.Phone}");

                return;
            }

            // ================= MIXED =================
            if (hasCash && hasMpesa)
            {
                sb.AppendLine("Payment: MIXED");
                sb.AppendLine($"Cash:{payment.CashAmount,23:0.00}");
                sb.AppendLine($"Mpesa:{payment.MpesaAmount,21:0.00}");
                sb.AppendLine($"Paid:{payment.AmountPaid,23:0.00}");
                sb.AppendLine($"Change:{payment.Change,20:0.00}");
                return;
            }

            // ================= MPESA =================
            if (hasMpesa)
            {
                sb.AppendLine("Payment: MPESA");
                sb.AppendLine($"Amount Paid:{payment.AmountPaid,15:0.00}");
                sb.AppendLine("Change: 0.00");
                return;
            }

            // ================= CASH =================
            sb.AppendLine("Payment: CASH");
            sb.AppendLine($"Amount Paid:{payment.AmountPaid,15:0.00}");
            sb.AppendLine($"Change:{payment.Change,20:0.00}"); 
      }

        // ================= HELPERS =================
        private static string Line(int width)
            => new string('-', width);

        private static string Center(string text, int width)
        {
            int pad = Math.Max(0, (width - text.Length) / 2);
            return new string(' ', pad) + text;
        }


        private static string Trim(string text, int max)
            => string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Length <= max ? text : text[..max];

private static List<string> WrapText(string text, int maxWidth)
{
    var lines = new List<string>();

    if (string.IsNullOrWhiteSpace(text))
        return lines;

    var words = text.Split(' ');
    var currentLine = "";

    foreach (var word in words)
    {
        if ((currentLine + " " + word).Trim().Length > maxWidth)
        {
            lines.Add(currentLine.Trim());
            currentLine = word;
        }
        else
        {
            currentLine += " " + word;
        }
    }

    if (!string.IsNullOrWhiteSpace(currentLine))
        lines.Add(currentLine.Trim());

    return lines;
}
private static string BuildInvoice(
    IEnumerable<SaleItem> items,
    PaymentResult payment,
    string receiptNumber,
    string cashier,
    ReceiptMode mode)
{
    
    var sb = new StringBuilder();
    var settings = SettingsCache.Current;
    int width = GetReceiptWidth();

    string label = mode switch
    {
        ReceiptMode.Original => "*** ORIGINAL ***",
        ReceiptMode.Copy => "*** COPY ***",
        ReceiptMode.Duplicate => "*** DUPLICATE ***",
        _ => ""
    };

sb.AppendLine(Center(label, width));

    sb.AppendLine(Line(width));
    sb.AppendLine(Center(settings.BusinessName.ToUpper(), width));
    sb.AppendLine(Center("INVOICE", width));
    sb.AppendLine(Line(width));

    sb.AppendLine($"Invoice No : {receiptNumber}");
    sb.AppendLine($"Date       : {DateTime.Now:yyyy-MM-dd HH:mm}");
    sb.AppendLine($"Cashier    : {cashier}");

    if (!string.IsNullOrWhiteSpace(payment.CustomerName))
    {
        sb.AppendLine($"Customer   : {payment.CustomerName}");
        sb.AppendLine($"Phone      : {payment.Phone}");
    }

    sb.AppendLine(Line(width));

    sb.AppendLine(
        $"{ "ITEM",-25}" +
        $"{ "QTY",6}" +
        $"{ "PRICE",12}" +
        $"{ "TOTAL",14}");

    sb.AppendLine(Line(width));

    foreach (var item in items)
    {
        sb.AppendLine(
            $"{Trim(item.Name, 25),-25}" +
            $"{item.Quantity,6}" +
            $"{item.Price,12:0.00}" +
            $"{item.Total,14:0.00}");
    }

    sb.AppendLine(Line(width));
    decimal qty = items.Sum(x => x.Quantity);

        sb.AppendLine(Line(width));

        sb.AppendLine($"Items      : {qty:0.##}");
        sb.AppendLine($"Subtotal   : {payment.Subtotal:0.00}");

        if (payment.Discount > 0)
        {
            sb.AppendLine($"Discount   : {payment.Discount:0.00}");
        }

        if (settings?.VatEnabled == true)
        {
            sb.AppendLine($"VAT        : {payment.Vat:0.00}");
        }

        sb.AppendLine($"TOTAL      : {payment.Total:0.00}");

        sb.AppendLine(Line(width));

    BuildInvoicePaymentSection(sb, payment);

    sb.AppendLine(Line(width));

    // SIGNATURES
    if (settings?.SignReceipt == true)
    {
        sb.AppendLine();
        sb.AppendLine($"Prepared By : {cashier}");
        sb.AppendLine();
        sb.AppendLine("Signature : __________________________");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("Received By : ________________________");
        sb.AppendLine();
        sb.AppendLine("Signature : __________________________");
        sb.AppendLine();
    }

    // FOOTER
    if (!string.IsNullOrWhiteSpace(settings?.ReceiptFooter))
    {
        sb.AppendLine(Center(settings.ReceiptFooter, width));
    }

    sb.AppendLine(Center("© TechKing", width));

    return sb.ToString();
}
private static void BuildInvoicePaymentSection(
    StringBuilder sb,
    PaymentResult payment)
{
    bool hasCash = payment.CashAmount > 0;
    bool hasMpesa = payment.MpesaAmount > 0;

    bool isDebtPayment = payment.IsDebtPayment;

    bool isCreditSale =
        !payment.IsDebtPayment &&
        payment.Balance > 0 &&
        !string.IsNullOrWhiteSpace(payment.CustomerName);

// MessageBox.Show(
//     $"IsDebtPayment: {payment.IsDebtPayment}\n" +
//     $"BalanceBefore: {payment.BalanceBefore}\n" +
//     $"CreditPaid: {payment.CreditPaid}\n" +
//     $"TotalBalance: {payment.TotalBalance}\n" +
//     $"Customer: {payment.CustomerName}"
// );

    // ================= DEBT PAYMENT =================

    if (isDebtPayment)
    {
        BuildDebtInvoice(sb, payment);
        return;
    }

    // ================= CREDIT SALE =================

    if (isCreditSale)
    {
        BuildCreditInvoice(sb, payment);
        return;
    }

    // ================= MIXED =================

    if (hasCash && hasMpesa)
    {
        BuildMixedInvoice(sb, payment);
        return;
    }

    // ================= MPESA =================

    if (hasMpesa)
    {
        BuildMpesaInvoice(sb, payment);
        return;
    }

    // ================= CASH =================

    BuildCashInvoice(sb, payment);
}
private static void BuildCreditInvoice(
    StringBuilder sb,
    PaymentResult payment)
{
    sb.AppendLine("PAYMENT: CREDIT SALE");
    sb.AppendLine();

    // sb.AppendLine($"Customer   : {payment.CustomerName}");
    // sb.AppendLine($"Phone      : {payment.Phone}");

    sb.AppendLine();

    sb.AppendLine($"GOODS      : {payment.Total:0.00}");
    sb.AppendLine($"PAID       : {payment.AmountPaid:0.00}");

    if (payment.BalanceBefore > 0)
    {
        sb.AppendLine(
            $"BAL B/F    : {payment.BalanceBefore:0.00}");
    }

    sb.AppendLine(
        $"BAL C/F    : {payment.BalanceAfter:0.00}");
}
private static void BuildDebtInvoice(
    StringBuilder sb,
    PaymentResult payment)
{
    sb.AppendLine("PAYMENT: DEBT PAYMENT");
    sb.AppendLine();

    sb.AppendLine($"BAL B/F    : {payment.BalanceBefore:0.00}");

    if (payment.Total > 0)
    {
        sb.AppendLine($"GOODS      : {payment.Total:0.00}");
    }

    sb.AppendLine($"PAID       : {payment.CreditPaid:0.00}");

    sb.AppendLine($"BAL C/F    : {payment.BalanceAfter:0.00}");

}
private static void BuildCashInvoice(
    StringBuilder sb,
    PaymentResult payment)
{
    sb.AppendLine("PAYMENT: CASH");
    sb.AppendLine();

    sb.AppendLine(
        $"TOTAL      : {payment.Total:0.00}");

    sb.AppendLine(
        $"PAID       : {payment.AmountPaid:0.00}");

    sb.AppendLine(
        $"CHANGE     : {payment.Change:0.00}");
}
private static void BuildMpesaInvoice(
    StringBuilder sb,
    PaymentResult payment)
{
    sb.AppendLine("PAYMENT: MPESA");
    sb.AppendLine();

    sb.AppendLine(
        $"TOTAL      : {payment.Total:0.00}");

    sb.AppendLine(
        $"PAID       : {payment.AmountPaid:0.00}");

    sb.AppendLine("CHANGE     : 0.00");
}
private static void BuildMixedInvoice(
    StringBuilder sb,
    PaymentResult payment)
{
    sb.AppendLine("PAYMENT: MIXED");
    sb.AppendLine();

    sb.AppendLine(
        $"TOTAL      : {payment.Total:0.00}");

    sb.AppendLine(
        $"CASH       : {payment.CashAmount:0.00}");

    sb.AppendLine(
        $"MPESA      : {payment.MpesaAmount:0.00}");

    sb.AppendLine(
        $"PAID       : {payment.AmountPaid:0.00}");

    sb.AppendLine(
        $"CHANGE     : {payment.Change:0.00}");
}
    }
}