
using System.Collections.Generic;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services
{
    public static class PendingReceiptStore
    {
        public static Dictionary<string, (List<SaleItem> Items, PaymentResult Payment, string Cashier)> Data
            = new();
    }
}