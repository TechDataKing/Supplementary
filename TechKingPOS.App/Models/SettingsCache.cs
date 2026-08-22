using TechKingPOS.App.Models;
using System.Linq;
using TechKingPOS.App.Data;

namespace TechKingPOS.App.Models
{
    public static class SettingsCache
    {
        public static AppSetting Current { get; private set; }
        public static long Version { get; private set; }

        public static void Load()
        {
            var db = SettingsRepository.Get() ?? new AppSetting();
            Current = Clone(db);
            Version++;
        }

        public static void ApplyChanges(AppSetting updated)
        {
            if (updated == null)
                return;

            Current = Clone(updated);
            Version++;
        }

        public static void Save()
        {
            if (Current != null)
                SettingsRepository.Save(Current);
        }

        // 🔑 DEEP CLONE
        private static AppSetting Clone(AppSetting s)
        {
            return new AppSetting
            {
                Id = s.Id,

                BusinessName = s.BusinessName,
                BranchName = s.BranchName,
                Phone = s.Phone,
                SecondaryPhone = s.SecondaryPhone,
                Email = s.Email,
                PhysicalAddress = s.PhysicalAddress,
                ReceiptFooter = s.ReceiptFooter,

                AutoPrintReceipt = s.AutoPrintReceipt,
                ShowCashierOnReceipt = s.ShowCashierOnReceipt,
                ShowLogoOnReceipt = s.ShowLogoOnReceipt,
                LogoImage = s.LogoImage,
                PaperSize = s.PaperSize,
                PrintLayout = s.PrintLayout,
                ReceiptCopies = s.ReceiptCopies,
                SignReceipt = s.SignReceipt,

                AllowNegativeStock = s.AllowNegativeStock,
                AllowPriceEditDuringSale = s.AllowPriceEditDuringSale,
                EnableCreditSales = s.EnableCreditSales,
                VatEnabled = s.VatEnabled,
                VatPercent = s.VatPercent,

                RequireLogin = s.RequireLogin,
                AutoLogout = s.AutoLogout,
                AllowVoidSales = s.AllowVoidSales,
                AllowWorkersEditPrices = s.AllowWorkersEditPrices,
                AllowWorkersGiveDiscounts = s.AllowWorkersGiveDiscounts,

                LicenseKey = s.LicenseKey,
                LicenseExpiry = s.LicenseExpiry,
                LicenseInstallDate = s.LicenseInstallDate,
                LicenseExpiryDate = s.LicenseExpiryDate,
                LicenseActivated = s.LicenseActivated,
                LicenseMachineId = s.LicenseMachineId,

                EnableDiscounts = s.EnableDiscounts,
                AllowFixedDiscount = s.AllowFixedDiscount,
                AllowPercentageDiscount = s.AllowPercentageDiscount,
                AllowConditionalDiscount = s.AllowConditionalDiscount,

                MaxFixedDiscount = s.MaxFixedDiscount,
                MaxPercentageDiscount = s.MaxPercentageDiscount,

                CondValueFixed = s.CondValueFixed,
                CondValuePercent = s.CondValuePercent,
                CondBasedRanges = s.CondBasedRanges,

                ConditionalMinSubtotal = s.ConditionalMinSubtotal,
                ConditionalDiscountAmount = s.ConditionalDiscountAmount,

                BranchId = s.BranchId,

                EnableEtims = s.EnableEtims,
                KraPin = s.KraPin,
                BhfId = s.BhfId,
                DeviceSerial = s.DeviceSerial,
                EtimsUsername = s.EtimsUsername,
                EtimsPassword = s.EtimsPassword,

                DiscountRanges = s.DiscountRanges?
                    .Select(r => new DiscountRange
                    {
                        From = r.From,
                        To = r.To,
                        DiscountAmount = r.DiscountAmount,
                        DiscountPercent = r.DiscountPercent
                    })
                    .ToList()
            };
        }
    }
}
