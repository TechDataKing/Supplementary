
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Infrastructure;


namespace TechKingPOS.App.Data
{       
        public static class SalesRepository
        {
        public static string SaveSale(
            List<SaleItem> items,
            string cashier,
            decimal subtotal,
            decimal discount,
            decimal vat,
            decimal total,
            decimal amountPaid,
            decimal totalCost = 0m,
            decimal totalProfit = 0m
            )
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                LoggerService.Info("💾", "SALE", "Saving sale started");

                string receiptNumber = ReceiptNumberService.Generate();
                DateTime createdAt = DateTime.Now;

                // ================= TRANSACTION HEADER =================
                string status;

                if (amountPaid <= 0)
                {
                    status = "Pending";
                }
                else
                {
                    status = "Completed";
                }
                var headerCmd = connection.CreateCommand();
                                headerCmd.Transaction = transaction;
                headerCmd.CommandText = @"
                    INSERT INTO Transactions (
                        ReceiptNumber,
                        Cashier,
                        Total,
                        Tax,
                        Discount,
                        AmountPaid,
                        CreatedAt,
                        PaymentStatus,
                        BranchId
                    )
                    VALUES (
                        @receipt,
                        @cashier,
                        @total,
                        @tax,
                        @discount,
                        @paid,
                        @created,
                        @status,
                        @branchId
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                ";

                headerCmd.Parameters.AddWithValue("@receipt", receiptNumber);
                headerCmd.Parameters.AddWithValue("@cashier", cashier);
                headerCmd.Parameters.AddWithValue("@total", total);
                headerCmd.Parameters.AddWithValue("@tax", vat);
                headerCmd.Parameters.AddWithValue("@discount", discount);
                headerCmd.Parameters.AddWithValue("@paid", amountPaid);
                headerCmd.Parameters.AddWithValue("@created", createdAt);
                headerCmd.Parameters.AddWithValue("@status", status);
                headerCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
                int saleId = (int)headerCmd.ExecuteScalar();



               ActivityRepository.Log(connection, transaction, new Activity
                {
                    EntityType = "Transaction",
                    EntityId = (int)saleId,
                    EntityName = receiptNumber,
                    Action = "TRANSACTION_CREATED",
                    QuantityChange = 0,
                    Reason = $"Sale started. Items={items.Count}",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                });






                LoggerService.Info(
                    "🧾",
                    "SALE",
                    "Transaction header saved",
                    receiptNumber
                );

                // ================= LINE ITEMS =================
                foreach (var item in items)
                { 
                    
                    decimal baseQty = 0;

                    if (!item.IsRepack)
                    {
                        if (!UnitConverter.TryToBase(
                            item.UnitType,
                            item.Quantity,
                            item.UnitValue,
                            out var baseUnit,
                            out baseQty,
                            out var error))
                        {
                            throw new InvalidOperationException(error);
                        }
                    }

                        decimal qtyBefore = ItemRepository.GetBaseQuantity(
                                connection,
                                transaction,
                                item.ItemId
                            );
                decimal costPrice;
                decimal costTotal;

                if (item.IsRepack && item.RepackId.HasValue)
                {
                    costPrice = RepackStockRepository.GetCostPerUnit(
                        connection,
                        transaction,
                        item.RepackId.Value
                    );

                    costTotal = costPrice * item.UnitValue; // ✅ correct
                }
                else
                {
                    costPrice = ItemRepository.GetMarkedPrice(
                        connection,
                        transaction,
                        item.ItemId
                    );

                    costTotal = costPrice * baseQty;
                }  
                        
                            decimal lineProfit = item.Total - costTotal;

                            totalCost += costTotal;
                            totalProfit += lineProfit;


                
                        // Deduct stock (transaction-safe)
        if (item.IsRepack && item.RepackId.HasValue)
        {
            // 🔥 REPACK DEDUCTION (NO CONVERSION)
            RepackStockRepository.DeductStock(
                connection,
                transaction,
                item.RepackId.Value,
                item.UnitValue, // ✅ DIRECT QTY (NO baseQty)
                $"Sale {receiptNumber}",
                cashier
            );
        }
        else
        {
            // ✅ NORMAL ITEM (UNCHANGED)
            ItemRepository.DeductStock(
                connection,
                transaction,
                item.ItemId,
                baseQty,
                $"Sale {receiptNumber}",
                cashier,
                ActivityActions.ITEM_SOLD
            );
        }
                        
                    

                    var itemCmd = connection.CreateCommand();
                    itemCmd.Transaction = transaction;
                   itemCmd.CommandText = @"
                            INSERT INTO Sales (
                                ReceiptNumber,
                                ItemId,
                                ItemName,
                                UnitType,
                                UnitValue,
                                Quantity,
                                Price,
                                Total,
                                CostPrice,
                                Profit,
                                SaleType,
                                BranchId,
                                CreatedAt
                            )
                            VALUES (
                                @receipt,
                                @itemId,
                                @name,
                                @unitType,
                                @unitValue,
                                @qty,
                                @price,
                                @total,
                                @costPrice,
                                @profit,
                                @SaleType,
                                @branchId,
                                @created
                            );
                        ";

                        itemCmd.Parameters.AddWithValue("@receipt", receiptNumber);
                        itemCmd.Parameters.AddWithValue("@itemId", item.ItemId);
                        itemCmd.Parameters.AddWithValue("@name", item.Name);
                        itemCmd.Parameters.AddWithValue("@unitType", item.UnitType);
                        itemCmd.Parameters.AddWithValue("@unitValue", item.UnitValue);
                        itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                        itemCmd.Parameters.AddWithValue("@price", item.Price);
                        itemCmd.Parameters.AddWithValue("@total", item.Total);
                        itemCmd.Parameters.AddWithValue("@costPrice", costPrice);
                        itemCmd.Parameters.AddWithValue("@profit", lineProfit);
                        itemCmd.Parameters.AddWithValue("@saleType", item.SaleType ?? "Retail");
                        itemCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);   
                        itemCmd.Parameters.AddWithValue("@created", createdAt);

                    itemCmd.ExecuteNonQuery();
                }

                LoggerService.Info(
                    "📦",
                    "SALE",
                    "Sale items saved",
                    $"Count={items.Count}"
                );

                transaction.Commit();
                AppState.NotifyDataChanged();

                LoggerService.Info(
                    "✅",
                    "SALE",
                    "Sale committed successfully",
                    receiptNumber
                );

                return receiptNumber;
            }
            catch (Exception ex)
            {
                transaction.Rollback();

                LoggerService.Error(
                    "❌",
                    "SALE",
                    "Sale failed — rollback executed",
                    ex
                );

                throw;
            }
        }


public static void CompleteMpesaSale(
    string receipt,
    decimal amount,
    string code)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        UPDATE Transactions
        SET PaymentStatus = 'Completed',
            AmountPaid = @amount,
            MpesaCode = @code
        WHERE ReceiptNumber = @receipt
        ";

    cmd.Parameters.AddWithValue("@amount", amount);
    cmd.Parameters.AddWithValue("@code", code);
    cmd.Parameters.AddWithValue("@receipt", receipt);

    cmd.ExecuteNonQuery();

    PaymentRepository.SavePayment(receipt, "Mpesa", amount);
}

public static void SaveMpesaCodes(
    string receipt,
    string codes)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();

    cmd.CommandText = @"
        UPDATE Transactions
        SET MpesaCode = @codes
        WHERE ReceiptNumber = @receipt";

    cmd.Parameters.AddWithValue("@codes", codes);
    cmd.Parameters.AddWithValue("@receipt", receipt);

    cmd.ExecuteNonQuery();
}

public static List<PendingSale> GetPendingSales()
{
    var list = new List<PendingSale>();

    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ReceiptNumber, Total
        FROM Transactions
        WHERE PaymentStatus = 'Pending'
        AND CreatedAt >= DATEADD(MINUTE, -10, GETDATE())
        ORDER BY CreatedAt DESC";

    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        list.Add(new PendingSale
        {
            Receipt = reader.GetString(0),
            Total = reader.GetDecimal(1)
        });
    }

    return list;
}
public static List<PendingSale> GetPendingByAmount(decimal amount)
{
    var list = new List<PendingSale>();

    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ReceiptNumber, Total
        FROM Transactions
        WHERE PaymentStatus = 'Pending'
        AND Total = @amount
        ORDER BY CreatedAt DESC";

    cmd.Parameters.AddWithValue("@amount", amount);

    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        list.Add(new PendingSale
        {
            Receipt = reader.GetString(0),
            Total = reader.GetDecimal(1)
        });
    }

    return list;
}
public static PendingSale GetSaleByReceipt(string receipt)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ReceiptNumber, Total
        FROM Transactions
        WHERE ReceiptNumber = @receipt
        AND PaymentStatus = 'Pending'";

    cmd.Parameters.AddWithValue("@receipt", receipt);

    using var reader = cmd.ExecuteReader();

    if (!reader.Read())
        return null;

    return new PendingSale
    {
        Receipt = reader["ReceiptNumber"].ToString(),
        Total = (decimal)reader["Total"]
    };
}

}
}