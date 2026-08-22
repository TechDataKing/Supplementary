using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Data;
using System.Collections.ObjectModel;

using TechKingPOS.App.Data;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;



namespace TechKingPOS.App
{
    public partial class ManageStockWindow : Window, ISupportBranchRefresh
    {
    private List<ItemLookup> _allItems;
        private List<DamagedItemVM> _damagedItems = new();
        // ================= DAMAGED GOODS STATE =================

// all items used for searching (reuse ItemLookup)
private List<ItemLookup> _damagedSearchItems;
// currently selected source item (from Items table)
private ItemLookup? _selectedDamagedSourceItem = null;
// Add this at the top of your class, with other private fields
private List<ItemLookup> _popupSelectedItems = new List<ItemLookup>();


// in-memory damaged list (until DB table is created)
// ================= REPACK STATE =================
private List<RepackStockModel> _repackItems = new();

private List<ItemLookup> _results = new();
private RepackStockModel? _selectedRepackItem = null;
private RepackRuleModel? _selectedRepackRule = null;
private bool _isEditMode = false;
private TabItem? _lastAllowedTab;
private bool _isLoaded = false;
private List<ItemLookup> _selectedItems = new List<ItemLookup>(); // This will store selected items

private bool _isLoading;
private LocationModel? _currentRootSelection;

private TreeViewItem _currentRoot;
private string _activeRootForSection = "";


private TreeViewItem _currentSection;

        public ManageStockWindow()
        {
            InitializeComponent();

            var status = LicenseService.GetStatus(SettingsCache.Current);

            TargetSearchBox.TextChanged += TargetSearchBox_TextChanged;
            EditSearchBox.TextChanged += EditSearchBox_TextChanged;
            EditItemsList.SelectionChanged += EditItemsList_SelectionChanged;
            
            DamageReasonCombo.SelectionChanged += DamageReasonCombo_SelectionChanged;
            DamagedItemSearchList.SelectionChanged += DamagedItemSearchList_SelectionChanged;
            DamagedItemsList.SelectionChanged += DamagedItemsList_SelectionChanged;
            DamageReasonCombo.SelectedIndex= 0;

            SupplierItemsGrid.ItemsSource = _returnItems; 
            //CustomerReceiptSearchBox.TextChanged += CustomerReceiptSearchBox_TextChanged;
            InitializeRoots();
            LoadAllItems();
            ReturnTypeCombo.SelectedIndex = 1; // Supplier Return


        }
private void InitializeRoots()
{
    LocationsTree.Items.Clear();

    using var conn = DbService.GetConnection();
    conn.Open();
    var repo = new LocationRepository(conn);

    var roots = repo.GetRoots();

    // Ensure default roots exist
    if (!roots.Any(r => r.Name == "Store (Backroom)"))
    {
        repo.Add(new LocationModel { Name = "Store (Backroom)", ParentId = null, Type = "Root" });
    }
    if (!roots.Any(r => r.Name == "Shop Floor"))
    {
        repo.Add(new LocationModel { Name = "Shop Floor", ParentId = null, Type = "Root" });
    }

    roots = repo.GetRoots();

    foreach (var root in roots)
    {
        var rootNode = new TreeViewItem
        {
            Header = root.Name,
            Tag = root,
            IsExpanded = true
        };

        // Load sections
        var sections = repo.GetChildren(root.Id);
        foreach (var section in sections)
        {
            var sectionNode = new TreeViewItem
            {
                Header = section.Name,
                Tag = section,
                IsExpanded = true
            };

            // Load shelves
            var shelves = repo.GetChildren(section.Id);
            foreach (var shelf in shelves)
            {
                var shelfNode = new TreeViewItem
                {
                    Header = shelf.Name,
                    Tag = shelf
                };
                sectionNode.Items.Add(shelfNode);
            }

            rootNode.Items.Add(sectionNode);
        }

        LocationsTree.Items.Add(rootNode);
    }

    // Select Shop Floor by default
    var shopFloorNode = LocationsTree.Items
        .Cast<TreeViewItem>()
        .FirstOrDefault(x => x.Header.ToString() == "Shop Floor");

    if (shopFloorNode != null)
    {
        LocationsTree.SelectedItemChanged -= LocationsTree_SelectedItemChanged;
        shopFloorNode.IsSelected = true;
        shopFloorNode.IsExpanded = true;
        LocationsTree.SelectedItemChanged += LocationsTree_SelectedItemChanged;

        _currentRootSelection = (LocationModel)shopFloorNode.Tag;
    }

    // Attach combo box events
    FromLocationCombo.SelectionChanged += (s, e) =>
        PopulateSectionCombo(FromLocationCombo.SelectedItem?.ToString(), false);

    ToLocationCombo.SelectionChanged += (s, e) =>
        PopulateSectionCombo(ToLocationCombo.SelectedItem?.ToString(), true);

    SectionCombo.SelectionChanged += SectionCombo_SelectionChanged;
}
        public void RefreshByBranch(int branchId)
{
    // Logic to refresh the window's data based on the new branch ID
    if (branchId != SessionContext.EffectiveBranchId)
    {
        // Update the branch ID in session context (if necessary)
        SessionContext.CurrentBranchId = branchId;

        // Reload the data for this branch (e.g., items, sales records)
        LoadAllItems(); // Re-load items for the new branch
    }
}

private void Window_Loaded(object sender, RoutedEventArgs e)
{
    // Prevent WPF from auto-selecting first tab
    StockTabs.SelectedIndex = -1;

    _isLoading = true;

    // Determine first allowed tab
    TabItem? firstAllowedTab = null;
    foreach (TabItem tab in StockTabs.Items)
    {
        string? key = tab.Tag as string;
        bool allowed = string.IsNullOrWhiteSpace(key) ||
                       PermissionService.Can(SessionService.CurrentUser.Id,
                                             SessionService.CurrentUser.Role,
                                             key);
        tab.IsEnabled = true;
        if (allowed && firstAllowedTab == null)
            firstAllowedTab = tab;
    }

    if (firstAllowedTab != null)
    {
        _lastAllowedTab = firstAllowedTab;
        StockTabs.SelectedItem = firstAllowedTab;
    }

    // Defer heavy loading to ensure SessionContext is ready
    Dispatcher.BeginInvoke(() =>
    {
        LoadAllItems();
        _isLoading = false;
    }, DispatcherPriority.Background);
}

/// <summary>
/// Loads items for all tabs in a branch-aware way, similar to SalesWindow.
/// </summary>
private void LoadAllItems()
{
    // ✅ Fetch all items (cumulative or branch-specific)
    _allItems = ItemRepository.GetAllItems();
    

    // -------------------- Targets Tab --------------------
    TargetGrid.ItemsSource = _allItems
        .Where(i => i.TargetQuantity == null) // only items without target
        .ToList();
    TargetGrid.DisplayMemberPath = "Display"; // use computed property
    LoggerService.Info("🎯", "STOCK", "Targets loaded",
                       $"Count={TargetGrid.Items.Count}");

    // -------------------- Edit Tab -----------------------
    EditItemsList.ItemsSource = _allItems.ToList();
    EditItemsList.DisplayMemberPath = "Display";

    // -------------------- Damaged Tab --------------------
    _damagedSearchItems = _allItems.ToList();
    DamagedItemSearchList.ItemsSource = _damagedSearchItems;
    DamagedItemSearchList.DisplayMemberPath = "Display";

    _damagedItems = DamagedRepository.GetAll()
        .Select(d => new DamagedItemVM
        {
            Id = d.Id,
            ItemId = d.ItemId,
            Name = d.ItemName,
            Alias = d.Alias,
            Quantity = d.Quantity,
            SellingPrice = d.SellingPrice,
            Reason = d.Reason,
            CreatedAt = d.DamagedAt
        })
        .ToList();
    DamagedItemsList.ItemsSource = _damagedItems;

    // -------------------- Repack Tab ---------------------
    _repackItems = RepackStockRepository.GetActive();
    RepackItemList.ItemsSource = _repackItems;
    RepackItemList.DisplayMemberPath = "Display";
    //-------------Move Stock Tab----------------
    // MoveSearchList.ItemsSource = _results;
    // MoveSearchList.DisplayMemberPath = "Display";

}


private void RefreshDamagedTab()
{
    LoadDamagedItems();
    ClearDamagedForm();
}

private void RefreshRepackTab()
{
    LoadRepackItems();
    LoadRepackRules();
}



private void RefreshReturnsTab()
{
    LoadSuppliers();
}

private void RefreshTab(TabItem tab)
{
    switch (tab.Tag?.ToString())
    {
        case "stock.targets":
            // RefreshTargetsTab();
            break;

        case "stock.edit_item":
            // RefreshEditItemsTab();
            break;

        case "stock.damaged":
            //RefreshDamagedTab();
            break;

        case "stock.repack":
            RefreshRepackTab();
            break;

        case "stock.returns":
            RefreshReturnsTab();
            break;
    }
}

private void StockTabItem_Loaded(object sender, RoutedEventArgs e)
{
    if (sender is not TabItem tab)
        return;

    string? key = tab.Tag as string;

    bool allowed =
        string.IsNullOrWhiteSpace(key) ||
        PermissionService.Can(
            SessionService.CurrentUser.Id,
            SessionService.CurrentUser.Role,
            key
        );

    if (!allowed)
    {
        // 🚫 PREVENT CONTENT FROM EVER RENDERING
        tab.IsEnabled = false;
        tab.Visibility = Visibility.Collapsed; // optional


        // 🔒 prevent default selection
        if (StockTabs.SelectedItem == tab)
            StockTabs.SelectedIndex = -1;
    }
}

private void StockTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (_isLoading)
        return;

    if (StockTabs.SelectedItem is not TabItem tab)
        return;

    string? permissionKey = tab.Tag as string;

    if (string.IsNullOrWhiteSpace(permissionKey))
    {
        _lastAllowedTab = tab;
        RefreshTab(tab);
        return;
    }

    bool allowed = PermissionService.Can(
        SessionService.CurrentUser.Id,
        SessionService.CurrentUser.Role,
        permissionKey
    );

    if (!allowed)
    {
        Dispatcher.BeginInvoke(() =>
        {
            MessageBox.Show(
                "Access denied",
                "Permission",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            _isLoading = true;

            if (_lastAllowedTab != null)
                StockTabs.SelectedItem = _lastAllowedTab;

            _isLoading = false;

        }, DispatcherPriority.Background);

        return;
    }

    _lastAllowedTab = tab;
    RefreshTab(tab);
    
}

private void StockTabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.OriginalSource is not DependencyObject source)
        return;

    var tab = ItemsControl.ContainerFromElement(
        StockTabs,
        source
    ) as TabItem;

    if (tab == null)
        return;

    string? permissionKey = tab.Tag as string;

    if (string.IsNullOrWhiteSpace(permissionKey))
        return;

    bool allowed = PermissionService.Can(
        SessionService.CurrentUser.Id,
        SessionService.CurrentUser.Role,
        permissionKey
    );

    if (!allowed)
    {
        // 🚫 BLOCK DEFAULT TAB SELECTION
        e.Handled = true;
        Dispatcher.BeginInvoke(() =>
    {
        MessageBox.Show(
            "Access denied",
            "Permission",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
    }, DispatcherPriority.Background);
        
    }
}

        private void LoadRepackItems()
        {
            _repackItems = RepackStockRepository.GetActive();
            RepackItemList.ItemsSource = _repackItems;
        }

        

        // ================= TARGET SEARCH =================
        private void TargetSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = TargetSearchBox.Text.Trim().ToLower();

            TargetGrid.ItemsSource = _allItems
                .Where(i =>
                    i.TargetQuantity == null &&
                    i.Name.ToLower().Contains(text))
                .ToList();
        }

        private void SetTarget_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ItemLookup item)
                return;

            string input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Set target for {item.Name}",
                "Set Target");

            if (!decimal.TryParse(input, out decimal target) || target < 0)
                return;

            ItemRepository.SetTarget(item.Id, target);

            LoggerService.Info("🎯", "STOCK", "Target set",
                $"{item.Name} → {target}");

            LoadAllItems();
        }

        // ================= EDIT SEARCH =================
        private void EditSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = EditSearchBox.Text.Trim().ToLower();

            EditItemsList.ItemsSource = _allItems
                .Where(i => i.Name.ToLower().Contains(text))
                .ToList();
        }

        private void EditItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditItemsList.SelectedItem is not ItemLookup item)
                return;

            NameBox.Text = item.Name;
            AliasBox.Text = item.Alias;
            QtyBox.Text = item.Quantity.ToString();
            MPBox.Text = item.MarkedPrice.ToString();
            SPBox.Text = item.SellingPrice.ToString();
            WPBox.Text = item.WholesalePrice.ToString();
            TargetBox.Text = item.TargetQuantity?.ToString() ?? "";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
{
    if (EditItemsList.SelectedItem is not ItemLookup item)
    {
        MessageBox.Show("Select an item first.");
        return;
    }

    if (!decimal.TryParse(QtyBox.Text, out decimal qty))
    {
        MessageBox.Show("Invalid quantity");
        return;
    }

    if (!decimal.TryParse(MPBox.Text, out decimal mp))
    {
        MessageBox.Show("Invalid marked price");
        return;
    }

    if (!decimal.TryParse(SPBox.Text, out decimal sp))
    {
        MessageBox.Show("Invalid selling price");
        return;
    }

    decimal? target = decimal.TryParse(TargetBox.Text, out decimal t) ? t : null;
    decimal? wp = decimal.TryParse(WPBox.Text, out decimal w) ? w : null;
    ItemRepository.UpdateItem(
        item.Id,
        NameBox.Text.Trim(),
        AliasBox.Text.Trim(),
        qty,
        mp,
        wp,
        sp,
        target,
        ActivityActions.STOCK_ADJUSTED
    );

    MessageBox.Show("Item saved");

    ClearEditForm();
    LoadAllItems();
    EditItemsList.ItemsSource = _allItems.ToList();
}



        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (EditItemsList.SelectedItem is not ItemLookup item)
                return;

            if (MessageBox.Show(
                $"Delete {item.Name}?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            ItemRepository.DeleteItem(item.Id);
            LoggerService.Info("🗑️", "STOCK", "Item deleted", item.Name);
            LoadAllItems();
            EditItemsList.ItemsSource = _allItems;
            ClearEditForm();

        }
        private void ClearEditForm()
        {
            EditItemsList.SelectedItem = null;

            BarcodeBox.Clear();
            NameBox.Clear();
            AliasBox.Clear();
            QtyBox.Clear();
            MPBox.Clear();
            SPBox.Clear();
            WPBox.Clear();
            TargetBox.Clear();
        }
        private void DamageReasonCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DamageReasonCombo.SelectedItem is ComboBoxItem item &&
                item.Content?.ToString() == "Other")
            {
                OtherReasonBox.Visibility = Visibility.Visible;
                OtherReasonBox.Focus();
            }
            else
            {
                OtherReasonBox.Clear();
                OtherReasonBox.Visibility = Visibility.Collapsed;
            }
        }
        private void ClearDamagedForm_Click(object sender, RoutedEventArgs e)
        {
            ClearDamagedForm();
        }

        private void ClearDamagedForm()
        {
            DamagedItemSearchList.SelectedItem = null;
            _selectedDamagedSourceItem = null;

            DamagedNameBox.Clear();
            DamagedAliasBox.Clear();
            DamagedStockBox.Clear();
            DamagedPriceBox.Clear();
            DamagedQtyBox.Clear();

            DamageReasonCombo.SelectedIndex = 0;
            OtherReasonBox.Clear();
            OtherReasonBox.Visibility = Visibility.Collapsed;
        }


 private void DamagedSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = ((TextBox)sender).Text.Trim().ToLower();

            DamagedItemSearchList.ItemsSource = _damagedSearchItems
                .Where(i =>
                    i.Name.ToLower().Contains(text) ||
                    i.Alias.ToLower().Contains(text))
                .ToList();
        }
        private void DamagedItemSearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DamagedItemSearchList.SelectedItem is not ItemLookup item)
                return;

            _selectedDamagedSourceItem = item;

            DamagedNameBox.Text = item.Name;
            DamagedAliasBox.Text = item.Alias;
            DamagedStockBox.Text = item.Quantity.ToString();
            DamagedPriceBox.Text = item.SellingPrice.ToString("0.00");

            DamagedQtyBox.Clear();
        }


        private void AddDamaged_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDamagedSourceItem == null)
            {
                MessageBox.Show("Select an item first");
                return;
            }

            if (!decimal.TryParse(DamagedQtyBox.Text, out decimal qty) || qty <= 0)
            {
                MessageBox.Show("Invalid quantity");
                return;
            }

            if (qty > _selectedDamagedSourceItem.Quantity)
            {
                MessageBox.Show("Quantity exceeds stock");
                return;
            }

            string reason = (DamageReasonCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            if (reason == "Other")
                reason = OtherReasonBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("Provide reason");
                return;
            }

            DamagedRepository.AddDamage(new DamagedItem
            {
                ItemId = _selectedDamagedSourceItem.Id,
                ItemName = _selectedDamagedSourceItem.Name,
                Alias = _selectedDamagedSourceItem.Alias,
                UnitType = _selectedDamagedSourceItem.UnitType,
                Quantity = qty,
                MarkedPrice = _selectedDamagedSourceItem.MarkedPrice,
                SellingPrice = _selectedDamagedSourceItem.SellingPrice,
                Reason = reason,
                RecordedBy = "SYSTEM",
                DamagedAt = DateTime.Now
            });

            LoadDamagedItems();
            LoadAllItems();
            ClearDamagedForm();
        }


    private void DeleteDamaged_Click(object sender, RoutedEventArgs e)
    {
        if (DamagedItemsList.SelectedItem is not DamagedItemVM vm)
            return;

        if (MessageBox.Show(
            $"Delete damaged record for {vm.Name}?",
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        DamagedRepository.Delete(new DamagedItem
        {
            Id = vm.Id,
            ItemId = vm.ItemId,
            Quantity = vm.Quantity,
            SellingPrice = vm.SellingPrice,
            ItemName = vm.Name
        });

        LoadDamagedItems();
        LoadAllItems();
    }

        private void DamagedItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Needed only to enable Delete button
            // No logic required for now
        }
        private void LoadDamagedItems()
        {
            _damagedItems = DamagedRepository.GetAll()
                .Select(d => new DamagedItemVM
                {   Id = d.Id,
                    ItemId = d.ItemId,
                    Name = d.ItemName,
                    Alias = d.Alias,
                    Quantity = d.Quantity,
                    SellingPrice = d.SellingPrice,
                    Reason = d.Reason,
                    CreatedAt = d.DamagedAt
                })
                .ToList();

            DamagedItemsList.ItemsSource = _damagedItems;
        }
        private void RepackSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = RepackSearchBox.Text.Trim().ToLower();

            RepackItemList.ItemsSource = _repackItems
                .Where(i =>
                    i.ItemName.ToLower().Contains(text) )
                .ToList();
        }
private void RepackItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (RepackItemList.SelectedItem is not RepackStockModel item)
        return;

    _selectedRepackItem = item;

    RepackSelectedItemText.Text = $"Item: {item.ItemName}";
    RepackStockUnitText.Text = item.BaseUnit;
    RepackAvailableStockText.Text = $"{item.RemainingQuantity} {item.BaseUnit}";

    LoadRepackRules();
}

        private void RepackRulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRepackRule = RepackRulesGrid.SelectedItem as RepackRuleModel;
        }

        private void LoadRepackRules()
        {
            if (_selectedRepackItem == null)
                return;

            var rules = RepackRepository.GetRulesForItem(_selectedRepackItem.ItemId);

            RepackRulesGrid.ItemsSource = rules;

            // IMPORTANT: reset selection
            _selectedRepackRule = null;
            RepackRulesGrid.SelectedItem = null;
        }

        private void OpenAddRepackRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRepackItem == null)
            {
                MessageBox.Show("Select a bulk item first.");
                return;
            }
            _isEditMode = false;

            AddRuleItemText.Text = $"Item: {_selectedRepackItem.ItemName}";
            AddRuleUnitText.Text = $"Base Unit: {_selectedRepackItem.BaseUnit}";
            AddRuleUnitSuffix.Text = _selectedRepackItem.BaseUnit;

            AddRuleQuantityBox.Text = "";
            AddRulePriceBox.Text = "";
            AddRuleActiveCheck.IsChecked = true;

            AddRepackRulePanel.Visibility = Visibility.Visible;


        }
        private void SaveAddRepackRule_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(AddRuleQuantityBox.Text, out var unitValue) || unitValue <= 0)
            {
                MessageBox.Show("Invalid UnitValue.");
                return;
            }

            if (!decimal.TryParse(AddRulePriceBox.Text, out var price) || price <= 0)
            {
                MessageBox.Show("Invalid price.");
                return;
            }

            bool isActive = AddRuleActiveCheck.IsChecked == true;

            // ================= ADD =================
            if (!_isEditMode)
            {
                RepackRepository.AddRule(
                _selectedRepackItem.ItemId,
                _selectedRepackItem.ItemName,
                unitValue,
                _selectedRepackItem.BaseUnit,
                price
            );



                // ✅ ACTIVITY: ADD
                ActivityRepository.Log(new Activity
                {
                    EntityType = "REPACK_RULE",
                    EntityId = _selectedRepackItem.ItemId, // item context
                    EntityName = _selectedRepackItem.ItemName,
                    Action = "ADD",
                    QuantityChange = 0,
                    UnitType = _selectedRepackItem.BaseUnit,
                    UnitValue = unitValue,
                    BeforeValue = null,
                    AfterValue =
                        $"ItemId={_selectedRepackItem.ItemId}, " +
                        $"UnitValue={unitValue}, Price={price}, Active={isActive}",
                    Reason = "Add repack sale rule",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                });
            }
            // ================= EDIT =================
            else
            {
                if (_selectedRepackRule == null)
                    return;

                // SNAPSHOT BEFORE
                var oldUnitValue = _selectedRepackRule.UnitValue;
                var oldPrice = _selectedRepackRule.SellingPrice;
                var oldActive = _selectedRepackRule.IsActive;

                RepackRepository.UpdateRule(
                    _selectedRepackRule.Id,
                    unitValue,
                    price,
                    isActive
                );

                // ✅ ACTIVITY: EDIT
                ActivityRepository.Log(new Activity
                {
                    EntityType = "REPACK_RULE",
                    EntityId = _selectedRepackRule.Id,
                    EntityName = _selectedRepackItem.ItemName,
                    Action = "EDIT",
                    QuantityChange = 0,
                    UnitType = _selectedRepackRule.UnitType,
                    UnitValue = unitValue,
                    BeforeValue =
                        $"ItemId={_selectedRepackItem.ItemId}, " +
                        $"UnitValue={oldUnitValue}, Price={oldPrice}, Active={oldActive}",

                    AfterValue =
                        $"ItemId={_selectedRepackItem.ItemId}, " +
                        $"UnitValue={unitValue}, Price={price}, Active={isActive}",
                    Reason = "Edit repack sale rule",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                });
            }

            AddRepackRulePanel.Visibility = Visibility.Collapsed;
            _isEditMode = false;
            LoadRepackRules();
        }


        private void CancelAddRepackRule_Click(object sender, RoutedEventArgs e)
        {
            AddRepackRulePanel.Visibility = Visibility.Collapsed;
        }
        private void DisableRepackRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRepackRule == null)
            {
                MessageBox.Show("Select a rule first.");
                return;
            }

            if (MessageBox.Show(
                "Disable this sale rule?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // SNAPSHOT BEFORE
            var unitValue = _selectedRepackRule.UnitValue;
            var price = _selectedRepackRule.SellingPrice;

            RepackRepository.DisableRule(_selectedRepackRule.Id);

            // ✅ ACTIVITY: DISABLE
            ActivityRepository.Log(new Activity
            {
                EntityType = "REPACK_RULE",
                EntityId = _selectedRepackRule.Id,
                EntityName = _selectedRepackItem.ItemName,
                Action = "DISABLE",
                QuantityChange = 0,
                UnitType = _selectedRepackRule.UnitType,
                UnitValue = unitValue,
                BeforeValue =
                    $"ItemId={_selectedRepackItem.ItemId}, " +
                    $"Active=true, UnitValue={unitValue}, Price={price}",

                AfterValue =
                    $"ItemId={_selectedRepackItem.ItemId}, " +
                    $"Active=false",
                Reason = "Disable repack sale rule",
                PerformedBy = SessionContext.CurrentUserName,
                BranchId = SessionContext.EffectiveBranchId,
                CreatedAt = DateTime.Now
            });

            LoadRepackRules();
        }

        private void DeleteRepackRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRepackRule == null)
            {
                MessageBox.Show("Select a rule first.");
                return;
            }

            if (MessageBox.Show(
                "Delete this rule? This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            // SNAPSHOT BEFORE
            var unitValue = _selectedRepackRule.UnitValue;
            var price = _selectedRepackRule.SellingPrice;

            RepackRepository.DeleteRule(_selectedRepackRule.Id);

            // ✅ ACTIVITY: DELETE
            ActivityRepository.Log(new Activity
            {
                EntityType = "REPACK_RULE",
                EntityId = _selectedRepackRule.Id,
                EntityName = _selectedRepackItem.ItemName,
                Action = "DELETE",
                QuantityChange = 0,
                UnitType = _selectedRepackRule.UnitType,
                UnitValue = unitValue,
                BeforeValue =
                    $"ItemId={_selectedRepackItem.ItemId}, " +
                    $"UnitValue={unitValue}, Price={price}",
                AfterValue = null,
                Reason = "Delete repack sale rule",
                PerformedBy = SessionContext.CurrentUserName,
                BranchId = SessionContext.EffectiveBranchId,
                CreatedAt = DateTime.Now
            });

            LoadRepackRules();
        }
        private void EditRepackRule_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRepackRule == null)
            {
                MessageBox.Show("Select a rule first.");
                return;
            }

            _isEditMode = true;

            AddRuleItemText.Text = $"Item: {_selectedRepackRule.ItemName}";
            AddRuleUnitText.Text = $"Base Unit: {_selectedRepackRule.UnitType}";
            AddRuleUnitSuffix.Text = _selectedRepackRule.UnitType;

            AddRuleQuantityBox.Text = _selectedRepackRule.UnitValue.ToString();
            AddRulePriceBox.Text = _selectedRepackRule.SellingPrice.ToString();
            AddRuleActiveCheck.IsChecked = _selectedRepackRule.IsActive;

            AddRepackRulePanel.Visibility = Visibility.Visible;
        }


// ================= LOCATIONS TREE =================
// Root button click handler
private void RootButton_Click(object sender, RoutedEventArgs e)
{
    if (sender is not Button btn) return;

    string rootName = btn.Content.ToString();

    using var conn = DbService.GetConnection();
    conn.Open();
    var repo = new LocationRepository(conn);

    var roots = repo.GetRoots();
    var selectedRoot = roots.FirstOrDefault(r => r.Name.Equals(rootName, StringComparison.OrdinalIgnoreCase));
    if (selectedRoot == null) return;

    _currentRootSelection = selectedRoot; // <-- store left panel selection

    SelectedRootText.Text = $"Selected: {selectedRoot.Name}";

    LocationsTree.Items.Clear();

    var rootNode = new TreeViewItem
    {
        Header = selectedRoot.Name,
        Tag = selectedRoot,
        IsExpanded = true
    };

    // Load Sections → Shelves
    var sections = repo.GetChildren(selectedRoot.Id);
    foreach (var section in sections)
    {
        var sectionNode = new TreeViewItem
        {
            Header = section.Name,
            Tag = section,
            IsExpanded = true
        };

        var shelves = repo.GetChildren(section.Id);
        foreach (var shelf in shelves)
        {
            var shelfNode = new TreeViewItem
            {
                Header = shelf.Name,
                Tag = shelf
            };
            sectionNode.Items.Add(shelfNode);
        }

        rootNode.Items.Add(sectionNode);
    }

    LocationsTree.Items.Add(rootNode);
}

private void LocationsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
{
    if (LocationsTree.SelectedItem is TreeViewItem selectedItem &&
        selectedItem.Tag is LocationModel location)
    {
        SelectedRootText.Text = $"Selected: {location.Name}";
    }
}

        // Add Section under the selected root
 private void AddSection_Click(object sender, RoutedEventArgs e)
{
    if (_currentRootSelection == null)
    {
        MessageBox.Show("Please select a root from the left panel.");
        return;
    }

    string name = NewNodeNameBox.Text.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        MessageBox.Show("Enter section name.");
        return;
    }

    using var conn = DbService.GetConnection();
    conn.Open();
    var repo = new LocationRepository(conn);

    var newLocation = new LocationModel
    {
        Name = name,
        ParentId = _currentRootSelection.Id,
        Type = "Section"
    };

    int newId = repo.Add(newLocation);
    newLocation.Id = newId;

    // Update TreeView display
    var rootNode = LocationsTree.Items
        .Cast<TreeViewItem>()
        .FirstOrDefault(x => ((LocationModel)x.Tag).Id == _currentRootSelection.Id);

    if (rootNode != null)
    {
        var newNode = new TreeViewItem
        {
            Header = name,
            Tag = newLocation
        };
        rootNode.Items.Add(newNode);
        rootNode.IsExpanded = true;
    }

    NewNodeNameBox.Clear();
}

    
        // Add Shelf under selected section
        private void AddShelf_Click(object sender, RoutedEventArgs e)
{
    if (LocationsTree.SelectedItem is not TreeViewItem selectedItem)
    {
        MessageBox.Show("Please select a section.");
        return;
    }

    var parentLocation = selectedItem.Tag as LocationModel;

    if (parentLocation == null || parentLocation.Type != "Section")
    {
        MessageBox.Show("You can only add shelves under a section.");
        return;
    }

    string name = NewNodeNameBox.Text.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        MessageBox.Show("Enter shelf name.");
        return;
    }

    // Duplicate check in UI
    foreach (TreeViewItem child in selectedItem.Items)
    {
        if (child.Header.ToString()
            .Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Shelf already exists in this section.");
            return;
        }
    }

    using var conn = DbService.GetConnection();
    conn.Open();
    var repo = new LocationRepository(conn);

    var newLocation = new LocationModel
    {
        Name = name,
        ParentId = parentLocation.Id,
        Type = "Shelf"
    };

    int newId = repo.Add(newLocation);
    newLocation.Id = newId;

    var newNode = new TreeViewItem
    {
        Header = name,
        Tag = newLocation
    };

    selectedItem.Items.Add(newNode);
    selectedItem.IsExpanded = true;

    NewNodeNameBox.Clear();
}

        // Edit the selected node (section or shelf)
private void EditSelected_Click(object sender, RoutedEventArgs e)
{
    if (LocationsTree.SelectedItem is not TreeViewItem selectedItem)
    {
        MessageBox.Show("Select a node to edit.");
        return;
    }

    if (selectedItem.Tag is not LocationModel location)
        return;

    // 🔹 Populate the textbox with the current name before editing
    NewNodeNameBox.Text = location.Name;

    string newName = NewNodeNameBox.Text.Trim();

    if (string.IsNullOrWhiteSpace(newName))
    {
        MessageBox.Show("Enter new name.");
        return;
    }

    // Duplicate check under same parent
    var parent = selectedItem.Parent as ItemsControl;

    if (parent != null)
    {
        foreach (TreeViewItem sibling in parent.Items)
        {
            if (sibling != selectedItem &&
                sibling.Header.ToString()
                .Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("A location with this name already exists.");
                return;
            }
        }
    }

    using var conn = DbService.GetConnection();
    conn.Open();
    var repo = new LocationRepository(conn);

    location.Name = newName;
    repo.Update(location);

    selectedItem.Header = newName;

    NewNodeNameBox.Clear();
}
        // Delete the selected node
        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
{
    if (LocationsTree.SelectedItem is not TreeViewItem selectedItem)
    {
        MessageBox.Show("Select a node to delete.");
        return;
    }

    if (selectedItem.Tag is not LocationModel location)
        return;

    if (location.ParentId == null)
    {
        MessageBox.Show("Root locations cannot be deleted.");
        return;
    }

    var confirm = MessageBox.Show(
        $"Are you sure you want to delete '{location.Name}'?",
        "Confirm Delete",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

    if (confirm != MessageBoxResult.Yes)
        return;

    using var conn = DbService.GetConnection();
    conn.Open();
    var repo = new LocationRepository(conn);

    repo.Delete(location.Id);

    var parent = selectedItem.Parent as ItemsControl;
    parent?.Items.Remove(selectedItem);

    NewNodeNameBox.Clear();
}


// ================= MOVE STOCK POPUP =================

private void UpdateCheckedItemsList()
{
    CheckedItemsList.ItemsSource = null;  // Clear existing items
    CheckedItemsList.ItemsSource = _selectedItems;  // Bind the updated list of selected items
}
private void MoveSearchList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (MoveSearchList.SelectedItem is ItemLookup selectedItem)
    {
        // Look for existing item in the selected items list
        var existing = _selectedItems
            .FirstOrDefault(i => i.Name.Equals(selectedItem.Name, StringComparison.OrdinalIgnoreCase));

        // Show input dialog with current available stock
        // If item exists, pre-fill with existing quantity for easy editing
        var quantityInputDialog = existing != null
            ? new QuantityInputDialog(selectedItem.Quantity, existing.Quantity)
            : new QuantityInputDialog(selectedItem.Quantity);

        if (quantityInputDialog.ShowDialog() == true)
        {
            decimal enteredQty = quantityInputDialog.Quantity;

            // Prevent quantity larger than available stock
            if (enteredQty > selectedItem.Quantity)
            {
                MessageBox.Show(
                    $"Cannot assign quantity greater than available stock ({selectedItem.Quantity}).",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (existing == null)
            {
                // ✅ Add new item
                _selectedItems.Add(new ItemLookup
                {
                    Name = selectedItem.Name,
                    Quantity = enteredQty,
                    IsSelected = true
                });
            }
            else
            {
                // ✅ Update existing item's quantity
                existing.Quantity = enteredQty;
            }

            // Refresh the UI
            UpdateCheckedItemsList();

            // Clear selection so the next double-click can select a new item
            MoveSearchList.SelectedItem = null;
        }
    }
}

private void MoveSearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // Ensure user actually clicked (ignore deselection)
    if (MoveSearchList.SelectedItem is ItemLookup selectedItem)
    {
        OpenQuantityDialog(selectedItem);

        // Clear selection so the next click works
        MoveSearchList.SelectedItem = null;
    }
}

// This method contains the logic from your old MouseDoubleClick
private void OpenQuantityDialog(ItemLookup selectedItem)
{
    try
    {
        // Look for existing item in the selected items list
        var existing = _selectedItems
            .FirstOrDefault(i => i.Name.Equals(selectedItem.Name, StringComparison.OrdinalIgnoreCase));

        var quantityInputDialog = existing != null
            ? new QuantityInputDialog(selectedItem.Quantity, existing.Quantity)
            : new QuantityInputDialog(selectedItem.Quantity);

        if (quantityInputDialog.ShowDialog() == true)
        {
            decimal enteredQty = quantityInputDialog.Quantity;

            // Prevent quantity larger than available stock
            if (enteredQty > selectedItem.Quantity)
            {
                MessageBox.Show(
                    $"Cannot assign quantity greater than available stock ({selectedItem.Quantity}).",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (existing == null)
            {
                // Add new item
                _selectedItems.Add(new ItemLookup
                {
                    Name = selectedItem.Name,
                    Quantity = enteredQty,
                    IsSelected = true
                });
            }
            else
            {
                // Update existing item's quantity
                existing.Quantity = enteredQty;
            }

            // Refresh the UI
            UpdateCheckedItemsList();
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error assigning quantity: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}


private void MoveSearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    string query = MoveSearchBox.Text.Trim();

    if (string.IsNullOrWhiteSpace(query))
    {
        MoveSearchList.ItemsSource = null;
        return;
    }

    // Filter _allItems without creating new instances (important for reference)
    var results = _allItems
        .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        .ToList();

    MoveSearchList.ItemsSource = results;
}

private void CloseMovementRulePopup_Click(object sender, RoutedEventArgs e)
{
    MovementRulePopup.Visibility = Visibility.Collapsed;
}
private void OpenMovementRulePopup_Click(object sender, RoutedEventArgs e)
{
    // Populate From/To combos from LocationsTree
    var roots = LocationsTree.Items.Cast<TreeViewItem>()
    .Select(x => x.Header.ToString())
    .ToList();

FromLocationCombo.ItemsSource = roots;
ToLocationCombo.ItemsSource = roots;

    // Optional: pre-select checked items (all items in _selectedItems that are IsSelected)
    var checkedItems = _selectedItems.Where(i => i.IsSelected).ToList();
    if (!checkedItems.Any())
    {
        MessageBox.Show("No items selected. Please double-click to assign quantities first.", 
                        "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    // Store checked items somewhere if needed, e.g. a temporary list
    _popupSelectedItems = checkedItems; // create a private List<ItemLookup> _popupSelectedItems

    MovementRulePopup.Visibility = Visibility.Visible;
}
// ================= ASSIGN MOVEMENT =================
private void AssignRule_Click(object sender, RoutedEventArgs e)
    {
        
    if (!_popupSelectedItems.Any())
    {
        MessageBox.Show("No items selected for movement.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    string from = FromLocationCombo.SelectedItem?.ToString() ?? "";
    string to = ToLocationCombo.SelectedItem?.ToString() ?? "";
    string section = SectionCombo.SelectedItem?.ToString() ?? "";
    string shelf = ShelfCombo.SelectedItem?.ToString() ?? "";
    string reason = (ReasonCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";

    foreach (var item in _popupSelectedItems)
    {
        int? fromId = GetSelectedLocationIdFromTree(from, section, shelf);
        int? toId   = GetSelectedLocationIdFromTree(to, section, shelf);

        if (fromId == null || toId == null)
        {
            MessageBox.Show("Invalid location selection.");
            return;
        }

        AssignedStockGrid.Items.Add(new
        {
            item.Name,
            item.Quantity,
            FromLocationId = fromId.Value,
            ToLocationId = toId.Value,
            Reason = reason
        });

        // Uncheck the item
        item.IsSelected = false;

        // Remove from the main selected items list
        _selectedItems.Remove(item);
    }

    // Clear the temporary list
    _popupSelectedItems.Clear();

    // Refresh the CheckedItemsList to reflect changes
    UpdateCheckedItemsList();

    // Hide the popup
    MovementRulePopup.Visibility = Visibility.Collapsed;
}


private void FromLocationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    _activeRootForSection = FromLocationCombo.SelectedItem?.ToString() ?? "";
    PopulateSectionCombo(_activeRootForSection);
}

private void ToLocationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    _activeRootForSection = ToLocationCombo.SelectedItem?.ToString() ?? "";
    PopulateSectionCombo(_activeRootForSection, isToLocation: true);
}

private TreeViewItem FindNodeByName(TreeViewItem parent, string name)
{
    if (parent.Header.ToString() == name)
        return parent;

    foreach (TreeViewItem child in parent.Items)
    {
        var found = FindNodeByName(child, name);
        if (found != null) return found;
    }

    return null;
}

private TreeViewItem FindNodeByName(string name)
{
    foreach (TreeViewItem root in LocationsTree.Items)
    {
        var found = FindNodeByName(root, name);
        if (found != null) return found;
    }
    return null;
}
private void PopulateSectionCombo(string selectedName, bool isToLocation = false)
{
    if (string.IsNullOrEmpty(selectedName)) return;

    var node = FindNodeByName(selectedName);
    if (node == null) return;

    // Get child names (sections or shelves)
    var childItems = node.Items.Cast<TreeViewItem>()
        .Select(x => x.Header.ToString())
        .ToList();

    if (!isToLocation)
        SectionCombo.ItemsSource = childItems;
    else
        SectionCombo.ItemsSource = childItems;

    SectionCombo.SelectedIndex = -1;

    // Clear shelves
    ShelfCombo.ItemsSource = null;
    ShelfCombo.SelectedIndex = -1;
}

private void SectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (SectionCombo.SelectedItem == null) return;
    if (string.IsNullOrEmpty(_activeRootForSection)) return;

    var parentNode = FindNodeByName(_activeRootForSection);
    if (parentNode == null) return;

    var sectionNode = parentNode.Items.Cast<TreeViewItem>()
        .FirstOrDefault(x => x.Header.ToString() == SectionCombo.SelectedItem.ToString());
    if (sectionNode == null) return;

    var shelves = sectionNode.Items.Cast<TreeViewItem>()
        .Select(x => x.Header.ToString())
        .ToList();

    ShelfCombo.ItemsSource = shelves;
    ShelfCombo.SelectedIndex = -1;
}

// ================= HELPERS =================
private List<string> GetAllLocations()
{
    var locations = new List<string>();

    foreach (TreeViewItem item in LocationsTree.Items)
        CollectLocations(item, locations);

    return locations;
}

private void CollectLocations(TreeViewItem node, List<string> list)
{
    if (node == null) return;

    list.Add(node.Header.ToString() ?? "");

    foreach (TreeViewItem child in node.Items)
        CollectLocations(child, list);
}
// ================= ASSIGNED STOCK BUTTONS =================

// Clear all assigned items
private void ClearAllAssigned_Click(object sender, RoutedEventArgs e)
{
    if (MessageBox.Show("Clear all assigned items?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
    {
        AssignedStockGrid.Items.Clear();
    }
}

private int? GetSelectedLocationIdFromTree(string rootName, string sectionName, string shelfName)
{
    var rootNode = FindNodeByName(rootName);
    if (rootNode?.Tag is not LocationModel rootLocation)
        return null;

    // Try shelf
    if (!string.IsNullOrWhiteSpace(sectionName) && !string.IsNullOrWhiteSpace(shelfName))
    {
        var sectionNode = rootNode.Items.Cast<TreeViewItem>()
            .FirstOrDefault(x => x.Header.ToString() == sectionName);

        if (sectionNode != null)
        {
            var shelfNode = sectionNode.Items.Cast<TreeViewItem>()
                .FirstOrDefault(x => x.Header.ToString() == shelfName);

            if (shelfNode?.Tag is LocationModel shelfLocation)
                return shelfLocation.Id;
        }
    }

    // Try section only
    if (!string.IsNullOrWhiteSpace(sectionName))
    {
        var sectionNode = rootNode.Items.Cast<TreeViewItem>()
            .FirstOrDefault(x => x.Header.ToString() == sectionName);

        if (sectionNode?.Tag is LocationModel sectionLocation)
            return sectionLocation.Id;
    }

    // Fallback to root
    return rootLocation.Id;
}

// Execute stock movement

private void ExecuteMoveStock_Click(object sender, RoutedEventArgs e)
{
    if (AssignedStockGrid.Items.Count == 0)
    {
        MessageBox.Show("No items assigned for movement.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    using var conn = DbService.GetConnection();
conn.Open();

// Create repository instance
var locationRepo = new LocationRepository(conn);

using var tx = conn.BeginTransaction();

try
{
    foreach (dynamic item in AssignedStockGrid.Items)
    {
        int itemId = _allItems.First(i => i.Name == item.Name).Id;

        // Use the instance
        int fromLocId = item.FromLocationId;
        int toLocId   = item.ToLocationId;


        decimal qty = item.Quantity;

            int BACKROOM_ID = 1; // use your real backroom ID

            if (fromLocId == BACKROOM_ID)
            {
                // Deduct from Items table
                ItemRepository.DeductStock(conn, tx, itemId, qty,
                    $"Move to Location {toLocId}",
                    SessionContext.CurrentUserName, ActivityActions.STOCK_OUT);
            }
            else
            {
                // Deduct from StockLevels table
                ItemRepository.DeductStockFromLocation(conn, tx, itemId, fromLocId, qty,
                    $"Move to Location {toLocId}",
                    SessionContext.CurrentUserName);
            }
        ItemRepository.AddStockToLocation(conn, tx, itemId, toLocId, qty);
        ItemRepository.LogStockMovement(conn, tx, itemId, fromLocId, toLocId, qty, item.Reason, SessionContext.CurrentUserName);
    }

    tx.Commit();
    MessageBox.Show("Stock moved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    AssignedStockGrid.Items.Clear();
}
catch (Exception ex)
{
    tx.Rollback();
    MessageBox.Show($"Error moving stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}

}

// Remove an assigned row
private void RemoveAssigned_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext != null)
    {
        AssignedStockGrid.Items.Remove(btn.DataContext);
    }
}
private void MoveToRepack_Click(object sender, RoutedEventArgs e)
{
    var win = new MoveToRepackWindow();


    // 🔥 IMPORTANT: attach Closed event
    win.Closed += (s, args) =>
    {
        LoadRepackItems();   // <-- reload left-side list
    };

    win.ShowDialog();
}

    // Toggle panels when return type changes
    private void ReturnTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReturnTypeCombo.SelectedItem is ComboBoxItem item)
        {
            string type = item.Content.ToString();
            if (type == "Customer Return")
            {
                CustomerReturnPanel.Visibility = Visibility.Visible;
                SupplierReturnPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                CustomerReturnPanel.Visibility = Visibility.Collapsed;
                SupplierReturnPanel.Visibility = Visibility.Visible;
            }
        }
    }

    // ---------------- Customer Return ----------------
    private void CustomerSearch_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Search items by receipt number and populate CustomerItemsGrid
    }


    private void CustomerCancelReturn_Click(object sender, RoutedEventArgs e)
    {
        CustomerItemsGrid.ItemsSource = null;
        CustomerReceiptSearchBox.Clear();
    }
private void CustomerProcessReturn_Click(object sender, RoutedEventArgs e)
{
    var selectedItems = _customerItems
        .Where(x => x.Selected && x.ReturnQty > 0)
        .ToList();

    if (!selectedItems.Any())
    {
        MessageBox.Show("No items selected");
        return;
    }

    var reason = (CustomerReasonCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
    var refundMethod = (CustomerRefundMethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (reason == "Select Reason" || string.IsNullOrWhiteSpace(reason))
        {
            MessageBox.Show("Please select a reason");
            return;
        }

        if (refundMethod == "Select Method" )
        {
            MessageBox.Show("Please select refund method");
            return;
        }

    var restock = CustomerRestockCheck.IsChecked == true;

    string receipt = CustomerReceiptSearchBox.Text;

    foreach (var item in selectedItems)
    {  
        
          decimal maxReturnable = item.SoldQty - item.AlreadyReturned;

        if (item.ReturnQty > maxReturnable)
        {
            MessageBox.Show(
                $"{item.ItemName}: Cannot return more than available.\n" +
                $"Max allowed: {maxReturnable}"
            );
            return;
        }
        MessageBox.Show(
    $"UNIT: {item.MarkedPrice} | SELL: {item.SellingPrice}"
);
        decimal unitPrice = item.MarkedPrice;
        decimal sellingPrice = item.SellingPrice;

        // ✅ ALWAYS INSERT RETURN RECORD
       ReturnsRepository.InsertReturn(
        receiptNumber: receipt, // 🔥 THIS IS KEY
        itemId: item.ItemId,
        itemName: item.ItemName,
        alias: null,
        quantity: item.ReturnQty,
        unitPrice: unitPrice,
        price: sellingPrice,
        reason: reason,
        returnType: "Customer Return",
        performedBy: SessionContext.CurrentUserName,
        branchId: SessionContext.EffectiveBranchId,
        saleDate: DateTime.Now,
        refundMethod: refundMethod,
        restock: restock

        );

        // ✅ CONDITIONAL RESTOCK

        if (restock)
        {
            ItemRepository.AddStock(
                item.ItemId,
                item.ReturnQty,
                item.MarkedPrice,
                item.SellingPrice,
                0,
                ActivityActions.CUSTOMER_RETURN_RESTOCK
            );
        }
    }

MessageBox.Show("Return processed successfully");

_customerItems.Clear();
CustomerItemsGrid.ItemsSource = null;

CustomerReceiptSearchBox.Clear();
CustomerSaleInfo.Text = "";

// ✅ RESET COMBOS
CustomerReasonCombo.SelectedIndex = 0;
CustomerRefundMethodCombo.SelectedIndex = 0;

// ✅ RESET CHECKBOX
CustomerRestockCheck.IsChecked = false;}


private ObservableCollection<CustomerReturnItem> _customerItems = new();
private void CustomerReceiptSearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    string text = CustomerReceiptSearchBox.Text.Trim();

    if (string.IsNullOrEmpty(text))
    {
        CustomerItemSuggestions.Visibility = Visibility.Collapsed;
        CustomerItemSuggestions.ItemsSource = null;
        return;
    }

    var receipts = ReturnsRepository.GetReceipts(text);

    CustomerItemSuggestions.ItemsSource = receipts;

    CustomerItemSuggestions.Visibility = receipts.Any()
        ? Visibility.Visible
        : Visibility.Collapsed;
}

private void CustomerItemSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (CustomerItemSuggestions.SelectedItem is not string receipt)
        return;

    CustomerReceiptSearchBox.Text = receipt;

    CustomerItemSuggestions.Visibility = Visibility.Collapsed;
    CustomerItemSuggestions.SelectedItem = null;

    // 🔥 LOAD ITEMS
    var soldItems = ReturnsRepository.GetItemsByReceipt(receipt);

    _customerItems.Clear();

    foreach (var item in soldItems)
    {
        var returnedQty = ReturnsRepository.GetReturnedQty(item.ItemId, receipt);

        _customerItems.Add(new CustomerReturnItem
        {
            ItemId = item.ItemId,
            ItemName = item.ItemName,
            SoldQty = item.Quantity,
            AlreadyReturned = returnedQty,
            ReturnQty = item.Quantity - returnedQty,
            Selected = true, // ✅ AUTO CHECK
            MarkedPrice = item.CostPrice,   // ✅ FIX
            SellingPrice = item.Price 
        });
    }

    CustomerItemsGrid.ItemsSource = null;
    CustomerItemsGrid.ItemsSource = _customerItems;

    // optional info
    CustomerSaleInfo.Text = $"Receipt: {receipt} | Items: {_customerItems.Count}";
}


    // ---------------- Supplier Return ----------------
private void LoadSuppliers()
{
    SupplierSearchCombo.ItemsSource = ReturnsRepository.GetSuppliers();
}
private void SupplierSearchCombo_TextChanged(object sender, TextChangedEventArgs e)
{
    if (SupplierSearchCombo.IsDropDownOpen == false)
        SupplierSearchCombo.IsDropDownOpen = true;

    string text = SupplierSearchCombo.Text;

    SupplierSearchCombo.ItemsSource = ReturnsRepository.GetSuppliers(text);
}
private void SupplierProcessReturn_Click(object sender, RoutedEventArgs e)
{
    var selectedItems = _returnItems
        .Where(x => x.Selected && x.ReturnQty > 0)
        .ToList();

if (!_returnItems.Any(x => x.Selected && x.ReturnQty > 0))
{
    MessageBox.Show("Select item AND enter return quantity.");
    return;
}

    string supplier = SupplierSearchCombo.Text?.Trim();

    if (string.IsNullOrWhiteSpace(supplier))
    {
        MessageBox.Show("Please enter or select a supplier.");
        return;
    }

    using var connection = DbService.GetConnection();
    connection.Open();

    using var transaction = connection.BeginTransaction();

    try
    {
        foreach (var item in selectedItems)
        {
            // ❌ PREVENT OVER-RETURN
            if (item.ReturnQty > item.Stock)
            {
                MessageBox.Show($"Return qty exceeds stock for {item.ItemName}");
                transaction.Rollback();
                return;
            }

            decimal totalPrice = item.ReturnQty * item.UnitPrice;

            // ✅ INSERT RETURN
            ReturnsRepository.InsertReturn(
                receiptNumber: null, // supplier return → no receipt
                itemId: item.Id,
                itemName: item.ItemName,
                alias: null,
                quantity: item.ReturnQty,
                unitPrice: item.MarkedPrice,
                price: item.SellingPrice,
                reason: item.Reason,
                returnType: "Supplier Return",
                performedBy: SessionContext.CurrentUserName,
                branchId: SessionContext.EffectiveBranchId,
                saleDate: DateTime.Now,
                refundMethod: null,
                restock: false, // supplier return NEVER restocks
                supplierName: supplier
            );

            // ❌ DEDUCT STOCK
            ItemRepository.DeductStock(
                connection,
                transaction,
                item.Id,
                item.ReturnQty,
                $"Supplier Return - {supplier}",
                SessionContext.CurrentUserName,
                ActivityActions.SUPPLIER_RETURN
            );
        }

        transaction.Commit();

        MessageBox.Show("Supplier return recorded successfully.");

        // ✅ RESET UI
        _returnItems.Clear();
        SupplierItemsGrid.ItemsSource = null;
        SupplierItemSearchBox.Clear();
        SupplierSearchCombo.Text = "";

    }
    catch (Exception ex)
    {
        transaction.Rollback();
        MessageBox.Show($"Error: {ex.Message}");
    }
}
    private void SupplierCancelReturn_Click(object sender, RoutedEventArgs e)
    {   _returnItems.Clear();
        SupplierItemsGrid.ItemsSource = null;
        SupplierItemDetails.Text = "";
        SupplierItemSearchBox.Clear();
    }
    // When user types in the search box
private void SupplierItemSearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    string searchTerm = SupplierItemSearchBox.Text.Trim();

    if (string.IsNullOrEmpty(searchTerm))
    {
        SupplierItemSuggestions.Visibility = Visibility.Collapsed;
        SupplierItemSuggestions.ItemsSource = null;
        return;
    }

    var items = ReturnsRepository.SearchItems(searchTerm);

    SupplierItemSuggestions.ItemsSource = items;
    SupplierItemSuggestions.DisplayMemberPath = "Name";

    SupplierItemSuggestions.Visibility = items.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;
}
// When user clicks/selects an item from suggestions
private ObservableCollection<SupplierReturnItem> _returnItems = new();

private void SupplierItemSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (SupplierItemSuggestions.SelectedItem is ItemLookup selectedItem)
    {
        // prevent duplicates
        if (_returnItems.Any(i => i.Id == selectedItem.Id))
            return;

        _returnItems.Add(new SupplierReturnItem
        {
            Id = selectedItem.Id,
            ItemName = selectedItem.Name,
            Stock = selectedItem.Quantity,
            UnitPrice = selectedItem.MarkedPrice,
            Price = selectedItem.SellingPrice,
            ReturnQty = 1,
            Reason = "",
            Selected = true
            
        });

        // clear UI
        SupplierItemSuggestions.SelectedItem = null;
        SupplierItemSearchBox.Clear();
        SupplierItemSuggestions.Visibility = Visibility.Collapsed;
    }
} }
}
