using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Windows;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;

namespace TechKingPOS.App.Data
{
    public static class ReportsRepository
    {
        // ================= SALES SUMMARY =================
public static SalesSummary GetSalesSummary(
    DateTime from,
    DateTime to,
    int branchId)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    LoggerService.Info(
        "📊",
        "REPORT",
        "Loading sales summary",
        $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}"
    );

    var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT
            COUNT(DISTINCT t.ReceiptNumber),
            ISNULL(SUM(t.Total), 0),
            ISNULL(SUM(t.Tax), 0),
            ISNULL(SUM(t.Discount), 0),
            ISNULL(SUM(t.AmountPaid), 0),
            ISNULL(SUM(sp.TotalProfit), 0)
        FROM Transactions t
        LEFT JOIN (
            SELECT
                ReceiptNumber,
                SUM(Profit) AS TotalProfit
            FROM Sales
            GROUP BY ReceiptNumber
        ) sp ON sp.ReceiptNumber = t.ReceiptNumber
        WHERE t.CreatedAt BETWEEN @from AND @to
        AND (@branchId = 0 OR t.BranchId = @branchId);
    ";

    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = to.Date.AddDays(1);
    cmd.Parameters.AddWithValue("@branchId", branchId);

    using var reader = cmd.ExecuteReader();
    reader.Read();

    var summary = new SalesSummary
    {
        ReceiptCount = reader.GetInt32(0),
        TotalSales   = reader.GetDecimal(1),
        Tax          = reader.GetDecimal(2),
        Discount     = reader.GetDecimal(3),
        AmountPaid   = reader.GetDecimal(4),
        Profit       = reader.GetDecimal(5)
    };

    LoggerService.Info(
        "✅",
        "REPORT",
        "Sales summary loaded",
        $"Receipts={summary.ReceiptCount}, Total={summary.TotalSales}, Profit={summary.Profit}"
    );

    return summary;
}



        // ================= SOLD ITEMS =================
        public static List<SoldItemReport> GetSoldItems(
            DateTime from,
            DateTime to, int branchId)
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            LoggerService.Info(
                "📦",
                "REPORT",
                "Loading sold items",
                $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}"
            );

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT
                s.ReceiptNumber,
                s.ItemName,
                SUM(s.Quantity) AS Quantity,
                s.Price,
                SUM(s.Total) AS Total,
                MIN(s.CreatedAt) AS CreatedAt,
                t.Cashier,
                s.SaleType,
                ISNULL(pm.PaymentMethod, 'CREDIT') AS PaymentMethod
            FROM Sales s
            JOIN Transactions t 
                ON t.ReceiptNumber = s.ReceiptNumber
            LEFT JOIN (
                SELECT 
                    ReceiptNumber,
                    STRING_AGG(Method, ', ') AS PaymentMethod
                FROM Payments
                GROUP BY ReceiptNumber
            ) pm ON pm.ReceiptNumber = s.ReceiptNumber
            WHERE t.CreatedAt BETWEEN @from AND @to
            AND t.BranchId = @branchId
            GROUP BY 
                s.ReceiptNumber,
                s.ItemName,
                s.Price,
                t.Cashier,
                s.SaleType,
                pm.PaymentMethod
            ORDER BY MIN(s.CreatedAt) ASC;
            ";

            cmd.Parameters.Add("@from", SqlDbType.DateTime2).Value = from;
            cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = to;
            cmd.Parameters.Add("@branchId", SqlDbType.Int).Value = branchId;

            var result = new List<SoldItemReport>();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SoldItemReport
                {
                    ReceiptNumber = reader.GetString(reader.GetOrdinal("ReceiptNumber")),
                    ItemName      = reader.GetString(reader.GetOrdinal("ItemName")),
                    Quantity      = reader.GetDecimal(reader.GetOrdinal("Quantity")),
                    Price         = reader.GetDecimal(reader.GetOrdinal("Price")),
                    Total         = reader.GetDecimal(reader.GetOrdinal("Total")),
                    PaymentMethod = reader["PaymentMethod"]?.ToString(),
                    Cashier   = reader["Cashier"]?.ToString(),
                    SaleType = reader.GetString(reader.GetOrdinal("SaleType"))
                });
            }

            LoggerService.Info(
                "✅",
                "REPORT",
                "Sold items loaded",
                $"Items={result.Count}"
            );

            return result;
        }

public static ReceiptPreviewData GetReceiptDetails(string receiptNumber)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    var data = new ReceiptPreviewData();

    // ================= 1️⃣ TRANSACTION =================

    var txCmd = connection.CreateCommand();

    txCmd.CommandText = @"
        SELECT
            t.Total,
            t.Discount,
            t.Tax,
            t.AmountPaid,
            c.BalanceBefore,
            c.Balance AS BalanceAfter,
            t.Cashier,
            t.CreatedAt
        FROM Transactions t
        LEFT JOIN Credits c
            ON c.ReceiptNumber = t.ReceiptNumber
        WHERE t.ReceiptNumber = @receipt";

    txCmd.Parameters.AddWithValue("@receipt", receiptNumber);

    using var txReader = txCmd.ExecuteReader();

    if (!txReader.Read())
        return null;

    decimal total = Convert.ToDecimal(txReader["Total"]);
    decimal discount = Convert.ToDecimal(txReader["Discount"]);
    decimal tax = Convert.ToDecimal(txReader["Tax"]);
    decimal paid = Convert.ToDecimal(txReader["AmountPaid"]);

    decimal balBefore =
        txReader["BalanceBefore"] == DBNull.Value
            ? 0m
            : Convert.ToDecimal(txReader["BalanceBefore"]);

    decimal balAfter =
        txReader["BalanceAfter"] == DBNull.Value
            ? 0m
            : Convert.ToDecimal(txReader["BalanceAfter"]);

    data.Cashier = txReader["Cashier"]?.ToString();

    txReader.Close();

    // ================= 2️⃣ PAYMENTS =================

    var payCmd = connection.CreateCommand();

    payCmd.CommandText = @"
        SELECT Method, Amount
        FROM Payments
        WHERE ReceiptNumber = @receipt";

    payCmd.Parameters.AddWithValue("@receipt", receiptNumber);

    decimal cash = 0m;
    decimal mpesa = 0m;
    decimal debtPaymentAmount = 0m;

    bool hasCash = false;
    bool hasMpesa = false;
    bool isDebtPayment = false;

    using var payReader = payCmd.ExecuteReader();

    while (payReader.Read())
    {
        string method = payReader["Method"]?.ToString() ?? "";

        decimal amount =
            Convert.ToDecimal(payReader["Amount"]);

        switch (method)
        {
            case "Cash":
                cash += amount;
                hasCash = true;
                break;

            case "Mpesa":
                mpesa += amount;
                hasMpesa = true;
                break;

            case "Debt Payment":
                debtPaymentAmount += amount;
                isDebtPayment = true;
                break;
        }
    }

    payReader.Close();

    string paymentMethod;

    if (hasCash && hasMpesa)
    {
        paymentMethod = "Cash + Mpesa";
    }
    else if (hasCash)
    {
        paymentMethod = "Cash";
    }
    else if (hasMpesa)
    {
        paymentMethod = "Mpesa";
    }
    else if (isDebtPayment)
    {
        paymentMethod = "Debt Payment";
    }
    else
    {
        paymentMethod = "Credit";
    }

    data.PaymentMethod = paymentMethod;

    // ================= 3️⃣ CUSTOMER =================

    string customerName = "";
    string phone = "";

    // 🔥 DEBT PAYMENT

    if (isDebtPayment)
    {
        var creditCmd = connection.CreateCommand();

        creditCmd.CommandText = @"
            SELECT TOP 1
                c.Name,
                c.Phone,
                cp.BalanceBefore,
                cp.BalanceAfter
            FROM CreditPayments cp
            JOIN Credits cr
                ON cp.CreditId = cr.Id
            JOIN Customers c
                ON c.Id = cr.CustomerId
            WHERE cp.ReceiptNumber = @receipt";

        creditCmd.Parameters.AddWithValue(
            "@receipt",
            receiptNumber);

        using var creditReader =
            creditCmd.ExecuteReader();

        if (creditReader.Read())
        {
            customerName =
                creditReader["Name"]?.ToString() ?? "";

            phone =
                creditReader["Phone"]?.ToString() ?? "";

            balBefore =
                Convert.ToDecimal(
                    creditReader["BalanceBefore"]);

            balAfter =
                Convert.ToDecimal(
                    creditReader["BalanceAfter"]);
        }
    }

    // 🔥 CREDIT SALE

    else if (cash == 0 && mpesa == 0)
    {
        var creditCmd = connection.CreateCommand();

        creditCmd.CommandText = @"
            SELECT
                c.Name,
                c.Phone,
                cr.BalanceBefore,
                cr.Balance
            FROM Credits cr
            JOIN Customers c
                ON c.Id = cr.CustomerId
            WHERE cr.ReceiptNumber = @receipt";

        creditCmd.Parameters.AddWithValue(
            "@receipt",
            receiptNumber);

        using var creditReader =
            creditCmd.ExecuteReader();

        if (creditReader.Read())
        {
            customerName =
                creditReader["Name"]?.ToString() ?? "";

            phone =
                creditReader["Phone"]?.ToString() ?? "";

            balBefore =
                creditReader["BalanceBefore"] == DBNull.Value
                    ? 0m
                    : Convert.ToDecimal(
                        creditReader["BalanceBefore"]);

            balAfter =
                Convert.ToDecimal(
                    creditReader["Balance"]);
        }
    }

    // ================= 4️⃣ PAYMENT RESULT =================

    var payment = new PaymentResult(
        total - tax + discount,
        discount,
        tax,
        total,
        balBefore,
        balAfter
    );

    payment.BalanceBefore = balBefore;
    payment.BalanceAfter = balAfter;

    payment.SetPayment(
        cash,
        mpesa,
        customerName,
        phone
    );

    payment.PaymentMethod = paymentMethod;
    payment.IsDebtPayment = isDebtPayment;
    payment.CreditPaid = debtPaymentAmount;

    data.Payment = payment;

    // ================= 5️⃣ ITEMS =================

    var itemsCmd = connection.CreateCommand();

    itemsCmd.CommandText = @"
        SELECT
            ItemName,
            Quantity,
            Price,
            Total
        FROM Sales
        WHERE ReceiptNumber = @receipt";

    itemsCmd.Parameters.AddWithValue(
        "@receipt",
        receiptNumber);

    using var itemsReader =
        itemsCmd.ExecuteReader();

    while (itemsReader.Read())
    {
        data.Items.Add(
            new SaleItem
            {
                Name = itemsReader["ItemName"]
                    ?.ToString() ?? "",

                Quantity = Convert.ToDecimal(
                    itemsReader["Quantity"]),

                Price = Convert.ToDecimal(
                    itemsReader["Price"])
            });
    }

    return data;
}




public static List<CreditView> GetCredits(DateTime? from = null, DateTime? to = null, int branchId = 0)
{
    var credits = new List<CreditView>();

    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT
            c.Id,
            c.CustomerId,   -- Ensure you're selecting the CustomerId as well
            cust.Name,
            cust.Phone,
            c.Total,
            c.Paid,
            c.Balance,
            c.ReceiptNumber,
            c.CreatedAt,
            COALESCE(
                (
                    SELECT MAX(cp.CreatedAt)
                    FROM CreditPayments cp
                    WHERE cp.CreditId = c.Id
                ),
                c.CreatedAt
            ) AS LastPaymentDate
        FROM Credits c
        JOIN Customers cust ON cust.Id = c.CustomerId
        WHERE c.Balance > 0
          AND (@from IS NULL OR c.CreatedAt >= @from)
          AND (@to IS NULL OR c.CreatedAt <= @to)
          AND (@branchId = 0 OR c.BranchId = @branchId)
        ORDER BY LastPaymentDate DESC;
    ";

    cmd.Parameters.AddWithValue("@branchId", branchId);
    cmd.Parameters.AddWithValue("@from", from.HasValue ? from.Value : DBNull.Value);
    cmd.Parameters.AddWithValue("@to", to.HasValue ? to.Value.AddDays(1).AddSeconds(-1) : DBNull.Value);

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        decimal balance = reader.GetDecimal(5);
        DateTime lastPayment = reader.GetDateTime(8);

        credits.Add(new CreditView
        {
            CreditId        = reader.GetInt32(0),
            CustomerId      = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),  // Handle potential null for CustomerId
            CustomerName    = reader.GetString(2),
            Phone           = reader.IsDBNull(3) ? "" : reader.GetString(3),
            Total           = reader.GetDecimal(4),
            Paid            = reader.GetDecimal(5),
            Balance         = reader.GetDecimal(6),
            ReceiptNumber   = reader.IsDBNull(7) ? "": reader["ReceiptNumber"].ToString(),
            CreatedAt       = reader.GetDateTime(8),
            LastPaymentDate = lastPayment
        });
    }

    return credits;
}

public static List<CreditPaymentView> GetCustomerStatement(
    int customerId,
    DateTime? from = null,
    DateTime? to = null)
{
    var list = new List<CreditPaymentView>();

    using var conn = DbService.GetConnection();
    conn.Open();

    DateTime fromDate = from ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    DateTime toDate = to ?? DateTime.Now;

    var events = new List<(DateTime Date, string Type, decimal Amount, string Desc)>();

    // 🔹 1. GET LOANS (DR) → USE LoanAmount ONLY
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT LoanAmount, ReceiptNumber, CreatedAt
            FROM Credits
            WHERE CustomerId = @cid
            AND CreatedAt BETWEEN @from AND @to
        ";

        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@from", fromDate);
        cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = toDate.Date.AddDays(1);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            decimal loanAmount = r.IsDBNull(0) ? 0 : r.GetDecimal(0);

            if (loanAmount <= 0) continue;

            events.Add((
                r.GetDateTime(2),
                "DR",
                loanAmount,
                $"Loan Issued (Receipt {r.GetString(1)})"
            ));
        }
    }

    // 🔹 2. GET PAYMENTS (CR)
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT cp.Amount, cp.Method, cp.CreatedAt
            FROM CreditPayments cp
            JOIN Credits c ON c.Id = cp.CreditId
            WHERE c.CustomerId = @cid
            AND cp.CreatedAt BETWEEN @from AND @to
        ";

        cmd.Parameters.AddWithValue("@cid", customerId);
        cmd.Parameters.AddWithValue("@from", fromDate);
        cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = toDate.Date.AddDays(1);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            events.Add((
                r.GetDateTime(2),
                "CR",
                r.GetDecimal(0),
                $"Payment ({r.GetString(1)})"
            ));
        }
    }

    // 🔹 3. ORDER EVENTS (VERY IMPORTANT)
    var ordered = events
        .OrderBy(x => x.Date)
        .ThenBy(x => x.Type) // DR first then CR (optional but cleaner)
        .ToList();

    // 🔹 4. RUNNING BALANCE
    decimal runningBalance = 0;

    foreach (var e in ordered)
    {
        if (e.Type == "DR")
            runningBalance += e.Amount;
        else
            runningBalance -= e.Amount;

        list.Add(new CreditPaymentView
        {
            CreatedAt = e.Date,
            Description = e.Desc,
            Debit = e.Type == "DR" ? e.Amount : 0,
            Credit = e.Type == "CR" ? e.Amount : 0,
            Balance = runningBalance
        });
    }

    return list;
}

public static decimal GetReturnsTotal(DateTime from, DateTime to, int branchId)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ISNULL(SUM(Price), 0)
        FROM Returns
        WHERE ReturnDate BETWEEN @from AND @to
        AND ReturnType = 'Customer Return'
        AND (BranchId = 0 OR BranchId = @branchId)
    ";

    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = to.Date.AddDays(1);
    cmd.Parameters.AddWithValue("@branchId", branchId);

    var result = cmd.ExecuteScalar();
    return result == null ? 0m : Convert.ToDecimal(result);
}

public static decimal GetRestockedReturnsTotal(DateTime from, DateTime to, int branchId)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ISNULL(SUM(Price), 0)
        FROM Returns
        WHERE ReturnDate BETWEEN @from AND @to
        AND Restock = 1
        AND ReturnType = 'Customer Return'
        AND (BranchId = 0 OR BranchId = @branchId)
    ";

    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = to.Date.AddDays(1);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    var result = cmd.ExecuteScalar();
    return result == null ? 0m : Convert.ToDecimal(result);
}

public static decimal GetNetSales(DateTime from, DateTime to)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT 
            ISNULL(SUM(CASE WHEN Restock = 1 THEN Price ELSE 0 END), 0)
        FROM Returns
        WHERE ReturnDate BETWEEN @from AND @to
        AND ReturnType = 'Customer Return'
        AND (@branchId = 0 OR BranchId = @branchId);
    ";

    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = to.Date.AddDays(1);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    decimal restockedReturns = Convert.ToDecimal(cmd.ExecuteScalar());

    // 👉 reuse your existing sales summary
    var sales = GetSalesSummary(from, to, SessionContext.EffectiveBranchId);

    return sales.TotalSales - restockedReturns;
}

public static decimal GetTotalReturns(DateTime from, DateTime to)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ISNULL(SUM(Price), 0)
        FROM Returns
        WHERE ReturnDate BETWEEN @from AND @to
        AND ReturnType = 'Customer Return'
        AND (@branchId = 0 OR BranchId = @branchId);
    ";

    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.Add("@to", SqlDbType.DateTime2).Value = to.Date.AddDays(1);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    return Convert.ToDecimal(cmd.ExecuteScalar());
}




    }
}
