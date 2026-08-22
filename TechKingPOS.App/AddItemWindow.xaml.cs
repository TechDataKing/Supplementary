using System;
using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ExcelDataReader;
using System.Data;
using System.Windows.Controls;

using TechKingPOS.App.Services;
using TechKingPOS.App.Data;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services.Import;
using TechKingPOS.App.Services.AI;


namespace TechKingPOS.App
{     
    public partial class AddItemWindow : Window 
    {   
        // 🔹 Barcode context (optional)
        public string? PendingBarcode { get; set; }
        public bool IsBarcodeMapping { get; set;}
        private bool _isProcessingScan;
        private DateTime _lastScan = DateTime.MinValue;
        private List<ItemLookup> _allItems = new();
        private bool _suppressTextChange;
        private string? _currentBarcodeSession;
        private readonly ImageInputService _imageInputService = new ImageInputService();
        private readonly PipelineService _pipelineService;

        public AddItemWindow(string? barcode = null)
        {   
            InitializeComponent();
            
            Loaded += (_, _) => BarcodeInput.Focus();
            Activated += (_, _) => BarcodeInput.Focus();
            MouseDown += (_, _) => BarcodeInput.Focus();
            Loaded += (_, _) =>
            {
                Keyboard.Focus(BarcodeInput);
            };
            BarcodeInput.Focusable = true;
            BarcodeInput.IsTabStop = true;

            PendingBarcode = barcode;
            IsBarcodeMapping = !string.IsNullOrWhiteSpace(barcode);

            UnitTypeBox.SelectedIndex= 0;
            ItemSuggestions.SelectionChanged += ItemSuggestions_SelectionChanged;
            _allItems = ItemRepository.GetAllItems();

            NameBox.TextChanged += ItemSearch_TextChanged;
            AliasBox.TextChanged += ItemSearch_TextChanged;
            _pipelineService = new PipelineService(
                new OCRService(),
                new TextCleaner(),
                new ImageClassifier(),
                new ImportRuleEngine()
            );
            LoggerService.Info(
                "🧾",
                "UI",
                "Add Item window opened"
            );
        }

// private void OpenBarcodeMapping( string barcode)
// {
// var win = new AddItemWindow(barcode);
// win.ShowDialog();
// BarcodeInput.Focus();
// }

private void BarcodeInput_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Key != Key.Enter)
        return;

    string barcode = BarcodeInput.Text.Trim();

    if (string.IsNullOrWhiteSpace(barcode))
        return;

    ProcessBarcode(barcode); // ✅ reuse same logic
}

private void PopulateItem(ItemModel item)
{
    _suppressTextChange = true;

    NameBox.Text = item.Name;
    AliasBox.Text = item.Alias;
    MarkedPriceBox.Text = item.MarkedPrice.ToString("0.00");
    SellingPriceBox.Text = item.SellingPrice.ToString("0.00");
    WholesalePriceBox.Text = item.WholesalePrice?.ToString("0.00");

    UnitTypeBox.SelectedItem =
        UnitTypeBox.Items
        .Cast<ComboBoxItem>()
        .FirstOrDefault(x => x.Content.ToString() == item.UnitType);

    // ❌ IMPORTANT: do NOT touch these
    QuantityBox.Clear();
    UnitValueBox.Clear();

    _suppressTextChange = false;
}
private void AddQuantityToExistingItem(int itemId, int unitsPerScan)
{
    // Simple input box for quantity
    var input = Microsoft.VisualBasic.Interaction.InputBox(
        "Enter quantity to add:",
        "Add Stock",
        "1"
    );

    if (!int.TryParse(input, out int qty) || qty <= 0)
    {
        MessageBox.Show("Invalid quantity entered.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    // Add stock
    ItemRepository.AddStock(
        itemId,
        qty * unitsPerScan,
        0, // prices untouched
        0,
        0,
        ActivityActions.ADD_ITEM
    );

    MessageBox.Show(
        $"Stock updated successfully (+{qty * unitsPerScan})",
        "Success",
        MessageBoxButton.OK,
        MessageBoxImage.Information
    );
}

 private bool ValidateForm(out string errorMessage)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(NameBox.Text))
        errors.Add("• Item Name is required");

    if (!decimal.TryParse(MarkedPriceBox.Text, out _))
        errors.Add("• Marked Price must be valid");

    if (!decimal.TryParse(SellingPriceBox.Text, out _))
        errors.Add("• Selling Price must be valid");

    if (!decimal.TryParse(QuantityBox.Text, out var qty) || qty <= 0)
        errors.Add("• Quantity must be a whole number");

    if (UnitTypeBox.SelectedItem is not ComboBoxItem unitItem)
    {
        errors.Add("• Unit Type is required");
        errorMessage = string.Join(Environment.NewLine, errors);
        return false;
    }


        string unit = unitItem.Content.ToString();


        if (unit == "pieces")
        {
            // OPTIONAL
            if (!string.IsNullOrWhiteSpace(UnitValueBox.Text))
            {
                if (!decimal.TryParse(UnitValueBox.Text, out var uv) || uv <= 0)
                    errors.Add("• Unit Value must be greater than zero if provided");
            }
        }
        else
        {
            // REQUIRED
            if (!decimal.TryParse(UnitValueBox.Text, out var uv) || uv <= 0)
                errors.Add("• Unit Value is required for this unit");
        }


    errorMessage = string.Join(Environment.NewLine, errors);
    return errors.Count == 0;
}

private void Save_Click(object sender, RoutedEventArgs e)
{
    if (!ValidateForm(out var errorMessage))
    {
        MessageBox.Show(errorMessage, "Validation Error");
        return;
    }

    string name = NameBox.Text.Trim();
    string alias = AliasBox.Text.Trim();
    decimal marked = decimal.Parse(MarkedPriceBox.Text);
    decimal selling = decimal.Parse(SellingPriceBox.Text);
    decimal? wholesale = string.IsNullOrWhiteSpace(WholesalePriceBox.Text)
    ? null
    : decimal.Parse(WholesalePriceBox.Text);
    decimal inputQty = decimal.Parse(QuantityBox.Text);
    string inputUnit = ((ComboBoxItem)UnitTypeBox.SelectedItem).Content.ToString();
    decimal? unitValue = string.IsNullOrWhiteSpace(UnitValueBox.Text) ? null : decimal.Parse(UnitValueBox.Text);
    string? supplier = string.IsNullOrWhiteSpace(SupplierBox.Text) ? null : SupplierBox.Text.Trim();

    // Convert to base unit
    if (!UnitConverter.TryToBase(inputUnit, inputQty, unitValue ?? 1m, out var baseUnit, out var baseQty, out var unitError))
    {
        MessageBox.Show(unitError, "Invalid Unit", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    try
    {
        var existing = ItemRepository.GetByNameOrAlias(name, alias);
        int finalItemId;

        if (existing != null)
        {
            if (existing.UnitType != baseUnit)
            {
                MessageBox.Show(
                    $"Cannot update '{existing.Name}'.\n\nExisting unit: {existing.UnitType}\nEntered unit: {baseUnit}",
                    "Unit Mismatch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // update stock
            ItemRepository.AddStock(existing.Id, baseQty, marked, selling,wholesale, ActivityActions.ADD_ITEM);
            finalItemId = existing.Id;
            MessageBox.Show($"Item '{name}' updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            // insert new item and get ID directly
            finalItemId = ItemRepository.InsertItem(name, alias, marked, selling,wholesale, baseQty, baseUnit, unitValue, supplier);
            MessageBox.Show($"Item '{name}' added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // barcode mapping
        string barcode = BarcodeInput.Text.Trim();

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var existingMapping = ItemBarcodeRepository.GetByBarcode(barcode);

            if (existingMapping != null)
            {
                if (existingMapping.ItemId != finalItemId)
                {
                    var result = MessageBox.Show(
                        "This barcode is already assigned to another item.\n\nDo you want to reassign it?",
                        "Barcode Exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        string msg;
                        ItemBarcodeRepository.Update(barcode, finalItemId, 1, out msg);

                        MessageBox.Show(msg, "Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Barcode not changed.", "Cancelled");
                    }
                }
                else
                {
                    MessageBox.Show("This barcode is already linked to this item.", "Info");
                }
            }
            else
            {
                string msg;
                var ok = ItemBarcodeRepository.Insert(barcode, finalItemId, 1, out msg);

                if (!ok)
                    MessageBox.Show(msg, "Barcode Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        Clear_Click(null, null);
        LoggerService.Info("✅", "UI", $"Item '{name}' saved/updated successfully");
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    _allItems = ItemRepository.GetAllItems();
    BarcodeInput.Focus();
}
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            NameBox.Clear();
            AliasBox.Clear();
            MarkedPriceBox.Clear();
            SellingPriceBox.Clear();
            WholesalePriceBox.Clear();
            QuantityBox.Clear();
            UnitValueBox.Clear();
            UnitTypeBox.SelectedIndex = 0;
            SupplierBox.Clear();

            // ✅ HIDE UNIT VALUE AGAIN
            UnitValuePanel.Visibility = Visibility.Collapsed;
            BarcodeInput.Clear();
            BarcodeInput.Focus();

            //UnitBox.Clear();

            LoggerService.Info(
                "🧹",
                "UI",
                "Add Item form cleared"
            );
                        }

                        private void Upload_Click(object sender, RoutedEventArgs e)
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter =
                            "All Supported|*.xlsx;*.csv|" +
                            "Excel Files|*.xlsx|" +
                            "CSV Files|*.csv"
                    };

                    if (dialog.ShowDialog() != true)
                        return;

                    string path = dialog.FileName;
                    string ext = Path.GetExtension(path).ToLower();

                    List<ItemImportResult> rows = ext switch
                    {
                        ".csv" => ImportPreviewWindow.FromCsv(path),
                        ".xlsx" => ImportPreviewWindow.FromExcel(path),
                        _ => new List<ItemImportResult>()
                    };

                    if (rows.Count == 0)
                    {
                        MessageBox.Show("No data found in file");
                        return;
                    }

                    var preview = new ImportPreviewWindow(rows);
                   preview.ShowDialog();
                   _allItems = ItemRepository.GetAllItems();
                   BarcodeInput.Focus();    

                }


        private List<ItemModel> ImportFromCsv(string path)
        {
            var items = new List<ItemModel>();

            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                var parts = line.Split(',');

                if (parts.Length < 6)
                    continue;

                items.Add(new ItemModel
                {
                    Name = parts[0].Trim(),
                    Alias = parts[1].Trim(),
                    MarkedPrice = decimal.Parse(parts[2]),
                    SellingPrice = decimal.Parse(parts[3]),
                    WholesalePrice = decimal.Parse(parts[4]),
                    Quantity = decimal.Parse(parts[5]),
                    UnitType = parts[6].Trim(),
                    Supplier = parts.Length > 7 ? parts[7].Trim() : null
                });
            }

            return items;
            
        }

        private List<ItemModel> ImportFromExcel(string path)
        {
            var items = new List<ItemModel>();

            // Required for ExcelDataReader
            System.Text.Encoding.RegisterProvider(
                System.Text.CodePagesEncodingProvider.Instance);

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var dataSet = reader.AsDataSet();
            var table = dataSet.Tables[0];

            for (int row = 1; row < table.Rows.Count; row++) // skip header
            {
                var r = table.Rows[row];
                items.Add(new ItemModel
                {
                    Name = r.Table.Columns.Count > 0 ? r[0]?.ToString() : null,
                    Alias = r.Table.Columns.Count > 1 ? r[1]?.ToString() : null,
                    MarkedPrice = r.Table.Columns.Count > 2 ? decimal.Parse(r[2].ToString()) : 0,
                    SellingPrice = r.Table.Columns.Count > 3 ? decimal.Parse(r[3].ToString()) : 0,
                    WholesalePrice = r.Table.Columns.Count > 4 ? decimal.Parse(r[4].ToString()) : 0,
                    Quantity = r.Table.Columns.Count > 5 ? int.Parse(r[4].ToString()) : 0,
                    UnitType = r.Table.Columns.Count > 6 ? r[5]?.ToString() : null,
                    Supplier = r.Table.Columns.Count > 7 ? r[6]?.ToString() : null,
                });
            }

            return items;
        }

private void ItemSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
    if (_suppressTextChange)
        return;

    string nameText = NameBox.Text.Trim().ToLower();
    string aliasText = AliasBox.Text.Trim().ToLower();

    if (string.IsNullOrEmpty(nameText) && string.IsNullOrEmpty(aliasText))
    {
        ItemSuggestions.Visibility = Visibility.Collapsed;
        return;
    }

    var matches = _allItems
        .Where(i =>
            (!string.IsNullOrEmpty(nameText) &&
             i.Name.ToLower().Contains(nameText)) ||
            (!string.IsNullOrEmpty(aliasText) &&
             !string.IsNullOrEmpty(i.Alias) &&
             i.Alias.ToLower().Contains(aliasText)))
        .ToList();

    if (matches.Count == 0)
    {
        ItemSuggestions.Visibility = Visibility.Collapsed;
        return;
    }

    ItemSuggestions.ItemsSource = matches;
    ItemSuggestions.Visibility = Visibility.Visible;
}

private void ItemSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (ItemSuggestions.SelectedItem is not ItemLookup item)
        return;

    _suppressTextChange = true;

    NameBox.Text = item.Name;
    AliasBox.Text = item.Alias;
    MarkedPriceBox.Text = item.MarkedPrice.ToString("0.00");
    SellingPriceBox.Text = item.SellingPrice.ToString("0.00");
    WholesalePriceBox.Text = item.WholesalePrice?.ToString("0.00");
    UnitTypeBox.SelectedItem =
    UnitTypeBox.Items
    .Cast<ComboBoxItem>()
    .FirstOrDefault(x => x.Content.ToString() == item.UnitType);

    QuantityBox.Clear(); // ❌ DO NOT AUTO-FILL

    _suppressTextChange = false;

    ItemSuggestions.Visibility = Visibility.Collapsed;
    ItemSuggestions.SelectedItem = null;
}
private void UnitType_Changed(object sender, SelectionChangedEventArgs e)
{
    if (UnitTypeBox.SelectedItem is not ComboBoxItem item)
        return;

    string unit = item.Content.ToString();

    UnitValuePanel.Visibility = Visibility.Visible;
}

private void ClearFormForNewBarcode(string barcode)
{
    _isProcessingScan = true;

    try
    {
        Clear_Click(null, null);

        PendingBarcode = barcode;
        IsBarcodeMapping = true;

        _currentBarcodeSession = null; // reset session first

        BarcodeInput.Text = barcode;

        BarcodeInput.CaretIndex = barcode.Length;
    }
    finally
    {
        _isProcessingScan = false;
    }

    LoggerService.Info("🔄", "UI", $"Barcode reset: {barcode}");
}
private void BarcodeSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (BarcodeSuggestions.SelectedItem is not string barcode)
        return;

    Dispatcher.BeginInvoke(new Action(() =>
    {
        BarcodeSuggestions.Visibility = Visibility.Collapsed;

        BarcodeInput.Text = barcode;
        BarcodeInput.CaretIndex = barcode.Length;

        ProcessBarcode(barcode); // ✅ FIXED
    }));
}
private System.Windows.Threading.DispatcherTimer _scanTimer = new();
private string _scanBuffer = "";

private void BarcodeInput_TextChanged(object sender, TextChangedEventArgs e)
{
    if (_isProcessingScan) // 🔥 prevent recursion
        return;
    

    _scanBuffer = BarcodeInput.Text;

    if (string.IsNullOrWhiteSpace(_scanBuffer))
    {
        _scanTimer.Stop();
        return;
    }

    _scanTimer.Stop();
    _scanTimer.Interval = TimeSpan.FromMilliseconds(150);
    _scanTimer.Tick -= ScanTimer_Tick;
    _scanTimer.Tick += ScanTimer_Tick;
    _scanTimer.Start();
}

private void ScanTimer_Tick(object? sender, EventArgs e)
{
    _scanTimer.Stop();

    string barcode = _scanBuffer?.Trim();

    if (string.IsNullOrWhiteSpace(barcode) || barcode.Length < 5)
        return;

    _isProcessingScan = true;

    try
    {
        ProcessBarcode(barcode);
    }
    finally
    {
        _isProcessingScan = false;
    }
}
private void ProcessBarcode(string barcode)
{
    if (string.IsNullOrWhiteSpace(barcode))
        return;

    // 🔥 Always treat new scan as fresh
    _currentBarcodeSession = barcode;

    var mapping = ItemBarcodeRepository.GetByBarcode(barcode);

    if (mapping == null)
    {
        ClearFormForNewBarcode(barcode);
        return;
    }

    var item = ItemRepository.GetById(mapping.ItemId);

    if (item == null)
    {
        ClearFormForNewBarcode(barcode);
        return;
    }

    // ✅ THIS is what you said is not happening
    PopulateItem(item);
}
private async void ImportImage_Click(object sender, RoutedEventArgs e)
{
    var result = await _imageInputService.PickFromFileAsync();

    if (result != null)
    {
        await _pipelineService.ProcessAsync(result.ImageBytes);
    }
}
    }
}
