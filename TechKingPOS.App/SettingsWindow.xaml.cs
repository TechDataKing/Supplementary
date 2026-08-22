using System.Windows;
using TechKingPOS.App.Data;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;


using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Media;
using System;
using System.Linq;
using System.Net.Http;
using System.IO;
using System.Windows.Media.Imaging;

namespace TechKingPOS.App
{
    public partial class SettingsWindow : Window
    {
        private AppSetting _settings;
        private bool _hasUnsavedChanges = false;
        private bool _suppressChangeTracking = false;

        private AppSetting _originalSettings;

        private enum SettingsSection
        {
            Business,
            Receipt,
            Sales,
            Users,
            System
        }
        private enum DiscountMode
        {
            None,
            Fixed,
            Percentage,
            Conditional
        }

        private DiscountMode _discountMode = DiscountMode.None;


        private ObservableCollection<DiscountRange> _discountRanges =
             new ObservableCollection<DiscountRange>();


        private SettingsSection _currentSection;

        public SettingsWindow()
        {
            InitializeComponent();


            EnableDiscountCheck.Checked += (_, _) => UpdateDiscountUI();
            EnableDiscountCheck.Unchecked += (_, _) => UpdateDiscountUI();

            FixedDiscountCheck.Checked += (_, _) =>
            {
                ResetOtherDiscountTypes(FixedDiscountCheck);
                UpdateDiscountUI();
            };

            PercentageDiscountCheck.Checked += (_, _) =>
            {
                ResetOtherDiscountTypes(PercentageDiscountCheck);
                UpdateDiscountUI();
            };

            ConditionalDiscountCheck.Checked += (_, _) =>
            {
                ResetOtherDiscountTypes(ConditionalDiscountCheck);
                UpdateDiscountUI();
            };

            CondValueFixed.Checked += (_, _) => UpdateDiscountUI();
            CondValuePercent.Checked += (_, _) => UpdateDiscountUI();
            
            CondBasedRanges.Checked += (_, _) => UpdateDiscountUI();
            CondValueFixed.Checked += (_, _) =>
            {
                CondValuePercent.IsChecked = false;
            };

            CondValuePercent.Checked += (_, _) =>
            {
                CondValueFixed.IsChecked = false;
            };

           CondBasedRanges.IsChecked = true;
            CondBasedRanges.IsEnabled = false;


            RangesGrid.DataContext = _discountRanges;
            RangesGrid.CellEditEnding += RangesGrid_CellEditEnding;
            VatEnabledCheck.Checked += (_, _) =>
            {
                VatPercentBox.IsEnabled = true;
            };

            VatEnabledCheck.Unchecked += (_, _) =>
            {
                VatPercentBox.IsEnabled = false;
            };

            // Load data FIRST
                _suppressChangeTracking = true;

                LoadSettings();
                UpdateDiscountUI();
                ShowBusiness();

                _suppressChangeTracking = false;
                _hasUnsavedChanges = false;

            // 🔑 NOW start tracking user changes
            RegisterChangeTracking(this);

            // 🔑 ensure clean state
            _hasUnsavedChanges = false;

        }

        
        private void LoadSettings()
        {
           _settings = SettingsCache.Current;

            if (_settings == null)
            {
                _settings = new AppSetting();
                SettingsCache.ApplyChanges(_settings);
            }

            // First run → start trial
            if (_settings.LicenseInstallDate == null)
            {
                _settings.LicenseInstallDate = DateTime.UtcNow;
                _settings.LicenseExpiryDate = DateTime.UtcNow.AddDays(30);
                _settings.LicenseActivated = false;

                SettingsRepository.Save(_settings);
                SettingsCache.ApplyChanges(_settings);
            }

            var status = LicenseService.GetStatus(_settings);

            switch (status)
            {
                case LicenseStatus.Active:
                    LicenseStatusBox.Text = "Active";
                    LicenseStatusBox.Foreground = Brushes.Green;
                    break;

                case LicenseStatus.Trial:
                    LicenseStatusBox.Text = "Trial";
                    LicenseStatusBox.Foreground = Brushes.Orange;
                    break;

                case LicenseStatus.Expired:
                    LicenseStatusBox.Text = "Expired";
                    LicenseStatusBox.Foreground = Brushes.Red;
                    break;
            }

            LicenseExpiryBox.Text =
                _settings.LicenseExpiryDate?.ToString("yyyy-MM-dd") ?? "N/A";

            bool needsSave = false;

            if (_settings == null)
            {
                _settings = new AppSetting
                {
                    AutoPrintReceipt = true,
                    ShowCashierOnReceipt = true,

                    VatEnabled = false,
                    VatPercent = 16m
                };
                needsSave = true;
            }

                    // 🔑 migrate / enforce defaults
            if (_settings.VatPercent <= 0)
            {
                _settings.VatPercent = 16m;
                needsSave = true;
            }

            if (needsSave)
            {
                SettingsRepository.Save(_settings);
                SettingsCache.ApplyChanges(_settings);
            }


            // BUSINESS
            BusinessNameBox.Text   = _settings.BusinessName ?? "";
            BranchNameBox.Text    = _settings.BranchName ?? "";
            PhoneBox.Text         = _settings.Phone ?? "";
            SecondaryPhoneBox.Text         = _settings.SecondaryPhone ?? "";
            EmailBox.Text         = _settings.Email ?? "";
            AddressBox.Text       = _settings.PhysicalAddress ?? "";
            FooterBox.Text        = _settings.ReceiptFooter ?? "";

            // -----RECEIPT----------
            AutoPrintReceiptCheck.IsChecked = _settings.AutoPrintReceipt;
            ShowCashierCheck.IsChecked = _settings.ShowCashierOnReceipt;
            ShowLogoCheck.IsChecked = _settings.ShowLogoOnReceipt;
            SignReceipt.IsChecked = _settings.SignReceipt;
                // Paper size
            PaperSizeCombo.SelectedIndex = _settings.PaperSize == "80mm" ? 1 : 0;
            PrintLayoutCombo.SelectedIndex = _settings.PrintLayout == "Invoice" ? 1: 0;

                // Receipt copies
            ReceiptCopiesBox.Text = (_settings.ReceiptCopies <= 0 ? 1 : _settings.ReceiptCopies).ToString();

            // ================= SALES & PAYMENT =================
            AllowNegativeStockCheck.IsChecked = _settings.AllowNegativeStock;
            AllowPriceEditCheck.IsChecked = _settings.AllowPriceEditDuringSale;
            EnableCreditSalesCheck.IsChecked = _settings.EnableCreditSales;

            VatEnabledCheck.IsChecked = _settings.VatEnabled;
            VatPercentBox.Text = _settings.VatPercent.ToString("0.##");

            VatPercentBox.IsEnabled = _settings.VatEnabled;
        
            // DISCOUNTS
            EnableDiscountCheck.IsChecked = _settings.EnableDiscounts;

            FixedDiscountCheck.IsChecked = _settings.AllowFixedDiscount;
            PercentageDiscountCheck.IsChecked = _settings.AllowPercentageDiscount;
            ConditionalDiscountCheck.IsChecked = _settings.AllowConditionalDiscount;

            MaxFixedDiscountBox.Text = _settings.MaxFixedDiscount.ToString();
            MaxPercentageDiscountBox.Text = _settings.MaxPercentageDiscount.ToString();

            // CONDITIONAL MODE
            CondValueFixed.IsChecked = _settings.CondValueFixed;
            CondValuePercent.IsChecked = _settings.CondValuePercent;

            CondBasedRanges.IsChecked = _settings.CondBasedRanges;

            // LOAD RANGE TABLE
            _discountRanges.Clear();
            if (_settings.DiscountRanges != null)
            {
                foreach (var r in _settings.DiscountRanges.OrderBy(x => x.From))
                    _discountRanges.Add(r);
            }

            // ================= USERS & SECURITY =================
                RequireLoginCheck.IsChecked = _settings.RequireLogin;
                AutoLogoutCheck.IsChecked = _settings.AutoLogout;
                AllowVoidSalesCheck.IsChecked = _settings.AllowVoidSales;
                AllowEditPricesCheck.IsChecked = _settings.AllowWorkersEditPrices;
                AllowGiveDiscountsCheck.IsChecked = _settings.AllowWorkersGiveDiscounts;

            // ================= Kra Etims =====================
                EnableEtimsCheck.IsChecked = _settings.EnableEtims;
                KraPinBox.Text = _settings.KraPin;
                BhfIdBox.Text = _settings.BhfId;
                DeviceSerialBox.Text = _settings.DeviceSerial;
                EtimsUsernameBox.Text =_settings.EtimsUsername;
                EtimsPasswordBox.Password= _settings.EtimsPassword;

                
            _originalSettings = CloneSettings(_settings);

            // ================= Kra Etims =====================


            // =================SYSTEM SETTINGS =====================

        }

        private void SaveSettings()
        {
            //--------BUSINESS------------
            _settings.BusinessName = BusinessNameBox.Text;
            _settings.BranchName = BranchNameBox.Text;
            _settings.Phone = PhoneBox.Text;
            _settings.SecondaryPhone = SecondaryPhoneBox.Text;
            _settings.Email = EmailBox.Text;
            _settings.PhysicalAddress = AddressBox.Text;
            _settings.ReceiptFooter = FooterBox.Text;

                // ===== RECEIPT =====
            _settings.AutoPrintReceipt = AutoPrintReceiptCheck.IsChecked == true;
            _settings.ShowCashierOnReceipt = ShowCashierCheck.IsChecked == true;
            _settings.ShowLogoOnReceipt = ShowLogoCheck.IsChecked == true;
            _settings.SignReceipt = SignReceipt.IsChecked == true;

            if (PaperSizeCombo.SelectedItem is ComboBoxItem item)
                _settings.PaperSize = item.Content.ToString();
            else
                _settings.PaperSize = "58mm";

            if (!int.TryParse(ReceiptCopiesBox.Text, out int copies) || copies < 1)
                copies = 1;

            _settings.ReceiptCopies = copies;

            if (PrintLayoutCombo.SelectedItem is ComboBoxItem layoutItem)
            {
                _settings.PrintLayout = layoutItem.Content.ToString();
            }
            else
            {
                _settings.PrintLayout = "Thermal Receipt";
            }


                //=====SALES  AND PAYMENT  =====
            _settings.AllowNegativeStock = AllowNegativeStockCheck.IsChecked == true;
            _settings.AllowPriceEditDuringSale = AllowPriceEditCheck.IsChecked == true;
            _settings.EnableCreditSales = EnableCreditSalesCheck.IsChecked == true;

            _settings.VatEnabled = VatEnabledCheck.IsChecked == true;

                _settings.VatEnabled = VatEnabledCheck.IsChecked == true;

                try
                {
                    _settings.VatPercent = VatHelper.ParseVatPercent(VatPercentBox.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid VAT", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

            _settings.EnableDiscounts = EnableDiscountCheck.IsChecked == true;

            _settings.AllowFixedDiscount = FixedDiscountCheck.IsChecked == true;
            _settings.AllowPercentageDiscount = PercentageDiscountCheck.IsChecked == true;
            _settings.AllowConditionalDiscount = ConditionalDiscountCheck.IsChecked == true;

            decimal.TryParse(MaxFixedDiscountBox.Text, out var maxFixed);
            decimal.TryParse(MaxPercentageDiscountBox.Text, out var maxPercent);

            _settings.MaxFixedDiscount = maxFixed;
            _settings.MaxPercentageDiscount = maxPercent;


            // CONDITIONAL FLAGS
            _settings.CondValueFixed = CondValueFixed.IsChecked == true;
            _settings.CondValuePercent = CondValuePercent.IsChecked == true;

            _settings.CondBasedRanges = CondBasedRanges.IsChecked == true;

            // SAVE RANGE LIST
            _settings.DiscountRanges = _discountRanges
                .OrderBy(r => r.From)
                .ToList();

            
            if (!AreRangesValid(out var msg))
                {
                    MessageBox.Show(msg, "Invalid Ranges", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                //======== USER AND SECURITY ======
                _settings.RequireLogin = RequireLoginCheck.IsChecked == true;
                _settings.AutoLogout = AutoLogoutCheck.IsChecked == true;
                _settings.AllowVoidSales = AllowVoidSalesCheck.IsChecked == true;
                _settings.AllowWorkersEditPrices = AllowEditPricesCheck.IsChecked == true;
                _settings.AllowWorkersGiveDiscounts = AllowGiveDiscountsCheck.IsChecked == true;

                //=========  Etims Settings ========

                _settings.EnableEtims = EnableEtimsCheck.IsChecked == true;

                _settings.KraPin = KraPinBox.Text;
                _settings.BhfId = BhfIdBox.Text;
                _settings.DeviceSerial = DeviceSerialBox.Text;
                _settings.EtimsUsername = EtimsUsernameBox.Text;
                _settings.EtimsPassword = EtimsPasswordBox.Password;



            SettingsRepository.Save(_settings);
            SettingsCache.ApplyChanges(_settings);

            //LogSettingsChanges(_originalSettings, _settings);
            _originalSettings = CloneSettings(_settings);

            MessageBox.Show("Settings saved successfully");
            _hasUnsavedChanges = false;
        }

        // ✅ ONLY ADDED METHOD (SAVE BUTTON HANDLER)
        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        // ================= NAVIGATION =================

        private void HideAll()
        {
            BusinessPanel.Visibility = Visibility.Collapsed;
            ReceiptPanel.Visibility = Visibility.Collapsed;
            SalesPanel.Visibility = Visibility.Collapsed;
            UsersPanel.Visibility = Visibility.Collapsed;
            SystemPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowBusiness()
        {
            HideAll();
            BusinessPanel.Visibility = Visibility.Visible;
        }

        private void Business_Click(object sender, RoutedEventArgs e)
{
    ConfirmNavigation(() =>
    {
        SetActiveButton(BusinessButton);
        HideAll();
        BusinessPanel.Visibility = Visibility.Visible;
    });
}

private void Receipt_Click(object sender, RoutedEventArgs e)
{
    ConfirmNavigation(() =>
    {
        SetActiveButton(ReceiptButton);
        HideAll();
        ReceiptPanel.Visibility = Visibility.Visible;
    });
}

private void Sales_Click(object sender, RoutedEventArgs e)
{
    ConfirmNavigation(() =>
    {
        SetActiveButton(SalesButton);
        HideAll();
        SalesPanel.Visibility = Visibility.Visible;
    });
}

private void Users_Click(object sender, RoutedEventArgs e)
{
    ConfirmNavigation(() =>
    {
        SetActiveButton(UsersButton);
        HideAll();
        UsersPanel.Visibility = Visibility.Visible;
    });
}

private void System_Click(object sender, RoutedEventArgs e)
{
    ConfirmNavigation(() =>
    {
        SetActiveButton(SystemButton);
        HideAll();
        SystemPanel.Visibility = Visibility.Visible;
    });
}

        private void UpdateDiscountUI()
        {
            _suppressChangeTracking = true;

            bool enabled = EnableDiscountCheck.IsChecked == true;

            FixedDiscountCheck.IsEnabled = enabled;
            PercentageDiscountCheck.IsEnabled = enabled;
            ConditionalDiscountCheck.IsEnabled = enabled;

            MaxFixedDiscountBox.Visibility = Visibility.Collapsed;
            MaxPercentageDiscountBox.Visibility = Visibility.Collapsed;

            // Default hidden
            CondValueFixed.Visibility = Visibility.Collapsed;
            CondValuePercent.Visibility = Visibility.Collapsed;
            CondBasedRanges.Visibility = Visibility.Collapsed;
            RangeSalesPanel.Visibility = Visibility.Collapsed;

            if (!enabled)
            {
                _suppressChangeTracking = false;
                return;
            }

            // ---------------- FIXED DISCOUNT MODE ----------------
            if (FixedDiscountCheck.IsChecked == true)
            {
                MaxFixedDiscountBox.Visibility = Visibility.Visible;
                _suppressChangeTracking = false;
                return;
            }

    // ---------------- PERCENTAGE DISCOUNT MODE ----------------
    if (PercentageDiscountCheck.IsChecked == true)
    {
        MaxPercentageDiscountBox.Visibility = Visibility.Visible;
        _suppressChangeTracking = false;
        return;
    }

    // ---------------- CONDITIONAL RANGE DISCOUNT ----------------
    if (ConditionalDiscountCheck.IsChecked == true)
    {
        // ALWAYS SHOW THESE
        CondValueFixed.Visibility = Visibility.Visible;
        CondValuePercent.Visibility = Visibility.Visible;

        CondBasedRanges.Visibility = Visibility.Visible;   // always checked anyway
        RangeSalesPanel.Visibility = Visibility.Visible;

        // ---- switch table column based on value type ----
        if (CondValueFixed.IsChecked == true)
        {
            DiscountColumn.Header = "Discount (KES)";
            DiscountColumn.Binding = new Binding("DiscountAmount");
        }
        else
        {
            DiscountColumn.Header = "Discount (%)";
            DiscountColumn.Binding = new Binding("DiscountPercent");
        }
    }

    _suppressChangeTracking = false;
}



        private void ResetOtherDiscountTypes(CheckBox active)
        {
            _suppressChangeTracking = true;

            if (active != FixedDiscountCheck) FixedDiscountCheck.IsChecked = false;
            if (active != PercentageDiscountCheck) PercentageDiscountCheck.IsChecked = false;
            if (active != ConditionalDiscountCheck) ConditionalDiscountCheck.IsChecked = false;

            _suppressChangeTracking = false;
        }

private void RegisterChangeTracking(DependencyObject parent)
{
    foreach (var child in LogicalTreeHelper.GetChildren(parent))
    {
        if (child is TextBox tb)
        {
            tb.TextChanged += (_, _) =>
            {
                if (_suppressChangeTracking) return;
                _hasUnsavedChanges = true;
            };
        }
        else if (child is CheckBox cb)
        {
            cb.Checked += (_, _) =>
            {
                if (_suppressChangeTracking) return;
                _hasUnsavedChanges = true;
            };

            cb.Unchecked += (_, _) =>
            {
                if (_suppressChangeTracking) return;
                _hasUnsavedChanges = true;
            };
        }
        else if (child is RadioButton rb)
        {
            rb.Checked += (_, _) =>
            {
                if (_suppressChangeTracking) return;
                _hasUnsavedChanges = true;
            };
        }
        else if (child is DependencyObject dep)
        {
            RegisterChangeTracking(dep);
        }
    }
}


private void ConfirmNavigation(Action navigate)
{
    if (!_hasUnsavedChanges)
    {
        navigate();
        return;
    }

    var result = MessageBox.Show(
        "You have unsaved changes.\n\nDo you want to save before leaving?",
        "Unsaved Changes",
        MessageBoxButton.YesNoCancel,
        MessageBoxImage.Warning
    );

    if (result == MessageBoxResult.Yes)
    {
        SaveSettings();
        navigate();
    }
    else if (result == MessageBoxResult.No)
    {
        _hasUnsavedChanges = false;
        navigate();
    }
    // Cancel → do nothing
}
        private void AddRange_Click(object sender, RoutedEventArgs e)
        {
            if (_discountRanges.Count == 0)
            {
                _discountRanges.Add(new DiscountRange { From = 0, To = 0 });
            }
            else
            {
                var last = _discountRanges.OrderBy(r => r.From).Last();
                _discountRanges.Add(new DiscountRange
                {
                    From = last.To + 1,
                    To = last.To + 1
                });
            }
        }


        private void RemoveRange_Click(object sender, RoutedEventArgs e)
        {
            if (RangesGrid.SelectedItem is DiscountRange selected)
            {
                _discountRanges.Remove(selected);
            }
            else
            {
                MessageBox.Show("Please select a row to remove.");
            }
        }
        private bool AreRangesValid(out string error)
        {
            error = string.Empty;

            if (_discountRanges.Count == 0)
                return true;

            var sorted = _discountRanges
                .OrderBy(r => r.From)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var r = sorted[i];

                // From must be <= To
                if (r.From > r.To)
                {
                    error = $"Range #{i + 1}: 'From' cannot be greater than 'To'.";
                    return false;
                }

                if (i > 0)
                {
                    var prev = sorted[i - 1];

                    // Must continue exactly from previous
                    var expectedFrom = prev.To + 1;

                    if (r.From != expectedFrom)
                    {
                        error = $"Range #{i + 1}: must start at {expectedFrom}.";
                        return false;
                    }
                }
            }

            return true;
        }

private void RangesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
{
    Dispatcher.BeginInvoke(new Action(() =>
    {
        if (!AreRangesValid(out var msg))
        {
            MessageBox.Show(msg, "Invalid Ranges", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }));
}

        public static class VatHelper
        {
            public static decimal ParseVatPercent(string input)
            {
                if (string.IsNullOrWhiteSpace(input))
                    return 16m; // default

                input = input.Trim().Replace("%", "");

                if (!decimal.TryParse(input, out var value))
                    throw new Exception("Invalid VAT value");

                if (value < 0 || value > 100)
                    throw new Exception("VAT must be between 0 and 100");

                return value;
            }
        }



        private AppSetting CloneSettings(AppSetting s)
        {
            return new AppSetting
            {
                AutoPrintReceipt = s.AutoPrintReceipt,
                ShowCashierOnReceipt = s.ShowCashierOnReceipt,
                VatEnabled = s.VatEnabled,
                VatPercent = s.VatPercent,
                AllowVoidSales = s.AllowVoidSales,
                AllowNegativeStock = s.AllowNegativeStock,
                AllowPriceEditDuringSale = s.AllowPriceEditDuringSale,
                EnableCreditSales = s.EnableCreditSales,
                EnableDiscounts = s.EnableDiscounts
                // add others if needed
            };
        }
private void SetActiveButton(Button active)
{
    // Reset all buttons
    BusinessButton.Tag = false;
    ReceiptButton.Tag = false;
    SalesButton.Tag = false;
    UsersButton.Tag = false;
    SystemButton.Tag = false;

    // Set the clicked button as active
    active.Tag = true;
}


private async void ActivateLicense_Click(object sender, RoutedEventArgs e)
{
    var key = LicenseKeyBox.Text.Trim().ToUpper();

    bool success;

    try
    {
        // Attempt to activate the license via your service
        success = await LicenseService.ActivateAsync(key, _settings);
    }
    catch (HttpRequestException ex)
    {
        MessageBox.Show(
            $"Failed to connect to license server: {ex.Message}",
            "Network Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        return;
    }

    if (!success)
    {
        MessageBox.Show(
            "Invalid or already used license key",
            "Activation Failed",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
        return;
    }

    // Update license properties in the settings
    _settings.LicenseKey = key;
    _settings.LicenseActivated = true;

    // If the activation returns a specific expiry date, use it; otherwise keep current or default
    if (_settings.LicenseExpiryDate == null)
        _settings.LicenseExpiryDate = DateTime.UtcNow.AddDays(30);

    // Persist to database and update cache
    SettingsRepository.Save(_settings);
    SettingsCache.ApplyChanges(_settings);

    // Update UI
    LicenseStatusBox.Text = "Active";
    LicenseStatusBox.Foreground = Brushes.Green;

    LicenseExpiryBox.Text = _settings.LicenseExpiryDate?.ToString("yyyy-MM-dd") ?? "N/A";

    MessageBox.Show(
        "License activated successfully",
        "Success",
        MessageBoxButton.OK,
        MessageBoxImage.Information
    );

    LicenseKeyBox.Clear();
}


private void LicenseKeyBox_GotFocus(object sender, RoutedEventArgs e)
{
    if (LicenseKeyBox.Text == "Enter license key")
    {
        LicenseKeyBox.Text = "";
        LicenseKeyBox.Foreground = Brushes.Black;
    }
}

private void LicenseKeyBox_LostFocus(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(LicenseKeyBox.Text))
    {
        LicenseKeyBox.Text = "Enter license key";
        LicenseKeyBox.Foreground = Brushes.Gray;
    }
}
private void UploadLogo_Click(object sender, RoutedEventArgs e)
{
    var dialog = new Microsoft.Win32.OpenFileDialog
    {
        Filter = "Image Files|*.png;*.jpg;*.jpeg"
    };

    if (dialog.ShowDialog() == true)
    {
        byte[] bytes = File.ReadAllBytes(dialog.FileName);
        _settings.LogoImage = bytes;

        // preview
        using var ms = new MemoryStream(bytes);
        var img = new BitmapImage();
        img.BeginInit();
        img.StreamSource = ms;
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();

        LogoPreview.Source = img;
    }
}
private void RemoveLogo_Click(object sender, RoutedEventArgs e)
{
    _settings.LogoImage = null;
    LogoPreview.Source = null;
}


            }
}
