using System;
using System.Collections.Generic;
using System.Linq;

using System.Windows;                // 🔥 Window, RoutedEventArgs
using System.Windows.Controls;       // 🔥 TextBox, ListBox
using System.Windows.Media;          // 🔥 Brushes

using TechKingPOS.App.Models;
using TechKingPOS.App.Data;
using TechKingPOS.App.Security;

namespace TechKingPOS.App{
public partial class MoveToRepackWindow : Window
{
    private List<ItemLookup> _items = new();
    private List<RepackStockModel> _repack = new();
    private ItemLookup _selectedItem;

    public MoveToRepackWindow()
    {
        InitializeComponent();
        LoadItems();
        LoadRepack();
        UnitCombo.SelectedIndex= 0;
    }

    private void LoadItems()
    {
        _items = ItemRepository.GetAllItems();
        ItemsList.ItemsSource = _items;
    }

    private void LoadRepack()
    {
        _repack = RepackStockRepository.GetActive();
        RepackList.ItemsSource = _repack;
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedItem = ItemsList.SelectedItem as ItemLookup;
        if (_selectedItem == null) return;

        SelectedItemText.Text = _selectedItem.Name;
        AvailableText.Text = $"{_selectedItem.Quantity} {_selectedItem.UnitType}";
        SourceUnitText.Text = _selectedItem.UnitType;
    }

    private void MoveToRepack_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        if (!decimal.TryParse(MoveQtyBox.Text, out var moveQty) || moveQty <= 0)
        {
            MessageBox.Show("Invalid move qty");
            return;
        }

        if (!decimal.TryParse(ConversionQtyBox.Text, out var conversionQty) || conversionQty <= 0)
{
    MessageBox.Show("Invalid conversion qty");
    return;
}

var unit = (UnitCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

decimal totalQty = moveQty * conversionQty;
decimal costPerUnit = _selectedItem.MarkedPrice / conversionQty;

//var unit = (UnitCombo.SelectedItem as ComboBoxItem)?.Content.ToString();

        //decimal costPerUnit = _selectedItem.MarkedPrice / totalQty;

        var repack = new RepackStockModel
        {
            ItemId = _selectedItem.Id,
            ItemName = _selectedItem.Name,

            SourceQuantity = moveQty,
            SourceUnit = _selectedItem.UnitType,

            BaseUnit = unit,
            TotalQuantity = totalQty,
            RemainingQuantity = totalQty,

            SourceCost = _selectedItem.MarkedPrice,
            CostPerUnit = costPerUnit
        };

using var con = DbService.GetConnection();
con.Open();

using var tx = con.BeginTransaction();

try
{
    // ✅ SAVE TO REPACK
    RepackStockRepository.Add(con, tx, repack);

    // ✅ DEDUCT FROM ITEMS (🔥 HERE)
    RepackStockRepository.DeductStockOnly(
        con,
        tx,
        _selectedItem.Id,
        moveQty,
        "MOVE TO REPACK",
        ActivityActions.REPACK_OUT,
        SessionContext.CurrentUserName
    );

    tx.Commit();
}
catch (Exception ex)
{
    tx.Rollback();
    MessageBox.Show(ex.Message);
}


        LoadRepack();
        ClearForm();
    }


    private void RemoveRepack_Click(object sender, RoutedEventArgs e)
    {
        if (RepackList.SelectedItem is not RepackStockModel r) return;

        using var con = DbService.GetConnection();
        con.Open();

        using var tx = con.BeginTransaction();

        try
        {
            // ✅ ADD BACK TO ITEMS (🔥 HERE)
            RepackStockRepository.AddStockOnly(
                con,
                tx,
                r.ItemId,
                r.RemainingQuantity
            );

            // ✅ REMOVE FROM REPACK
            RepackStockRepository.Remove(con, tx, r.Id);

            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            MessageBox.Show(ex.Message);
        }
        ClearForm();
        LoadRepack();
    }

    private void ClearForm()
{
    // RESET SELECTED ITEM
    _selectedItem = null;
    ItemsList.SelectedItem = null;

    // TEXT FIELDS
    SelectedItemText.Text = "";
    AvailableText.Text = "";
    SourceUnitText.Text = "-";

    MoveQtyBox.Text = "";
    ConversionQtyBox.Text = "";

    // COMBOBOX (reset back to first)
    UnitCombo.SelectedIndex = 0;

    // AUTO-RESULT FIELDS
    TotalQtyText.Text = "Total Quantity: -";
    CostText.Text = "Cost per unit: -";

    // SEARCH BOX (optional)
    // SearchBox.Text = "Search item...";
    // SearchBox.Foreground = Brushes.Gray;
}

private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    if (SearchBox.Text == "Search item...") return;

    var text = SearchBox.Text.ToLower();

    ItemsList.ItemsSource = _items
        .Where(i => i.Name.ToLower().Contains(text))
        .ToList();
}

private void RepackSearchBox_TextChanged(object sender, TextChangedEventArgs e)
{
    if (RepackSearchBox.Text == "Search repack...") return;

    var text = RepackSearchBox.Text.ToLower();

    RepackList.ItemsSource = _repack
        .Where(i => i.ItemName.ToLower().Contains(text))
        .ToList();
}
    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
{
    if (SearchBox.Text == "Search item...")
    {
        SearchBox.Text = "";
        SearchBox.Foreground = Brushes.Black;
    }
}

private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(SearchBox.Text))
    {
        SearchBox.Text = "Search item...";
        SearchBox.Foreground = Brushes.Gray;
    }
}
private void RepackSearchBox_GotFocus(object sender, RoutedEventArgs e)
{
    if (RepackSearchBox.Text == "Search repack...")
    {
        RepackSearchBox.Text = "";
        RepackSearchBox.Foreground = Brushes.Black;
    }
}

private void RepackSearchBox_LostFocus(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(RepackSearchBox.Text))
    {
        RepackSearchBox.Text = "Search repack...";
        RepackSearchBox.Foreground = Brushes.Gray;
    }
}
private void Recalculate(object sender, EventArgs e)
{
    if (_selectedItem == null)
        return;

    if (!decimal.TryParse(MoveQtyBox.Text, out var moveQty) || moveQty <= 0)
        return;

    if (!decimal.TryParse(ConversionQtyBox.Text, out var conversionQty) || conversionQty <= 0)
        return;

    var unit = (UnitCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
    if (string.IsNullOrWhiteSpace(unit))
        return;

    // ✅ TOTAL = moveQty × conversion
    decimal totalQty = moveQty * conversionQty;

    // ✅ COST (using MarkedPrice as you said)
    decimal costPerUnit = _selectedItem.MarkedPrice / conversionQty;

    // UI OUTPUT
    TotalQtyText.Text = $"Total Quantity: {totalQty} {unit}";
    CostText.Text = $"Cost per {unit}: {costPerUnit:0.00}";
}
}
}