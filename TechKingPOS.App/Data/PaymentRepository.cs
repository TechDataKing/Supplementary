using System;
using TechKingPOS.App.Services;
using TechKingPOS.App.Models;
using TechKingPOS.App.Security;
using TechKingPOS.App.Infrastructure;

namespace TechKingPOS.App.Data
{     
     
    public static class PaymentRepository
    {

        public static void SavePayment(
            string receiptNumber,
            string method,
            decimal amount)
        {
            if (amount <= 0) return;

            using var connection = DbService.GetConnection();
            connection.Open();

            using var tx = connection.BeginTransaction();

            // 1️⃣ Insert payment
            var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Payments (
                    ReceiptNumber,
                    Method,
                    Amount,
                    BranchId,
                    CreatedAt
                )
                VALUES (
                    @receipt,
                    @method,
                    @amount,
                    @branchId,
                    @created
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";

            cmd.Parameters.AddWithValue("@receipt", receiptNumber);
            cmd.Parameters.AddWithValue("@method", method);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
            cmd.Parameters.AddWithValue("@created", DateTime.Now);

            // Get inserted Payment Id
            int paymentId = (int)cmd.ExecuteScalar();

            // 2️⃣ Log activity
            ActivityRepository.Log(connection, tx, new Activity
            {
                EntityType = "Payment",
                EntityId = paymentId,
                EntityName = receiptNumber,
                Action = "PAYMENT_RECEIVED",
                UnitValue = amount,
                Reason = method,
                PerformedBy = SessionContext.CurrentUserName,
                BranchId = SessionContext.EffectiveBranchId,
                CreatedAt = DateTime.Now
            });

            tx.Commit();
            AppState.NotifyDataChanged();

        }
    }
}
