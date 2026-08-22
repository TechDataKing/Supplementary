namespace TechKingPOS.App.Models
{
    public class PaymentResult
    {
        // ===== AMOUNTS (calculated in FinishPay_Click) =====
        public decimal Subtotal { get; }
        public decimal Vat { get; }
        public decimal Total { get; }

        public decimal Discount {get;}
        public int BranchId { get; set; }
        public decimal LoanAmount { get; set; }

        public decimal BalanceBefore { get; set; }
        public decimal TotalBalance { get; set; }
        // ===== PAYMENTS =====
        public decimal CashAmount { get; private set; }
        public bool IsQuickCash { get; set; }
        public bool IsQuickSale => IsQuickCash || CashAmount > 0 || MpesaAmount > 0;
        public decimal MpesaAmount { get; private set; }

        // ===== DERIVED (safe, no business rules) =====
        public decimal AmountPaid => CashAmount + MpesaAmount;
        public decimal Balance => Total - AmountPaid;
        public decimal Change => AmountPaid > Total ? AmountPaid - Total : 0;

        public bool IsCredit => Balance > 0;

        // ===== CREDIT INFO =====
        public string? CustomerName { get; private set; }
        public string? Phone { get; private set; }
        public bool IsDebtPayment { get; set; } = false;
        public decimal CreditPaid { get; set; } = 0;
        public decimal DebtPaymentAmount { get; set; }
        public string PaymentMethod { get; set; } = "";
        public decimal BalanceAfter { get; set; }

        // ===== CONSTRUCTOR =====
        public PaymentResult(
            decimal subtotal,
            decimal discount,
            decimal vat,
            decimal total,
            decimal balBefore,
            decimal totalBalance
            )
            
        {
            Subtotal = subtotal;
            Discount = discount;
            Vat = vat;
            Total = total;
            BalanceBefore = balBefore;
            TotalBalance = totalBalance;
        }

        // ===== PAYMENT SETTER =====
        public void SetPayment(
            decimal cash,
            decimal mpesa,
            string? customerName,
            string? phone)
        {
            CashAmount = cash;
            MpesaAmount = mpesa;
            CustomerName = customerName;
            Phone = phone;
        }
    }
}
