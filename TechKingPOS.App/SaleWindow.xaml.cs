using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using TechKingPOS.App.Data;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;


namespace TechKingPOS.App
{
    public partial class SalesWindow : RefreshOnFocusWindow, ISupportBranchRefresh

    {    

        // ================= DISCOUNT STATE =================
        private bool _isLoaded;
        // Is any discount applied?
        private bool _hasDiscount;

        private bool _isWholesale = false;
        
        // Final calculated discount amount (KES)
        private decimal _discountAmount;

        // Discount type: "Fixed", "Percentage", "Conditional"
        private string? _discountType;

        // Raw value entered (e.g. 500 or 10)
        private decimal _discountValue;

            private long _lastItemVersion = -1;
            private long _lastSettingsVersion = -1;


        // Cart (right side)
        private ObservableCollection<SaleItem> CartItems = new();

        // All items from DB (left side)
        private List<ItemLookup> _allItems = new();
        private List<Button> _customerButtons;
        private Button _activeCustomer;
        // Holds a cart per customer button
        private Dictionary<Button, ObservableCollection<SaleItem>> _customerCarts = new();
        private Dictionary<string, int> _lastUsedBranchForWindows = new Dictionary<string, int>();
        private DispatcherTimer _itemsTimer = new DispatcherTimer();

        private string? _quickPaymentMethod; // "Cash", "Mpesa", or null

        private AppSetting _settings;
        private bool _isLookupOpen = false;
        private readonly DispatcherTimer _barcodeTimer = new DispatcherTimer();
        private bool _addingBarcode = false;

        private enum DiscountMode
        {
            None,
            Fixed,
            Percentage,
            Conditional
        }
        private decimal VatRate =>
    (_settings?.VatEnabled == true && _settings.VatPercent > 0)
        ? _settings.VatPercent / 100m
        : 0m;


private int _tickCount = 0;

private void AutoRefreshSaleItems()
{
    _tickCount++;

    if (_lastItemVersion != ItemRepository.Version || _tickCount % 5 == 0)
    {
        _lastItemVersion = ItemRepository.Version;

        LoadItems();
    }
}

    private DiscountMode _discountMode = DiscountMode.None;        // overall mode
    private DiscountMode _conditionalType = DiscountMode.None; 
        public SalesWindow()
        {
            InitializeComponent();
            
            ApplyReceiptSettings();
            LoadDiscountSettings();
            AutoRefreshSaleItems();

            _barcodeTimer.Interval = TimeSpan.FromMilliseconds(150);
            _barcodeTimer.Tick += BarcodeTimer_Tick;

            _itemsTimer.Interval = TimeSpan.FromSeconds(2); // faster than 5s for POS
            _itemsTimer.Tick += (s, e) => AutoRefreshItems();
            _itemsTimer.Start();
             Loaded += PendingWindow_Loaded;

            _customerButtons = new List<Button>
     {
                    Customer1Btn,
                    Customer2Btn,
                    Customer3Btn,
                    Customer4Btn
                    // Customer5Btn,
                    // Customer6Btn
                };
                 _customerCarts[Customer1Btn] = new ObservableCollection<SaleItem>();

                SetActiveCustomer(Customer1Btn);


            CartGrid.ItemsSource = CartItems;

            try
            {
                LoadItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "SalesWindow crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            SearchBox.TextChanged += SearchBox_TextChanged;

            
        }
        private void AutoRefreshItems()
{
    // Only refresh if items changed
    if (_lastItemVersion != ItemRepository.Version)
    {
        _lastItemVersion = ItemRepository.Version;

        var selected = ItemsList.SelectedItem;

        LoadItems();

        // restore selection (important for UX)
        if (selected != null)
        {
            var old = selected.GetType().GetProperty("Item")?.GetValue(selected) as ItemLookup;

            if (old != null)
            {
                ItemsList.SelectedItem =
                    ItemsList.Items.Cast<object>()
                    .FirstOrDefault(x =>
                    {
                        var item = x.GetType().GetProperty("Item")?.GetValue(x) as ItemLookup;
                        return item?.Id == old.Id;
                    });
            }
        }
    }
}
protected override void OnClosed(EventArgs e)
{
    _itemsTimer.Stop();
    base.OnClosed(e);
}

// Get the last used branch for this specific window
public int GetCurrentBranch()
{
    string windowKey = this.GetType().Name; // Get the window's name as key
    if (_lastUsedBranchForWindows.ContainsKey(windowKey))
    {
        return _lastUsedBranchForWindows[windowKey];
    }
    return SessionContext.EffectiveBranchId; // Default to the effective branch if not found
}

// Load sales data for the specified branch and store it in the dictionary
public void LoadSalesData(int branchId)
{
    string windowKey = this.GetType().Name; // Get the window's name as key
    _lastUsedBranchForWindows[windowKey] = branchId; // Store the branch ID for this window

    // Reload data for this branch
    SessionContext.CurrentBranchId = branchId; // Set current branch
    LoadItems(); // Reload items for the branch
    UpdateTotals(); // Recalculate totals
    ApplyReceiptSettings();
    LoadDiscountSettings();
}

public void RefreshByBranch(int branchId)
{
    // Logic to refresh the window's data based on the new branch ID
    if (branchId != SessionContext.EffectiveBranchId)
    {
        // Update the branch ID in session context
        SessionContext.CurrentBranchId = branchId;

        // Reload the data for this branch (e.g., items, sales records)
        LoadSalesData(branchId); // Use the method to load data for the new branch
    }
}


protected override void Refresh()
{
    if (!_isLoaded)
        return;

    // 🔁 SETTINGS
    if (_lastSettingsVersion != SettingsCache.Version)
    {
        _lastSettingsVersion = SettingsCache.Version;
        LoadDiscountSettings();
        ApplyReceiptSettings();
        UpdateTotals(); // 🔥 cart preserved
    }

    // 🔁 ITEMS
    if (_lastItemVersion != ItemRepository.Version)
    {
        _lastItemVersion = ItemRepository.Version;

        var selected = ItemsList.SelectedItem;

        LoadItems();

        // optional: restore selection
        if (selected is ItemLookup old)
        {
            ItemsList.SelectedItem =
                _allItems.FirstOrDefault(i => i.Id == old.Id);
        }
    }
}

        private void LoadDiscountSettings()
{
    // Load settings including discount ranges from DB
    _settings = SettingsCache.Current;
    if (_settings == null)
    {
        _settings = new AppSetting();
        //MessageBox.Show("No settings found. Using default.");
    }

    // Ensure ranges are loaded
    var ranges = _settings.DiscountRanges ?? new List<DiscountRange>();

    //Determine main discount mode
    if (_settings.EnableDiscounts)
    {
        if (_settings.AllowFixedDiscount)
            _discountMode = DiscountMode.Fixed;
        else if (_settings.AllowPercentageDiscount)
            _discountMode = DiscountMode.Percentage;
        else if (_settings.AllowConditionalDiscount)
        {
            _discountMode = DiscountMode.Conditional;

            // Determine conditional type from ranges
            if (ranges.Any(r => r.DiscountAmount.HasValue))
                _conditionalType = DiscountMode.Fixed;      // conditional fixed KES
            else if (ranges.Any(r => r.DiscountPercent.HasValue))
                _conditionalType = DiscountMode.Percentage; // conditional %
            else
            {
                _conditionalType = DiscountMode.None;
                MessageBox.Show("Conditional discount enabled but no valid ranges in DB!");
            }
        }
        else
            _discountMode = DiscountMode.None;
    }
    else
    {
        _discountMode = DiscountMode.None;
    }

    // Debug: show what was loaded
//     MessageBox.Show($@"
// EnableDiscounts: {_settings.EnableDiscounts}
// AllowFixedDiscount: {_settings.AllowFixedDiscount}
// AllowPercentageDiscount: {_settings.AllowPercentageDiscount}
// AllowConditionalDiscount: {_settings.AllowConditionalDiscount}
// DiscountRanges Count: {ranges.Count}
// DiscountMode: {_discountMode}
// Conditional Type: {_conditionalType}
// Ranges Details:
// {string.Join("\n", ranges.Select(r => $"From: {r.From}, To: {r.To}, Percent: {r.DiscountPercent}, Amount: {r.DiscountAmount}"))}
// ");
 }


        // ================= LOAD ITEMS =================
private void LoadItems()
{
    _allItems = ItemRepository.GetAllItems();
    var repacks = RepackStockRepository.GetActive();

    // ---------------- NORMAL ITEMS ----------------
    var normalItems = _allItems
        .Select(i => new
        {
            Item = i, // 🔥 KEEP ORIGINAL OBJECT
            Display = i.GetDisplay(_isWholesale),
            IsRepack = false,
            Repack = (RepackStockModel?)null
        });

    // ---------------- REPACK ITEMS ----------------
    var repackItems = repacks
        .Where(r => r.RemainingQuantity > 0) // ✅ avoid dead stock
        .Select(r => new
        {
            Item = new ItemLookup   // 🔥 FAKE ITEM (important trick)
            {
                Id = r.ItemId,
                Name = r.ItemName + " (Repack)",
                UnitType = r.BaseUnit,
                Quantity = r.RemainingQuantity,
                SellingPrice = 0,   // fallback price
                MarkedPrice = 0
            },
            Display = $"{r.ItemName} (Repack) → {r.RemainingQuantity} {r.BaseUnit}",
            IsRepack = true,
            Repack = (RepackStockModel?)r
        });

    // ---------------- COMBINE ----------------
    var displayItems = normalItems
        .Concat(repackItems)
        .ToList();

    ItemsList.ItemsSource = displayItems;
    ItemsList.DisplayMemberPath = "Display";

    LoggerService.Info(
        "📦",
        "SALES",
        "Items + Repack loaded into sales list",
        $"Normal={_allItems.Count}, Repack={repacks.Count}"
    );
}

        // ================= SEARCH =================
private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    string text = SearchBox.Text.Trim().ToLower();

    if (string.IsNullOrEmpty(text))
    {
        LoadItems(); // ✅ restore full list (normal + repack)
        return;
    }

    var currentList = ItemsList.Items.Cast<object>();

    var filtered = currentList
        .Where(x =>
        {
            var item = x.GetType().GetProperty("Item")?.GetValue(x) as ItemLookup;

            if (item == null) return false;

            return
                item.Name.ToLower().Contains(text) ||
                (item.Alias != null && item.Alias.ToLower().Contains(text));
        })
        .ToList();

    ItemsList.ItemsSource = filtered;
}
        private Window GetMainWindow()
        {
            return Application.Current.MainWindow;
        }



        // ================= ADD TO CART =================
private void ItemsList_DoubleClick(object sender, MouseButtonEventArgs e)
{
    var selected = ItemsList.SelectedItem;
    if (selected == null)
        return;

    // 🔥 GET ORIGINAL OBJECTS (no breaking change)
    var selectedObj = selected;

    ItemLookup dbItem = (ItemLookup)selectedObj
        .GetType().GetProperty("Item")
        .GetValue(selectedObj);

    bool isRepack = (bool)selectedObj
        .GetType().GetProperty("IsRepack")
        .GetValue(selectedObj);

    var repack = selectedObj
        .GetType().GetProperty("Repack")
        .GetValue(selectedObj) as RepackStockModel;

    // ---------------- PRICE (ONLY FOR FULL ITEM) ----------------
    decimal selectedPrice = dbItem.SellingPrice;

    if (_isWholesale && dbItem.WholesalePrice.HasValue && dbItem.WholesalePrice > 0)
    {
        selectedPrice = dbItem.WholesalePrice.Value;
    }

    // ---------------- CREATE SALE ITEM ----------------
    var saleItem = new SaleItem
    {
        ItemId = dbItem.Id,
        Name = dbItem.Name,
        UnitType = dbItem.UnitType,
        Quantity = 1,

        // 🔥 IMPORTANT: price for repack comes later from rule
        Price = isRepack ? 0 : selectedPrice,
        MarkedPrice = dbItem.MarkedPrice,
        AvailableQuantity = dbItem.Quantity,
        

        BranchId = SessionContext.EffectiveBranchId,
        SaleType = _isWholesale ? "Wholesale" : "Retail",

        IsRepack = isRepack,
        RepackId = repack?.Id,
        RepackAvailableQty = repack?.RemainingQuantity ?? 0
    };

    // ---------------- LOAD RULES ----------------
List<RepackRuleModel> rules;

if (isRepack)
{
    // ✅ ONLY when clicking a repack item
    rules = RepackRepository
        .GetRulesForItem(dbItem.Id)
        .Where(r => r.IsActive)
        .ToList();
}
else
{
    // ❌ FULL item → NO repack UI
    rules = new List<RepackRuleModel>();
}

    // ---------------- INITIAL STOCK CHECK ----------------
    if (!SettingsCache.Current.AllowNegativeStock)
    {
        if (dbItem.Quantity <= 0)
        {
            MessageBox.Show(
                $"Out of stock: {dbItem.Name}",
                "Stock Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }
    }

    // ---------------- OPEN SELECT WINDOW ----------------
    var dialog = new SelectItemWindow(saleItem, rules)
    {
        Owner = GetMainWindow()
    };

    if (dialog.ShowDialog() == true)
    {
        // 🔥 PRICE BYPASS SUPPORT
        if (
            saleItem.Price < saleItem.MarkedPrice)
        {
            MessageBox.Show(
                $"Cannot sell below unit price.\n\n" +
                $"Unit Cost: {saleItem.MarkedPrice:0.00}\n" +
                $"Selling Price: {saleItem.Price:0.00}",
                "Price Protection",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        // ---------------- STOCK CHECK AFTER INPUT ----------------
        if (!SettingsCache.Current.AllowNegativeStock)
    {
        if (isRepack)
        {
            if (saleItem.RepackAvailableQty <= 0)
            {
                MessageBox.Show(
                    $"Out of repack stock: {dbItem.Name}",
                    "Stock Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
        }
        else
        {
            if (dbItem.Quantity <= 0)
            {
                MessageBox.Show(
                    $"Out of stock: {dbItem.Name}",
                    "Stock Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
        }
    }

    // ---- COMPUTE CURRENT CART QTY OF THIS ITEM ----
var existingQtyInCart = CartItems
    .Where(ci => ci.ItemId == saleItem.ItemId && !ci.IsRepack)
    .Sum(ci => ci.Quantity);

// ---- REQUIRED TOTAL ----
var totalQtyAfterAdd = existingQtyInCart + saleItem.Quantity;

// ---- VALIDATION ----
if (!SettingsCache.Current.AllowNegativeStock)
{
    if (!isRepack)
    {
        if (totalQtyAfterAdd > dbItem.Quantity)
        {
            MessageBox.Show(
                $"Insufficient stock.\n\n" +
                $"Available: {dbItem.Quantity}\n" +
                $"In Cart: {existingQtyInCart}\n" +
                $"You Tried: {saleItem.Quantity}",
                "Stock Limit",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }
    }
}
if (isRepack)
{
    var repackInCart = CartItems
        .Where(ci => ci.RepackId == saleItem.RepackId && ci.IsRepack)
        .Sum(ci => ci.Quantity);

    var totalRepackAfterAdd = repackInCart + saleItem.Quantity;

    if (totalRepackAfterAdd > saleItem.RepackAvailableQty)
    {
        MessageBox.Show(
            $"Insufficient repack stock.\n\n" +
            $"Available: {saleItem.RepackAvailableQty}\n" +
            $"In Cart: {repackInCart}\n" +
            $"You Tried: {saleItem.Quantity}",
            "Stock Limit",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
        return;
    }
}

        CartItems.Add(saleItem);
        UpdateTotals();
    }


            ActivityRepository.Log(new Activity
        {
            EntityType = "Item",
            EntityId = saleItem.ItemId,
            EntityName = saleItem.Name,
            Action = "ADD_TO_CART",
            QuantityChange = saleItem.Quantity,
            UnitType = saleItem.UnitType,
            UnitValue = saleItem.Price,
            AfterValue = saleItem.Total.ToString("0.00"),
            PerformedBy = SessionContext.CurrentUserName,
            CreatedAt = DateTime.Now
        });

        }

        // ================= CART HELPERS =================
        private SaleItem? SelectedCartItem =>
            CartGrid.SelectedItem as SaleItem;

        private void ChangeQty_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCartItem is null)
            {
                MessageBox.Show("Select an item in the cart first.");
                return;
            }
            var beforeQty = SelectedCartItem.Quantity;
            var beforePrice = SelectedCartItem.Price;
            ActivityRepository.Log(new Activity
            {
                EntityType = "Item",
                EntityId = SelectedCartItem.ItemId,
                EntityName = SelectedCartItem.Name,
                Action = "UPDATE_CART_ITEM",
                QuantityChange = SelectedCartItem.Quantity - beforeQty,
                UnitType = SelectedCartItem.UnitType,
                UnitValue = SelectedCartItem.Price,
                BeforeValue = $"{beforeQty} @ {beforePrice}",
                AfterValue = $"{SelectedCartItem.Quantity} @ {SelectedCartItem.Price}",
                PerformedBy = SessionContext.CurrentUserName,
                CreatedAt = DateTime.Now
            });

            var tempItem = new SaleItem
            {
                ItemId = SelectedCartItem.ItemId,
                Name = SelectedCartItem.Name,
                Quantity = SelectedCartItem.Quantity,
                Price = SelectedCartItem.Price,
                MarkedPrice = SelectedCartItem.MarkedPrice,
                UnitType = SelectedCartItem.UnitType,
                UnitValue = SelectedCartItem.UnitValue,
                AvailableQuantity = SelectedCartItem.AvailableQuantity,
                BranchId = SelectedCartItem.BranchId,
                SaleType = SelectedCartItem.SaleType
            };

            var dialog = new SelectItemWindow(tempItem)
            {
                Owner = GetMainWindow(),
                IsEditMode = true
            };

            if (dialog.ShowDialog() == true)
            {
                var dbItem = _allItems.FirstOrDefault(i => i.Id == SelectedCartItem.ItemId);

                if (dbItem == null)
                    return;

                if (!ValidateSaleItem(tempItem, dbItem, true))
                    return;

                SelectedCartItem.Quantity = tempItem.Quantity;
                SelectedCartItem.Price = tempItem.Price;

                UpdateTotals();
                CartGrid.Items.Refresh();
            }
        }

        private void ChangePrice_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCartItem is null)
            {
                MessageBox.Show("Select an item in the cart first.");
                return;
            }

            var tempItem = new SaleItem
            {
                ItemId = SelectedCartItem.ItemId,
                Name = SelectedCartItem.Name,
                Quantity = SelectedCartItem.Quantity,
                Price = SelectedCartItem.Price,
                MarkedPrice = SelectedCartItem.MarkedPrice,
                UnitType = SelectedCartItem.UnitType,
                UnitValue = SelectedCartItem.UnitValue,
                BranchId = SelectedCartItem.BranchId,
                SaleType = SelectedCartItem.SaleType,
                AvailableQuantity = SelectedCartItem.AvailableQuantity
            };

            var dialog = new SelectItemWindow(tempItem)
            {
                Owner = GetMainWindow(),
                IsEditMode = true
            };

            if (dialog.ShowDialog() == true)
            {
                var dbItem = _allItems.FirstOrDefault(i => i.Id == SelectedCartItem.ItemId);

                if (dbItem == null)
                    return;

                if (!ValidateSaleItem(tempItem, dbItem, true))
                    return;
                
                SelectedCartItem.Quantity = tempItem.Quantity;
                SelectedCartItem.Price = tempItem.Price;

                UpdateTotals();
                CartGrid.Items.Refresh();
            }        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCartItem is null)
            {
                MessageBox.Show("Select an item to delete.");
                return;
            }
            ActivityRepository.Log(new Activity
            {
                EntityType = "Item",
                EntityId = SelectedCartItem.ItemId,
                EntityName = SelectedCartItem.Name,
                Action = "REMOVE_FROM_CART",
                QuantityChange = -SelectedCartItem.Quantity,
                UnitType = SelectedCartItem.UnitType,
                UnitValue = SelectedCartItem.Price,
                BeforeValue = SelectedCartItem.Total.ToString("0.00"),
                PerformedBy = SessionContext.CurrentUserName,
                CreatedAt = DateTime.Now
            });


            if (MessageBox.Show(
                "Remove selected item from cart?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                CartItems.Remove(SelectedCartItem);
                UpdateTotals();
            }
        }

        private void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (CartItems.Count == 0)
                return;

            if (MessageBox.Show(
                "Clear entire cart?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                CartItems.Clear();
                UpdateTotals();
            }
            ActivityRepository.Log(new Activity
        {
            EntityType = "Sale",
            Action = "CLEAR_CART",
            Reason = "User cleared cart",
            PerformedBy = SessionContext.CurrentUserName,
            CreatedAt = DateTime.Now
        });

        }

        private void AddCustomer_Click(object sender, RoutedEventArgs e)
            {
                var nextHidden = _customerButtons
                    .FirstOrDefault(b => b.Visibility == Visibility.Collapsed);

                if (nextHidden == null)
                {
                    MessageBox.Show("Maximum customers reached");
                    return;
                }

                nextHidden.Visibility = Visibility.Visible;
                SetActiveCustomer(nextHidden);
            }
            private void Customer_Click(object sender, RoutedEventArgs e)
            {
                        if (sender is Button btn)
                        {
                            SetActiveCustomer(btn);
                        }
                    }


            private void SetActiveCustomer(Button btn)
            {
                foreach (var b in _customerButtons)
                {
                    b.Background = Brushes.White;
                    b.Foreground = Brushes.Black;
                }

                btn.Background = Brushes.DodgerBlue;
                btn.Foreground = Brushes.White;

                _activeCustomer = btn;

                // 🔴 MISSING PART — CART SWITCH
                if (!_customerCarts.ContainsKey(btn))
                {
                    _customerCarts[btn] = new ObservableCollection<SaleItem>();
                }

                CartItems = _customerCarts[btn];
                CartGrid.ItemsSource = CartItems;

                UpdateTotals();
            }





        // ================= TOTALS =================

           private void UpdateTotals()
        {
            decimal subtotal = CartItems.Sum(i => i.Total);

            decimal discount = CalculateDiscount(subtotal);
            if (discount > subtotal)
                discount = subtotal;

            decimal payable = subtotal - discount;
            if (payable < 0)
                payable = 0;

            decimal vat = 0m;

            if (_settings.VatEnabled)
            {
                vat = Math.Round(payable * VatRate, 2);
            }

            TotalItemsText.Text = CartItems.Sum(i => i.Quantity).ToString();
            SubtotalText.Text = subtotal.ToString("0.00");

            if (discount > 0)
            {
                DiscountRow.Visibility = Visibility.Visible;
                DiscountText.Text = "-" + discount.ToString("0.00");
            }
            else
            {
                DiscountRow.Visibility = Visibility.Collapsed;
            }

            // VAT is DISPLAY ONLY

            // 🔥 THIS IS WHAT CUSTOMER PAYS
            TotalAmountText.Text = payable.ToString("0.00");

            UpdateChange();
        }




       private void QuickPayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            string method = btn == CashQuickBtn ? "Cash" : "Mpesa";

            // Toggle OFF
            if (_quickPaymentMethod == method)
            {
                _quickPaymentMethod = null;
                ResetQuickPaymentButtons();

                CashRow.Visibility = Visibility.Collapsed;
                ChangeRow.Visibility = Visibility.Collapsed;
                CashGivenBox.Text = "";
                ChangeText.Text = "0.00";
                return;
            }

            // Switch payment method
            _quickPaymentMethod = method;
            HighlightQuickPaymentButton(btn);

            if (method == "Cash")
            {
                CashRow.Visibility = Visibility.Visible;
                ChangeRow.Visibility = Visibility.Visible;
                CashGivenBox.Focus();
            }
            else
            {
                CashRow.Visibility = Visibility.Collapsed;
                ChangeRow.Visibility = Visibility.Collapsed;
            }
        }


        private void HighlightQuickPaymentButton(Button active)
        {
            ResetQuickPaymentButtons();

            active.Background = Brushes.DodgerBlue;
            active.Foreground = Brushes.White;
        }

        private void ResetQuickPaymentButtons()
        {
            CashQuickBtn.Background = Brushes.White;
            CashQuickBtn.Foreground = Brushes.Black;

            MpesaQuickBtn.Background = Brushes.White;
            MpesaQuickBtn.Foreground = Brushes.Black;
        }


        private void CashGiven_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CartItems.Count == 0)
            {
                CashRow.Visibility = Visibility.Collapsed;
                ChangeRow.Visibility = Visibility.Collapsed;
                return;
            }
            UpdateChange();
        }

    private void UpdateChange()
    {
        if (CashRow.Visibility != Visibility.Visible)
            return;

        decimal total = decimal.Parse(TotalAmountText.Text);
        decimal cashGiven = 0;

        decimal.TryParse(CashGivenBox.Text, out cashGiven);

        decimal change = cashGiven - total;
        if (change < 0) change = 0;

        ChangeText.Text = change.ToString("0.00");
    }
        private void RefreshSettingsIfNeeded()
        {
            if (_lastSettingsVersion != SettingsCache.Version)
            {
                _lastSettingsVersion = SettingsCache.Version;
                LoadDiscountSettings();
                ApplyReceiptSettings();
            }
        }
private bool ValidateCart()
{
    foreach (var item in CartItems)
    {
        if (item.Quantity <= 0)
        {
            MessageBox.Show($"Invalid quantity for {item.Name}");
            return false;
        }

        if (item.Price <= 0)
        {
            MessageBox.Show($"Invalid price for {item.Name}");
            return false;
        }

        // 🔥 NEW: PRICE RULE SPLIT
        if (!item.IsRepack) // ✅ ONLY FULL ITEMS
        {
            if (item.Price < item.MarkedPrice)
            {
                MessageBox.Show(
                    $"Item '{item.Name}' has a price below allowed ({item.MarkedPrice})",
                    "Price Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }
        }
        else
        {
            // ✅ REPACK → allow rule price (bypass)
            // Optional: you can still block zero/negative profit here if you want
        }
    }

    return true;
}

private bool ValidateSaleItem(SaleItem item, ItemLookup dbItem, bool isEdit = false)
{
    // 🔴 Quantity check
    if (item.Quantity <= 0)
    {
        MessageBox.Show($"Invalid quantity for {item.Name}");
        return false;
    }

    // 🔴 Price check
    if (item.Price <= 0)
    {
        MessageBox.Show($"Invalid price for {item.Name}");
        return false;
    }

    // ================= STOCK CHECK =================
    if (!SettingsCache.Current.AllowNegativeStock)
    {
if (item.IsRepack)
        {
            // ✅ Use repack stock directly (NO conversion)
            if (item.Quantity > dbItem.Quantity)
            {
                MessageBox.Show(
                    $"OUT OF REPACK STOCK\n\n" +
                    $"Item: {item.Name}\n" +
                    $"Available: {dbItem.Quantity}\n" +
                    $"Requested: {item.Quantity}",
                    "Stock Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }
        }
        else
        {
            // ✅ NORMAL ITEM (UNCHANGED)
            if (item.Quantity > dbItem.Quantity)
            {
                MessageBox.Show(
                    $"OUT OF STOCK\n\n" +
                    $"Item: {item.Name}\n" +
                    $"Available: {dbItem.Quantity}\n" +
                    $"Requested: {item.Quantity}",
                    "Stock Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }
        }
    }

    // ================= PRICE CHECK =================
    if (!item.IsRepack)
    {
        // 🔴 STRICT for full items
        if (item.Price < item.MarkedPrice)
        {
            var result = MessageBox.Show(
                $"SELLING PRICE IS BELOW THE MINIMUM PRICE\n\n" +
                $"Item: {item.Name}\n\n" +
                $"Selling Price: {item.Price:0.00}\n" +
                $"Minimum (Unit): {item.MarkedPrice:0.00}\n\n" +
                $"Do you want to proceed?",
                "Price Protection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes)
                return false;
        }
    }
    else
    {
        // ✅ REPACK BYPASS
        // You can optionally enforce rule price here if needed
    }

    return true;
}

        // ================= FINISH & PAY =================
private void FinishPay_Click(object sender, RoutedEventArgs e)
{
    RefreshSettingsIfNeeded();

    // ================= QUICK PAYMENT =================
    if (_quickPaymentMethod != null)
    {
        if (CartItems.Count == 0)
        {
            MessageBox.Show("Cart is empty.");
            return;
        }

        if (!ValidateCart())
            return;

        decimal grossTotal = CartItems.Sum(i => i.Total);
        decimal discount = CalculateDiscount(grossTotal);

        decimal vat = 0m;

        if (_settings.VatEnabled)
        {
            vat = Math.Round(
                grossTotal * VatRate / (1 + VatRate),
                2
            );
        }

        decimal subtotal = grossTotal - vat - discount;
        if (subtotal < 0) subtotal = 0;

        decimal total = subtotal + vat;

        string cashier = SessionContext.CurrentUserName;

        var payment = new PaymentResult(
            subtotal,
            discount,
            vat,
            total,
            0,
            0
        );
        payment.IsQuickCash = _quickPaymentMethod == "Cash";

        // ================= CASH =================
        if (_quickPaymentMethod == "Cash")
        {
            if (!decimal.TryParse(CashGivenBox.Text, out decimal cashGiven) || cashGiven <= 0)
            {
                MessageBox.Show("Enter a valid cash amount.");
                return;
            }

            if (cashGiven < total)
            {
                MessageBox.Show("Cash given is less than total.");
                return;
            }

            payment.SetPayment(cashGiven, 0, null, null);
        }
        else // MPESA
        {
            payment.SetPayment(0, payment.Total, null, null);

            PendingSaleStore.CartItems = CartItems.ToList();
            PendingSaleStore.Payment = payment;
            PendingSaleStore.Cashier = cashier;

            string receiptNumber = ReceiptNumberService.Generate();

            var pending = new PendingWindow(receiptNumber,payment);

            pending.PaymentCompleted += () =>
            {
                string savedReceipt = SalesRepository.SaveSale(
                    PendingSaleStore.CartItems,
                    PendingSaleStore.Cashier,
                    payment.Subtotal,
                    payment.Discount,
                    payment.Vat,
                    payment.Total,
                    payment.Total
                );

                SalesRepository.SaveMpesaCodes(
                    savedReceipt,
                    pending.MpesaCodes
                );

                PaymentRepository.SavePayment(
                    savedReceipt,
                    "Mpesa",
                    payment.Total
                );

                foreach (int id in pending.UsedMessageIds)
                {
                    MpesaRepository.MarkAsUsed(id);
                }

                if (ShouldPrint())
                {
                    ShowReceiptWithSettings(
                        CartItems,
                        payment,
                        receiptNumber,
                        cashier,
                        ReceiptMode.Original
                    );
                }

                if (ShouldPreview())
                {
                    PreviewReceiptSimple(
                        CartItems,
                        payment,
                        receiptNumber,
                        cashier,
                        ReceiptMode.Original
                    );
                }

                CartItems.Clear();
                UpdateTotals();

                CashRow.Visibility = Visibility.Collapsed;
                ChangeRow.Visibility = Visibility.Collapsed;
                ChangeText.Text = "0.00";
                CashGivenBox.Text = "";
                SubtotalText.Text = "0.00";
                TotalAmountText.Text ="0.00";

                _quickPaymentMethod = null;
                ResetQuickPaymentButtons();
            };
            
            // MessageBox.Show($"Cart count: {PendingSaleStore.CartItems.Count}");
            pending.Show();

            return;
        }

        try
        {
            string receiptNumber = SalesRepository.SaveSale(
                CartItems.ToList(),
                cashier,
                subtotal,
                discount,
                vat,
                total,
                payment.AmountPaid
            );

            LoadItems();

            if (_quickPaymentMethod == "Cash")
            {
                PaymentRepository.SavePayment(
                    receiptNumber,
                    "Cash",
                    payment.AmountPaid
                );
            }

            MessageBox.Show(
                "Transaction successful!",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            if (ShouldPrint())
            {
                ShowReceiptWithSettings(
                    CartItems,
                    payment,
                    receiptNumber,
                    cashier,
                    ReceiptMode.Original
                );
            }

            if (ShouldPreview())
            {
                PreviewReceiptSimple(
                    CartItems,
                    payment,
                    receiptNumber,
                    cashier,
                    ReceiptMode.Original
                );
            }

           // PreviewReceiptSimple(CartItems,payment,receiptNumber,cashier,ReceiptMode.Original);
           
           
            CartItems.Clear();
            UpdateTotals();

            CashRow.Visibility = Visibility.Collapsed;
            ChangeRow.Visibility = Visibility.Collapsed;
            ChangeText.Text = "0.00";
            CashGivenBox.Text = "";
            SubtotalText.Text = "0.00";
            TotalAmountText.Text ="0.00";

            _quickPaymentMethod = null;
            ResetQuickPaymentButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Sale failed");
        }

        return;
    }

    // ================= OLD SYSTEM =================

    if (CartItems.Count == 0)
    {
        MessageBox.Show("Cart is empty.");
        return;
    }

    if (!ValidateCart())
        return;

    if (!_settings.EnableCreditSales)
    {
        MessageBox.Show(
            "Credit sales are disabled in settings.\nPlease use quick payment.",
            "Payment Restricted",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
        return;
    }

    decimal grossTotalOld = CartItems.Sum(i => i.Total);
    decimal discountOld = CalculateDiscount(grossTotalOld);

    decimal vatOld = 0m;

    if (_settings.VatEnabled)
    {
        vatOld = Math.Round(
            grossTotalOld * VatRate / (1 + VatRate),
            2
        );
    }

    decimal subtotalOld = grossTotalOld - vatOld - discountOld;
    if (subtotalOld < 0) subtotalOld = 0;

    decimal totalOld = subtotalOld + vatOld;

    var paymentSeed = new PaymentResult(
        subtotalOld,
        discountOld,
        vatOld,
        totalOld,
        0,
        0
    );
    
    var paymentWindow = new PaymentWindow(paymentSeed)
    {
        Owner = GetMainWindow()
    };

    if (paymentWindow.ShowDialog() != true)
        return;

    PaymentResult paymentOld = paymentWindow.Result;

    if (paymentOld.AmountPaid <= 0 && paymentOld.Balance <= 0)
    {
        MessageBox.Show("No payment entered.");
        return;
    }

    string cashierOld = SessionContext.CurrentUserName;

    int? customerId = null;
    decimal previousBalance = 0m;

    if (!string.IsNullOrWhiteSpace(paymentOld.CustomerName))
    {
        var customer = CreditRepository.FindCustomer(
            paymentOld.Phone ?? paymentOld.CustomerName
        );

        if (customer != null)
        {
            customerId = customer.Id;
            previousBalance = CreditRepository.GetCustomerBalance(customer.Id);
        }
    }

    decimal loanAmount = totalOld - paymentOld.AmountPaid;

    // 🔥 DEFAULT (new credit scenario)
    decimal newBalance = previousBalance + loanAmount;

    // 🔥 OVERRIDE if it's debt payment (overpay case)
    if (paymentOld.IsDebtPayment && customerId != null)
    {
        newBalance = previousBalance - paymentOld.CreditPaid;

        if (newBalance < 0)
            newBalance = 0;
    }

    // ✅ SET FINAL VALUES (single source of truth)
    paymentOld.BalanceBefore = previousBalance;
    paymentOld.TotalBalance = newBalance;
    paymentOld.BalanceAfter = newBalance;


    try
    {
        decimal salePayment = paymentOld.AmountPaid;

        if (paymentOld.IsDebtPayment)
        {
            salePayment -= paymentOld.CreditPaid;
        }

        string receiptNumber = SalesRepository.SaveSale(
            CartItems.ToList(),
            cashierOld,
            subtotalOld,
            discountOld,
            vatOld,
            totalOld,
            salePayment
        );

        LoadItems();

        bool hasCash = paymentOld.CashAmount > 0;
        bool hasMpesa = paymentOld.MpesaAmount > 0;

        // CASH ENTRY

        if (paymentOld.CashAmount > 0)
        {
            PaymentRepository.SavePayment(
                receiptNumber,
                "Cash",
                paymentOld.CashAmount
            );
        }

        // MPESA ENTRY

        if (paymentOld.MpesaAmount > 0)
        {
            PaymentRepository.SavePayment(
                receiptNumber,
                "Mpesa",
                paymentOld.MpesaAmount
            );
        }

        // PURE CREDIT SALE

        if (paymentOld.Balance > 0)
        {
            PaymentRepository.SavePayment(
                receiptNumber,
                "Credit",
                0
            );
        }

        // EXTRA USED TO PAY OLD DEBT

        if (paymentOld.IsDebtPayment &&
            paymentOld.CreditPaid > 0)
        {
            PaymentRepository.SavePayment(
                receiptNumber,
                "Debt Payment",
                paymentOld.CreditPaid
            );
        }

        if (paymentOld.Balance > 0)
        {
            CreditRepository.SaveCredit(
                receiptNumber,
                paymentOld.CustomerName!,
                paymentOld.Phone,
                paymentOld.Total,
                paymentOld.AmountPaid,
                paymentOld.Balance,
                previousBalance
            );
        }

        MessageBox.Show(
            "Transaction successful!",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        if (ShouldPrint())
        {
        ShowReceiptWithSettings(
            CartItems,
            paymentOld,
            receiptNumber,
            cashierOld,
            ReceiptMode.Original
        );
        }
        if (ShouldPreview())
        {
        PreviewReceiptSimple(CartItems,paymentOld,receiptNumber,cashierOld,ReceiptMode.Original);
        }
        if (paymentOld.IsDebtPayment && customerId != null)
        {
            CreditRepository.AddPayment(
                customerId.Value,
                paymentOld.CreditPaid,
                "Paid for receipt " + receiptNumber,
                receiptNumber,
                paymentOld.BalanceBefore,
                paymentOld.TotalBalance
            );
        }

        CartItems.Clear();
        UpdateTotals();

        CashRow.Visibility = Visibility.Collapsed;
        ChangeRow.Visibility = Visibility.Collapsed;
        ChangeText.Text = "0.00";
        CashGivenBox.Text = "";
        SubtotalText.Text = "0.00";
        TotalAmountText.Text ="0.00";
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message, "Sale failed");
    }
}

private void Discount_Click(object sender, RoutedEventArgs e)
{
    // 🔑 FORCE settings refresh FIRST
    if (_lastSettingsVersion != SettingsCache.Version)
    {
        _lastSettingsVersion = SettingsCache.Version;
        LoadDiscountSettings();
    }

    // ✅ NOW settings are guaranteed fresh
    if (_settings == null || !_settings.EnableDiscounts)
    {
        MessageBox.Show("Discounts are disabled in settings.");
        return;
    }

    if (_discountMode == DiscountMode.None)
    {
        MessageBox.Show("No discount type is enabled in settings.");
        return;
    }

    _hasDiscount = !_hasDiscount;

    if (sender is Button btn)
    {
        btn.Background = _hasDiscount ? Brushes.Blue : Brushes.White;
        btn.Foreground = _hasDiscount ? Brushes.White : Brushes.Black;
    }

    UpdateTotals();
}
        private decimal CalculateDiscount(decimal subtotal)
        {
            if (!_settings.EnableDiscounts || !_hasDiscount)
                return 0m;

            switch (_discountMode)
            {
                case DiscountMode.Conditional:
                    if (_settings.AllowConditionalDiscount &&
                        _settings.CondBasedRanges &&
                        _settings.DiscountRanges?.Count > 0)
                    {
                        // Find the first matching range for the subtotal
                        var match = _settings.DiscountRanges
                            .OrderBy(r => r.From)
                            .FirstOrDefault(r => subtotal >= r.From && subtotal <= r.To);

                        if (match != null)
                        {
                            // Conditional percentage
                            if (match.DiscountPercent.HasValue)
                                return Math.Round(subtotal * (match.DiscountPercent.Value / 100m), 2);

                            // Conditional fixed amount
                            if (match.DiscountAmount.HasValue)
                                return Math.Min(match.DiscountAmount.Value, subtotal);
                        }
                    }
                    return 0m;

                case DiscountMode.Fixed:
                    return (_settings.AllowFixedDiscount && _settings.MaxFixedDiscount > 0)
                        ? Math.Min(_settings.MaxFixedDiscount, subtotal)
                        : 0m;

                case DiscountMode.Percentage:
                    return (_settings.AllowPercentageDiscount && _settings.MaxPercentageDiscount > 0)
                        ? Math.Round(subtotal * (_settings.MaxPercentageDiscount / 100m), 2)
                        : 0m;

                default:
                    return 0m;
            }
        }


private void ApplyReceiptSettings()
{
    var settings = SettingsCache.Current;

    if (settings == null)
        return;

    // Enable/disable checkbox
        PrintReceiptCheckBox.IsEnabled = settings.AutoPrintReceipt;
        PrintReceiptCheckBox.IsChecked = settings.AutoPrintReceipt;
    if (settings.AutoPrintReceipt)
    {
        // ✅ DEFAULT = CHECKED (AUTO PRINT ON)
        if (PrintReceiptCheckBox.IsChecked == null)
            PrintReceiptCheckBox.IsChecked = true;
    }
    else
    {
        // ❌ System disables printing
        PrintReceiptCheckBox.IsChecked = false;
    }
}

private bool ShouldPrintReceipt()
{
    var settings = SettingsCache.Current;

    // RULE 1: settings must allow
    if (settings?.AutoPrintReceipt != true)
        return false;

    // RULE 2: checkbox must be checked
    if (PrintReceiptCheckBox.IsChecked != true)
        return false;

    return true;
}

private bool ShouldPrint()
{
    return PrintReceiptCheckBox.IsChecked == true;
}

private bool ShouldPreview()
{
    return PreviewReceiptCheckBox.IsChecked == true;
}

private void ShowReceiptWithSettings(
    IEnumerable<SaleItem> items,
    PaymentResult payment,
    string receiptNumber,
    string cashier,
    ReceiptMode initialMode
    )
{
    var settings = SettingsCache.Current;

    if (settings?.AutoPrintReceipt != true)
        return;

    if (PrintReceiptCheckBox.IsChecked != true)
        return;

    int copies = settings.ReceiptCopies <= 0 ? 1 : settings.ReceiptCopies;

    for (int i = 0; i < copies; i++)
    {
        ReceiptMode mode;

        if (i == 0)
            mode = ReceiptMode.Original;
        else
            mode = ReceiptMode.Copy;

        var receiptText = ReceiptBuilder.Build(
            items,
            payment,
            receiptNumber,
            cashier,
            mode
        );

        PrintService.PrintReceiptText(receiptText, "Receipt", 1);
    }
}
private void PreviewReceiptSimple(
    IEnumerable<SaleItem> items,
    PaymentResult payment,
    string receiptNumber,
    string cashier,
    ReceiptMode mode)
{
    var receiptText = ReceiptBuilder.Build(
        items,
        payment,
        receiptNumber,
        cashier,
        mode
    );

    var preview = new ReceiptPreviewWindow(receiptText);
    preview.ShowDialog();
}
private void SaleTypeToggle_Checked(object sender, RoutedEventArgs e)
{
    _isWholesale = true;
    SaleTypeToggle.Content = "Wholesale";
    RefreshCartPrices();
    LoadItems();
}

private void SaleTypeToggle_Unchecked(object sender, RoutedEventArgs e)
{
    _isWholesale = false;
    SaleTypeToggle.Content = "Retail";
    RefreshCartPrices();
    LoadItems();
}
private void RefreshCartPrices()
{
    foreach (var item in CartItems)
    {
        var dbItem = _allItems.FirstOrDefault(i => i.Id == item.ItemId);
        if (dbItem == null)
            continue;

        if (_isWholesale && dbItem.WholesalePrice.HasValue && dbItem.WholesalePrice > 0)
            item.Price = dbItem.WholesalePrice.Value;
        else
            item.Price = dbItem.SellingPrice;
    }

    CartGrid.Items.Refresh();
    UpdateTotals();
}

private void OpenSelectWindow(object selectedObj)
{
    if (selectedObj == null)
        return;

    // ---------------- EXTRACT ITEM ----------------
    var dbItem = selectedObj
        .GetType()
        .GetProperty("Item")
        ?.GetValue(selectedObj) as ItemLookup;

    bool isRepack = (bool)selectedObj
        .GetType()
        .GetProperty("IsRepack")
        ?.GetValue(selectedObj);

    var repack = selectedObj
        .GetType()
        .GetProperty("Repack")
        ?.GetValue(selectedObj) as RepackStockModel;

    // ---------------- PRICE ----------------
    decimal price = dbItem.SellingPrice;

    if (_isWholesale && dbItem.WholesalePrice.HasValue && dbItem.WholesalePrice > 0)
        price = dbItem.WholesalePrice.Value;

    // ---------------- SALE ITEM ----------------
    var saleItem = new SaleItem
    {
        ItemId = dbItem.Id,
        Name = dbItem.Name,
        UnitType = dbItem.UnitType,
        Quantity = 1,
        Price = isRepack ? 0 : price,
        MarkedPrice = dbItem.MarkedPrice,
        BranchId = SessionContext.EffectiveBranchId,
        SaleType = _isWholesale ? "Wholesale" : "Retail",
        IsRepack = isRepack,
        RepackId = repack?.Id,
         AvailableQuantity = dbItem.Quantity,
        RepackAvailableQty = repack?.RemainingQuantity ?? 0
    };

    // ---------------- RULES ----------------
    List<RepackRuleModel> rules = isRepack
        ? RepackRepository.GetRulesForItem(dbItem.Id)
            .Where(r => r.IsActive)
            .ToList()
        : new List<RepackRuleModel>();

    // ---------------- OPEN YOUR EXISTING WINDOW ----------------
    var dialog = new SelectItemWindow(saleItem, rules)
    {
        Owner = GetMainWindow()
    };

    if (dialog.ShowDialog() == true)
    {
        // 🔥 keep your existing safety rule
        if (saleItem.Price < saleItem.MarkedPrice)
        {
            MessageBox.Show("Cannot sell below unit price.");
            return;
        }

        CartItems.Add(saleItem);
        UpdateTotals();
    }
    
}
private void ItemLookup_Click(object sender, RoutedEventArgs e)
{
    if (!_isLookupOpen)
    {
        LeftColumn.Width = new GridLength(100);
        _isLookupOpen = true;
    }
    else
    {
        LeftColumn.Width = new GridLength(0);
        _isLookupOpen = false;
    }
}
private void Calculator_Click(object sender, RoutedEventArgs e)
{
    var calc = new CalculatorWindow()
        {
            Owner = GetMainWindow() 
        };
        calc.ShowDialog();
            
}
private void ScannerBox_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Key != Key.Enter)
        return;

    _barcodeTimer.Stop();

    if (_addingBarcode)
        return;

    _addingBarcode = true;

    try
    {
        AddScannedItem();
    }
    finally
    {
        _addingBarcode = false;
    }

    e.Handled = true;
}


private void ScannerBox_TextChanged(object sender, TextChangedEventArgs e)
{
    if (_addingBarcode)
        return;

    // restart timer every time a character arrives
    _barcodeTimer.Stop();
    _barcodeTimer.Start();
}
private void BarcodeTimer_Tick(object? sender, EventArgs e)
{
    _barcodeTimer.Stop();

    if (_addingBarcode)
        return;

    if (string.IsNullOrWhiteSpace(ScannerBox.Text))
        return;

    _addingBarcode = true;

    try
    {
        AddScannedItem();
    }
    finally
    {
        _addingBarcode = false;
    }
}
private void AddScannedItem()
{
    string barcode = ScannerBox.Text.Trim();

    if (string.IsNullOrWhiteSpace(barcode))
        return;

    // Lookup barcode
    var result = ItemBarcodeRepository.GetItemByfBarcode(barcode);

    if (result == null)
    {
        MessageBox.Show("Barcode not found.");

        ScannerBox.Clear();
        ScannerBox.Focus();
        Keyboard.Focus(ScannerBox);
        return;
    }

    ItemModel item = result.Item;
    decimal unitsPerScan = result.UnitsPerScan;

    // Already in cart?
    var existing = CartItems.FirstOrDefault(x => x.ItemId == item.Id);

    if (existing != null)
    {
        existing.Quantity += unitsPerScan;
    }
    else
    {
        CartItems.Add(new SaleItem
        {
            ItemId = item.Id,
            Barcode = barcode,
            Name = item.Name,
            Price = item.SellingPrice,
            Quantity = unitsPerScan,
            UnitType= item.UnitType,
            SaleType = _isWholesale ? "Wholesale" : "Retail",
            AvailableQuantity = item.Quantity
        });
    }

        CartGrid.Items.Refresh();
        UpdateTotals();

        // Clear AFTER everything succeeded
        ScannerBox.Clear();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            ScannerBox.Focus();
            Keyboard.Focus(ScannerBox);
        }), DispatcherPriority.Background);
}
private void PendingWindow_Loaded(object sender, RoutedEventArgs e)
{
    ScannerBox.Focus();
    Keyboard.Focus(ScannerBox);
}
private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
{
    Dispatcher.BeginInvoke(new Action(() =>
    {
        ScannerBox.Focus();
        Keyboard.Focus(ScannerBox);
    }),
    DispatcherPriority.Background);
}
protected override void OnContentRendered(EventArgs e)
{
    base.OnContentRendered(e);

    ScannerBox.Focus();
    Keyboard.Focus(ScannerBox);
}
private void Drawer_Click(object sender, RoutedEventArgs e )
{
}

            }
        }