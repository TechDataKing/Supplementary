using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;

namespace TechKingPOS.App.Data
{
    public static class SettingsRepository
    {
        public static AppSetting Get()
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP 1 * FROM AppSettings";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            var s = new AppSetting
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),

                BusinessName = reader.IsDBNull(reader.GetOrdinal("BusinessName")) ? "" : reader.GetString(reader.GetOrdinal("BusinessName")),
                BranchName = reader.IsDBNull(reader.GetOrdinal("BranchName")) ? "" : reader.GetString(reader.GetOrdinal("BranchName")),
                Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? "" : reader.GetString(reader.GetOrdinal("Phone")),
                Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                PhysicalAddress = reader.IsDBNull(reader.GetOrdinal("PhysicalAddress")) ? "" : reader.GetString(reader.GetOrdinal("PhysicalAddress")),
                ReceiptFooter = reader.IsDBNull(reader.GetOrdinal("ReceiptFooter")) ? "" : reader.GetString(reader.GetOrdinal("ReceiptFooter")),

                AutoPrintReceipt = reader.GetBoolean(reader.GetOrdinal("AutoPrintReceipt")),
                SignReceipt = reader.GetBoolean(reader.GetOrdinal("SignReceipt")),
                ShowCashierOnReceipt = reader.GetBoolean(reader.GetOrdinal("ShowCashierOnReceipt")),
                ShowLogoOnReceipt = reader.GetBoolean(reader.GetOrdinal("ShowLogoOnReceipt")),
                PaperSize = reader.GetString(reader.GetOrdinal("PaperSize")),
                PrintLayout = reader.IsDBNull(reader.GetOrdinal("PrintLayout")) ? "": reader.GetString(reader.GetOrdinal("PrintLayout")),
                ReceiptCopies = reader.GetInt32(reader.GetOrdinal("ReceiptCopies")),
                AllowNegativeStock = reader.GetBoolean(reader.GetOrdinal("AllowNegativeStock")),
                AllowPriceEditDuringSale = reader.GetBoolean(reader.GetOrdinal("AllowPriceEditDuringSale")),
                EnableCreditSales = reader.GetBoolean(reader.GetOrdinal("EnableCreditSales")),

                VatEnabled = reader.GetBoolean(reader.GetOrdinal("VatEnabled")),
                VatPercent = reader.GetDecimal(reader.GetOrdinal("VatPercent")),

                RequireLogin = reader.GetBoolean(reader.GetOrdinal("RequireLogin")),
                AutoLogout = reader.GetBoolean(reader.GetOrdinal("AutoLogout")),
                AllowVoidSales = reader.GetBoolean(reader.GetOrdinal("AllowVoidSales")),
                AllowWorkersEditPrices = reader.GetBoolean(reader.GetOrdinal("AllowWorkersEditPrices")),
                AllowWorkersGiveDiscounts = reader.GetBoolean(reader.GetOrdinal("AllowWorkersGiveDiscounts")),

                LicenseKey = reader.IsDBNull(reader.GetOrdinal("LicenseKey")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("LicenseKey")),
                LicenseExpiry = reader.IsDBNull(reader.GetOrdinal("LicenseExpiry")) 
                                ? (DateTime?)null 
                                : reader.GetDateTime(reader.GetOrdinal("LicenseExpiry")),
                LicenseActivated = reader.GetBoolean(reader.GetOrdinal("LicenseActivated")),
                LicenseInstallDate = reader.IsDBNull(reader.GetOrdinal("LicenseInstallDate")) 
                                        ? null 
                                        : reader.GetDateTime(reader.GetOrdinal("LicenseInstallDate")),
                LicenseExpiryDate = reader.IsDBNull(reader.GetOrdinal("LicenseExpiryDate")) 
                                        ? null 
                                        : reader.GetDateTime(reader.GetOrdinal("LicenseExpiryDate")),
                LicenseMachineId = reader.IsDBNull(reader.GetOrdinal("LicenseMachineId")) 
                                        ? null 
                                        : reader.GetString(reader.GetOrdinal("LicenseMachineId")),


                EnableDiscounts = reader.GetBoolean(reader.GetOrdinal("EnableDiscounts")),

                AllowFixedDiscount = reader.GetBoolean(reader.GetOrdinal("AllowFixedDiscount")),
                AllowPercentageDiscount = reader.GetBoolean(reader.GetOrdinal("AllowPercentageDiscount")),
                AllowConditionalDiscount = reader.GetBoolean(reader.GetOrdinal("AllowConditionalDiscount")),
                MaxFixedDiscount = reader.GetDecimal(reader.GetOrdinal("MaxFixedDiscount")),
                MaxPercentageDiscount = reader.GetDecimal(reader.GetOrdinal("MaxPercentageDiscount")),

                CondValueFixed = reader.GetBoolean(reader.GetOrdinal("CondValueFixed")),
                CondValuePercent = reader.GetBoolean(reader.GetOrdinal("CondValuePercent")),
                CondBasedRanges = reader.GetBoolean(reader.GetOrdinal("CondBasedRanges")),

                ConditionalMinSubtotal = reader.GetDecimal(reader.GetOrdinal("ConditionalMinSubtotal")),
                ConditionalDiscountAmount = reader.GetDecimal(reader.GetOrdinal("ConditionalDiscountAmount")),
                SecondaryPhone = reader.IsDBNull(reader.GetOrdinal("SecondaryPhone"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("SecondaryPhone")),

                LogoImage = reader.IsDBNull(reader.GetOrdinal("LogoImage"))
                    ? null
                    : reader.GetFieldValue<byte[]>(reader.GetOrdinal("LogoImage")),
                EnableEtims = reader.GetBoolean(reader.GetOrdinal("EnableEtims")),
                KraPin = reader.IsDBNull(reader.GetOrdinal("KraPin")) ? "" : reader.GetString(reader.GetOrdinal("KraPin")),
                BhfId = reader.IsDBNull(reader.GetOrdinal("BhfId")) ? "" : reader.GetString(reader.GetOrdinal("BhfId")),
                DeviceSerial = reader.IsDBNull(reader.GetOrdinal("DeviceSerial")) ? "" : reader.GetString(reader.GetOrdinal("DeviceSerial")),
                EtimsUsername = reader.IsDBNull(reader.GetOrdinal("EtimsUsername")) ? "" : reader.GetString(reader.GetOrdinal("EtimsUsername")),
                EtimsPassword = reader.IsDBNull(reader.GetOrdinal("EtimsPassword")) ? "" : reader.GetString(reader.GetOrdinal("EtimsPassword")),
            };

            s.DiscountRanges = DiscountRangeRepository.GetAll();

            return s;
        }

        public static void Save(AppSetting s)
        {
            var oldSettings = Get();   // snapshot BEFORE save
            LogChangedSettings(oldSettings, s);

            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM AppSettings WHERE Id = 1)
    UPDATE AppSettings SET
        BusinessName = @name,
        BranchName = @branch,
        Phone = @phone,
        SecondaryPhone = @secondaryPhone,
        Email = @email,
        PhysicalAddress = @addr,
        ReceiptFooter = @footer,
        AutoPrintReceipt = @autoPrint,
        ShowCashierOnReceipt = @showCashier,
        ShowLogoOnReceipt = @showLogo,
        LogoImage = @logoImage,
        PaperSize = @paper,
        PrintLayout = @layout,
        ReceiptCopies = @copies,
        SignReceipt = @sign,
        AllowNegativeStock = @negStock,
        AllowPriceEditDuringSale = @editPrice,
        EnableCreditSales = @credit,
        VatEnabled = @vat,
        VatPercent = @vatp,
        EnableEtims = @enableEtims,
        KraPin = @kraPin,
        BhfId = @bhfId,
        DeviceSerial = @deviceSerial,
        EtimsUsername = @etimsUsername,
        EtimsPassword = @etimsPassword,
        RequireLogin = @login,
        AutoLogout = @logout,
        AllowVoidSales = @void,
        AllowWorkersEditPrices = @workerEdit,
        AllowWorkersGiveDiscounts = @workerDisc,
        LicenseKey = @key,
        LicenseExpiry = @expiry,
        LicenseActivated = @activated,
        LicenseInstallDate = @installDate,
        LicenseExpiryDate = @expiryDate,
        LicenseMachineId = @machineId,
        EnableDiscounts = @enableDisc,
        AllowFixedDiscount = @fixed,
        AllowPercentageDiscount = @percent,
        AllowConditionalDiscount = @conditional,
        MaxFixedDiscount = @maxFixed,
        MaxPercentageDiscount = @maxPercent,
        CondValueFixed = @cvf,
        CondValuePercent = @cvp,
        CondBasedRanges = @ranges,
        ConditionalMinSubtotal = @minSubtotal,
        ConditionalDiscountAmount = @condAmount
WHERE Id = 1
ELSE
    INSERT INTO AppSettings (
        BusinessName, BranchName, Phone,SecondaryPhone, Email, PhysicalAddress, ReceiptFooter,
        AutoPrintReceipt, ShowCashierOnReceipt, ShowLogoOnReceipt,LogoImage, PaperSize,PrintLayout, ReceiptCopies,SignReceipt,
        AllowNegativeStock, AllowPriceEditDuringSale, EnableCreditSales,
        VatEnabled, VatPercent,
        EnableEtims,KraPin,BhfId,DeviceSerial,EtimsUsername,EtimsPassword,
        RequireLogin, AutoLogout, AllowVoidSales, AllowWorkersEditPrices, AllowWorkersGiveDiscounts,
        LicenseKey, LicenseExpiry,LicenseActivated, LicenseInstallDate, LicenseExpiryDate, LicenseMachineId,
        EnableDiscounts, AllowFixedDiscount, AllowPercentageDiscount, AllowConditionalDiscount,
        MaxFixedDiscount, MaxPercentageDiscount,
        CondValueFixed, CondValuePercent, CondBasedRanges,
        ConditionalMinSubtotal, ConditionalDiscountAmount
    )
    VALUES (
        @name, @branch, @phone,@secondaryPhone, @email, @addr, @footer,
        @autoPrint, @showCashier, @showLogo,@logoImage, @paper, @layout, @copies, @sign,
        @negStock, @editPrice, @credit,
        @vat, @vatp,
        @enableEtims,@kraPin,@bhfId,@deviceSerial,@etimsUsername,@etimsPassword,
        @login, @logout, @void, @workerEdit, @workerDisc,
        @key, @expiry,@activated, @installDate, @expiryDate, @machineId,
        @enableDisc, @fixed, @percent, @conditional,
        @maxFixed, @maxPercent,
        @cvf, @cvp, @ranges,
        @minSubtotal, @condAmount
    );
";

            cmd.Parameters.AddWithValue("@name", s.BusinessName ?? "");
            cmd.Parameters.AddWithValue("@branch", s.BranchName ?? "");
            cmd.Parameters.AddWithValue("@phone", s.Phone ?? "");
            cmd.Parameters.AddWithValue("@secondaryPhone", s.SecondaryPhone ?? "");
            cmd.Parameters.AddWithValue("@email", s.Email ?? "");
            cmd.Parameters.AddWithValue("@addr", s.PhysicalAddress ?? "");
            cmd.Parameters.AddWithValue("@footer", s.ReceiptFooter ?? "");

            cmd.Parameters.AddWithValue("@autoPrint", s.AutoPrintReceipt);
            cmd.Parameters.AddWithValue("@showCashier", s.ShowCashierOnReceipt);
            cmd.Parameters.AddWithValue("@showLogo", s.ShowLogoOnReceipt);
            var logoParam = cmd.Parameters.Add("@logoImage", System.Data.SqlDbType.VarBinary);

                if (s.LogoImage != null)
                    logoParam.Value = s.LogoImage;
                else
                    logoParam.Value = DBNull.Value;
            cmd.Parameters.AddWithValue("@paper", s.PaperSize ?? "80mm");
            cmd.Parameters.AddWithValue("@layout", s.PrintLayout ?? "Thermal");
            cmd.Parameters.AddWithValue("@copies", s.ReceiptCopies);
            cmd.Parameters.AddWithValue("@sign", s.SignReceipt);

            cmd.Parameters.AddWithValue("@negStock", s.AllowNegativeStock);
            cmd.Parameters.AddWithValue("@editPrice", s.AllowPriceEditDuringSale);
            cmd.Parameters.AddWithValue("@credit", s.EnableCreditSales);

            cmd.Parameters.AddWithValue("@vat", s.VatEnabled);
            cmd.Parameters.AddWithValue("@vatp", s.VatPercent);

            cmd.Parameters.AddWithValue("@enableEtims", s.EnableEtims);
            cmd.Parameters.AddWithValue("@kraPin", s.KraPin ?? "");
            cmd.Parameters.AddWithValue("@bhfId", s.BhfId ?? "");
            cmd.Parameters.AddWithValue("@deviceSerial", s.DeviceSerial ?? "");
            cmd.Parameters.AddWithValue("@etimsUsername", s.EtimsUsername ?? "");
            cmd.Parameters.AddWithValue("@etimsPassword", s.EtimsPassword ?? "");

            cmd.Parameters.AddWithValue("@login", s.RequireLogin);
            cmd.Parameters.AddWithValue("@logout", s.AutoLogout);
            cmd.Parameters.AddWithValue("@void", s.AllowVoidSales);
            cmd.Parameters.AddWithValue("@workerEdit", s.AllowWorkersEditPrices);
            cmd.Parameters.AddWithValue("@workerDisc", s.AllowWorkersGiveDiscounts);

            cmd.Parameters.AddWithValue("@key", s.LicenseKey ?? "");
            cmd.Parameters.AddWithValue("@expiry", s.LicenseExpiry ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@activated", s.LicenseActivated);
            cmd.Parameters.AddWithValue("@installDate", s.LicenseInstallDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@expiryDate", s.LicenseExpiryDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@machineId", s.LicenseMachineId ?? "");

            cmd.Parameters.AddWithValue("@enableDisc", s.EnableDiscounts);
            cmd.Parameters.AddWithValue("@fixed", s.AllowFixedDiscount);
            cmd.Parameters.AddWithValue("@percent", s.AllowPercentageDiscount);
            cmd.Parameters.AddWithValue("@conditional", s.AllowConditionalDiscount);

            cmd.Parameters.AddWithValue("@maxFixed", s.MaxFixedDiscount);
            cmd.Parameters.AddWithValue("@maxPercent", s.MaxPercentageDiscount);

            cmd.Parameters.AddWithValue("@cvf", s.CondValueFixed);
            cmd.Parameters.AddWithValue("@cvp", s.CondValuePercent);
            cmd.Parameters.AddWithValue("@ranges", s.CondBasedRanges);

            cmd.Parameters.AddWithValue("@minSubtotal", s.ConditionalMinSubtotal);
            cmd.Parameters.AddWithValue("@condAmount", s.ConditionalDiscountAmount);

            cmd.ExecuteNonQuery();

            DiscountRangeRepository.SaveAll(s.DiscountRanges);
        }

        public static void UpdateField(string fieldName, object value)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE AppSettings SET {fieldName} = @val WHERE Id = 1";
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        private static void LogChangedSettings(AppSetting oldS, AppSetting newS)
        {
            if (oldS == null || newS == null)
                return;

            var props = typeof(AppSetting).GetProperties();

            foreach (var prop in props)
            {
                // Skip collections / complex types
                if (prop.PropertyType == typeof(List<DiscountRange>))
                    continue;

                var oldVal = prop.GetValue(oldS);
                var newVal = prop.GetValue(newS);

                string oldText = oldVal?.ToString() ?? "";
                string newText = newVal?.ToString() ?? "";

                if (oldText == newText)
                    continue;

                ActivityRepository.Log(new Activity
                {
                    EntityType = "Settings",
                    EntityId = 1,
                    EntityName = prop.Name,
                    Action = "UPDATE",
                    BeforeValue = oldText,
                    AfterValue = newText,
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                });
            }
        }
    }
}
