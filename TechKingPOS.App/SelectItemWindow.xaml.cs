using System.Windows;
using TechKingPOS.App.Models;
using System.Windows.Controls;
using TechKingPOS.App.Services;


namespace TechKingPOS.App
{
    public partial class SelectItemWindow : Window
    {
        public SaleItem Item { get; private set; }
        public List<RepackRuleModel> RepackRules { get; }
        public bool IsEditMode { get; set; }


        public SelectItemWindow(SaleItem item, List<RepackRuleModel>? rules = null)
        {
            InitializeComponent();

            Item = item;
            RepackRules = rules ?? new List<RepackRuleModel>();

            DataContext = Item;

            if (RepackRules.Any())
                ShowRepackUI();
        }

        private void ShowRepackUI()
        {
            RepackPanel.Visibility = Visibility.Visible;

            RepackCombo.ItemsSource = RepackRules.Where(r => r.IsActive).ToList();
            RepackCombo.SelectedIndex = 0; // auto-select first rule
        }
private void RepackCombo_Changed(object sender, SelectionChangedEventArgs e)
{
    if (RepackCombo.SelectedItem is RepackRuleModel rule)
    {
        Item.UnitValue = rule.UnitValue;      // 0.25
        Item.UnitType = rule.UnitType;        // kg
        Item.Quantity = 1m;                   // default pack count
        Item.Price = rule.SellingPrice;       // price per pack
        Item.IsRepack = true;
    }
}





        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Item.Quantity = 0;
            Item.Price = 0;
            DialogResult = false;
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
            MessageBox.Show(
                $"SELLING PRICE TOO LOW\n\n" +
                $"Item: {item.Name}\n\n" +
                $"Selling Price: {item.Price:0.00}\n" +
                $"Minimum (Unit): {item.MarkedPrice:0.00}\n",
                "Price Protection",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
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
private void Add_Click(object sender, RoutedEventArgs e)
{
    if (Item.Quantity <= 0)
    {
        MessageBox.Show(
            "Quantity must be greater than zero.");
        return;
    }

    if (Item.Price <= 0)
    {
        MessageBox.Show(
            "Price must be greater than zero.");
        return;
    }

    // Stock validation
    if (!SettingsCache.Current.AllowNegativeStock)
    {
        decimal available = Item.IsRepack
            ? Item.RepackAvailableQty
            : Item.AvailableQuantity;

        if (Item.Quantity > available)
        {
            MessageBox.Show(
                $"Insufficient stock.\n\n" +
                $"Available: {available}\n" +
                $"Requested: {Item.Quantity}",
                "Stock Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }
    }

    // Price validation
    if (!Item.IsRepack && Item.Price < Item.MarkedPrice)
    {
        MessageBox.Show(
            $"Selling price too low.\n\n" +
            $"Entered: {Item.Price:0.00}\n" +
            $"Minimum: {Item.MarkedPrice:0.00}",
            "Price Protection",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    DialogResult = true;
}

    }
}
/*  */