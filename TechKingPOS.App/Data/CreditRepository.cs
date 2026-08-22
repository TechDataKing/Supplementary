using System;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Data;
using TechKingPOS.App.Infrastructure;

namespace TechKingPOS.App.Data
{
    public static class CreditRepository
    {  
 
        public static Customer FindCustomer(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            return null;
            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP 1
                    Id, Name, Phone
                FROM Customers
                WHERE (BranchId = 0 OR BranchId = @branchId)
                AND (Name = @s OR Phone = @s)
                ORDER BY Id;


            ";
            cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
            cmd.Parameters.AddWithValue("@s", search);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new Customer
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Phone = r.IsDBNull(2) ? null : r.GetString(2)
            };
        }
        private static int GetLatestCreditId(int customerId)
            {
                using var conn = DbService.GetConnection();
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT TOP 1 Id
                    FROM Credits
                    WHERE CustomerId = @cid
                    AND (BranchId = 0 OR BranchId = @branchId)
                    ORDER BY Id DESC;

                ";
                cmd.Parameters.AddWithValue("@cid", customerId);
                cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

                var result = cmd.ExecuteScalar();
                if (result == null)
                    throw new Exception("No credit record found for customer");

                return Convert.ToInt32(result);
            }
public static void SaveCredit(
    string receiptNumber,
    string customerName,
    string phone,
    decimal total,
    
    decimal paid,
    decimal balance,
    decimal balancebefore
    )
{
    using var conn = DbService.GetConnection();
    conn.Open();
    using var tx = conn.BeginTransaction();

    try
    {
        // Step 1: Normalize phone number
        var normalizedPhone = NormalizePhone(phone);

        // Step 2: Find if customer exists by phone (ensuring unique phone entries)
        var findCmd = conn.CreateCommand();
        findCmd.Transaction = tx;
        findCmd.CommandText = @"
            SELECT Id, Name FROM Customers
            WHERE Phone = @p AND BranchId = @branchId
        ";
        findCmd.Parameters.AddWithValue("@p", normalizedPhone);
        findCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

        var customerReader = findCmd.ExecuteReader();
        int customerId = 0;

        if (customerReader.Read())
        {
            // If customer exists, use their ID
            customerId = customerReader.GetInt32(0);
        }
        customerReader.Close();

        // Step 3: If customer doesn't exist, insert the customer (only once)
        if (customerId == 0)
        {
            var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = @"
                INSERT INTO Customers (Name, Phone, BranchId, CreatedAt)
                VALUES (@n, @p, @branchId, @c);

                SELECT SCOPE_IDENTITY(); -- Get the newly created customer Id
            ";

            insertCmd.Parameters.AddWithValue("@n", customerName);
            insertCmd.Parameters.AddWithValue("@p", normalizedPhone);
            insertCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
            insertCmd.Parameters.AddWithValue("@c", DateTime.Now);

            customerId = Convert.ToInt32(insertCmd.ExecuteScalar());
        }
            decimal loanAmount = total - paid;

                // ❗ If fully paid → no credit
                if (loanAmount <= 0)
                {
                    tx.Commit();
                    return;
                }

        // Step 4: Insert the new credit record (no conflict with unique key as multiple credits are allowed)
        var creditCmd = conn.CreateCommand();
        creditCmd.Transaction = tx;
        creditCmd.CommandText = @"
            INSERT INTO Credits 
            (CustomerId, ReceiptNumber, Total, Paid, Balance,LoanAmount, BalanceBefore, BranchId, CreatedAt)
            VALUES 
            (@cid, @r, @t, @p, @b,@l,@bb, @branchId, @c);
        ";
        creditCmd.Parameters.AddWithValue("@cid", customerId);
        creditCmd.Parameters.AddWithValue("@r", receiptNumber); // Ensure that receipt number is unique per credit
        creditCmd.Parameters.AddWithValue("@t", total);
        creditCmd.Parameters.AddWithValue("@p", paid);
        creditCmd.Parameters.AddWithValue("@l", loanAmount);
        creditCmd.Parameters.AddWithValue("@b", balance);
        creditCmd.Parameters.AddWithValue("@bb", balancebefore);
        creditCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
        creditCmd.Parameters.AddWithValue("@c", DateTime.Now);
        creditCmd.ExecuteNonQuery();

 

        // 6️⃣ Log the Activity (optional)
        ActivityRepository.Log(conn, tx, new Activity
        {
            EntityType = "Credit",
            EntityId = customerId,
            EntityName = customerName,
            Action = "CREDIT_CREATED",
            AfterValue = $"Receipt={receiptNumber}, Total={total:0.00}, Paid={paid:0.00}, Balance={balance:0.00}",
            PerformedBy = SessionContext.CurrentUserName,
            BranchId = SessionContext.EffectiveBranchId,
            CreatedAt = DateTime.Now
        });

        tx.Commit();
        AppState.NotifyDataChanged();
    }
    catch (Exception ex)
    {
        tx.Rollback();
        throw new Exception("Error while saving credit: " + ex.Message);
    }
}private static string NormalizePhone(string phone)
{
    if (string.IsNullOrWhiteSpace(phone))
        return "";

    phone = phone.Trim();

    // remove spaces
    phone = phone.Replace(" ", "");

    // normalize Kenyan format (optional but VERY useful)
    if (phone.StartsWith("07"))
        phone = "254" + phone.Substring(1);

    if (phone.StartsWith("+254"))
        phone = phone.Substring(1);

    return phone;
}

public static void AddPayment(
    int customerId,
    decimal amount,
    string method,
    string? receiptNumber = null,
    decimal? balanceBefore = null,
    decimal? balanceAfter = null)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var tx = conn.BeginTransaction();

    try
    {
        if (amount <= 0)
            throw new Exception("Invalid payment amount.");

        decimal remaining = amount;

        method = method?.Trim() ?? "Unknown";

        var creditsCmd = conn.CreateCommand();
        creditsCmd.Transaction = tx;

        creditsCmd.CommandText = @"
            SELECT Id, Balance
            FROM Credits
            WHERE CustomerId = @cid
              AND Balance > 0
              AND (BranchId = 0 OR BranchId = @branchId)
            ORDER BY CreatedAt ASC
        ";

        creditsCmd.Parameters.AddWithValue("@cid", customerId);
        creditsCmd.Parameters.AddWithValue(
            "@branchId",
            SessionContext.EffectiveBranchId
        );

        var credits = new List<(int Id, decimal Balance)>();

        using (var reader = creditsCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                credits.Add((
                    reader.GetInt32(0),
                    reader.GetDecimal(1)
                ));
            }
        }

        if (credits.Count == 0)
            throw new Exception(
                "Customer has no outstanding balance."
            );

        decimal totalOutstanding = credits.Sum(x => x.Balance);

        if (amount > totalOutstanding)
        {
            throw new Exception(
                $"Payment exceeds balance. Debt: {totalOutstanding:0.00}"
            );
        }

        foreach (var credit in credits)
        {
            if (remaining <= 0)
                break;

            decimal payAmount = Math.Min(
                remaining,
                credit.Balance
            );

            // 1️⃣ Save payment

            var paymentCmd = conn.CreateCommand();
            paymentCmd.Transaction = tx;

            paymentCmd.CommandText = @"
                INSERT INTO CreditPayments
                (
                    CreditId,
                    ReceiptNumber,
                    Amount,
                    Method,
                    BalanceBefore,
                    BalanceAfter,
                    BranchId,
                    CreatedAt
                )
                VALUES
                (
                    @creditId,
                    @receipt,
                    @amount,
                    @method,
                    @balanceBefore,
                    @balanceAfter,
                    @branchId,
                    @created
                )
                ";

            paymentCmd.Parameters.AddWithValue(
                "@creditId",
                credit.Id
            );

            paymentCmd.Parameters.AddWithValue(
                "@receipt",
                (object?)receiptNumber ?? DBNull.Value
            );

            paymentCmd.Parameters.AddWithValue(
                "@amount",
                payAmount
            );

            paymentCmd.Parameters.AddWithValue(
                "@method",
                method
            );

            paymentCmd.Parameters.AddWithValue(
                "@balanceBefore",
                balanceBefore ?? credit.Balance
            );

            paymentCmd.Parameters.AddWithValue(
                "@balanceAfter",
                balanceAfter ?? Math.Max(0, credit.Balance - payAmount)
            );

            paymentCmd.Parameters.AddWithValue(
                "@branchId",
                SessionContext.EffectiveBranchId
            );

            paymentCmd.Parameters.AddWithValue(
                "@created",
                DateTime.Now
            );

            paymentCmd.ExecuteNonQuery();

            // 2️⃣ Update credit

            var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;

            updateCmd.CommandText = @"
                UPDATE Credits
                SET Paid = Paid + @amount,
                    Balance = Balance - @amount
                WHERE Id = @creditId
            ";

            updateCmd.Parameters.AddWithValue(
                "@amount",
                payAmount
            );

            updateCmd.Parameters.AddWithValue(
                "@creditId",
                credit.Id
            );

            updateCmd.ExecuteNonQuery();

            remaining -= payAmount;
        }

        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

public static decimal GetCustomerBalance(int customerId)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ISNULL(SUM(Balance), 0)
        FROM Credits
        WHERE CustomerId = @cid
        AND Balance > 0
        AND (BranchId = 0 OR BranchId = @branchId);
    ";

    cmd.Parameters.AddWithValue("@cid", customerId);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    var result = cmd.ExecuteScalar();
    return result == null ? 0m : Convert.ToDecimal(result);
}

public static List<Customer> SearchCustomers(string text)
{
    var list = new List<Customer>();

    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT TOP 10 Id, Name, Phone
        FROM Customers
        WHERE (Name LIKE @t OR Phone LIKE @t)
        AND (BranchId = 0 OR BranchId = @branchId)
        ORDER BY Name
    ";

    cmd.Parameters.AddWithValue("@t", "%" + text + "%");
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(new Customer
        {
            Id = r.GetInt32(0),
            Name = r.GetString(1),
            Phone = r.GetString(2)
        });
    }

    return list;
}

    }
}
