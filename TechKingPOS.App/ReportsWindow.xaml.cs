
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using System.Printing;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.Windows.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using TechKingPOS.App.Models;
using TechKingPOS.App.Data;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Infrastructure;
using ActivityModel = TechKingPOS.App.Models.Activity;


namespace TechKingPOS.App
{
    public partial class ReportsWindow : RefreshOnFocusWindow
    {   
        private bool _isLoaded = false;
        private bool _isLoading = false;
        private TabItem? _lastAllowedTab;
        private List<ActivityModel> _allMovements = new();
        private List<MovementView> _viewData = new();

         private List<CreditView> _allCredits = new();

        public ReportsWindow()
        {
         InitializeComponent();
         ExpenseCategoryCombo.SelectedIndex= 0;
        }

        private int _editingExpenseId = 0;
        
private enum ReportType
{
    Sales,
    Inventory,
    Credit,
    Expenses,
    StockMovement

}

private ReportType GetActiveReport()
{
    return (ReportType)ReportsTabControl.SelectedIndex;
}


public void InitializeReport()
{

    FromDatePicker.SelectedDate = DateTime.Today;
    ToDatePicker.SelectedDate = DateTime.Today;

    LoadReport();       // TODAY SALES
    LoadOutOfStock();   // INVENTORY DEFAULT
    LoadCredits();      // CREDIT
    LoadExpenses();    // EXPENSES
    ExpenseCategoryCombo.SelectedIndex= 0;
    ExpensePaymentCombo.SelectedIndex= 0;

}

protected override void Refresh()
        {
            if (!_isLoaded)
                return;

            
            LoadToday();          // TODAY SALES
            LoadReport();     // TODAY SALES
            LoadOutOfStock();     // INVENTORY DEFAULT
            LoadCredits();        // CREDIT
            LoadExpenses();      // EXPENSES
            LoadGoodStock();      // REFRESH INVENTORY FILTER
            LoadRunningLow();    // REFRESH INVENTORY FILTER
            
        }

        private Window GetMainWindow()
        {
            return Application.Current.MainWindow;
        }
private void ExportPdf_Click(object sender, RoutedEventArgs e)
{
    switch (GetActiveReport())
    {
        case ReportType.Sales:
            ExportSalesPdf();
            break;

        case ReportType.Inventory:
            ExportInventoryPdf();
            break;

        case ReportType.Credit:
            ExportCreditPdf();
            break;

        case ReportType.Expenses:
            ExportExpensesPdf();
            break;
        case ReportType.StockMovement:
            ExportStockMovementPdf();
            break;
        
    }
}


private void ExportExcel_Click(object sender, RoutedEventArgs e)
{
    switch (GetActiveReport())
    {
        case ReportType.Sales:
            ExportSalesExcel();
            break;

        case ReportType.Inventory:
            ExportInventoryExcel();
            break;

        case ReportType.Credit:
            ExportCreditExcel();
            break;

        case ReportType.Expenses:
            ExportExpensesExcel();
            break;
        case ReportType.StockMovement:
            ExportStockMovementExcel();
            break;
    }
}


private void PrintReport_Click(object sender, RoutedEventArgs e)
{
    switch (GetActiveReport())
    {
        case ReportType.Sales:
            PrintSales();
            break;

        case ReportType.Inventory:
            PrintInventory();
            break;

        case ReportType.Credit:
            PrintCredits();
            break;

        case ReportType.Expenses:
            PrintExpenses();
            break;
        case ReportType.StockMovement:
            PrintStockMovement();
            break;
        
    }
}

// ================= WINDOW LOADED =================
private void ReportsWindow_Loaded(object sender, RoutedEventArgs e)
{   ReportsTabControl.SelectedIndex = -1;

    // 🔒 Block all logic during init
    _isLoaded = false;
    _isLoading = true;

    FromDatePicker.SelectedDate = DateTime.Today;
    ToDatePicker.SelectedDate = DateTime.Today;

    TabItem? firstAllowedTab = null;

    foreach (TabItem tab in ReportsTabControl.Items)
    {
        string? key = tab.Tag as string;

        bool allowed =
            string.IsNullOrWhiteSpace(key) ||
            PermissionService.Can(
                SessionService.CurrentUser.Id,
                SessionService.CurrentUser.Role,
                key
            );

        // Tabs stay enabled so clicks can be intercepted
        tab.IsEnabled = true;

        if (allowed && firstAllowedTab == null)
            firstAllowedTab = tab;
    }

    // 🔒 Force initial safe tab BEFORE any rendering
    if (firstAllowedTab != null)
    {
        _lastAllowedTab = firstAllowedTab;
        ReportsTabControl.SelectedItem = firstAllowedTab;
    }

    // 🔁 Defer heavy loading until UI is stable
    Dispatcher.BeginInvoke(() =>
    {
        LoadReport();
        LoadOutOfStock();
        LoadCredits();
        LoadExpenses();

        // ✅ App fully ready
        _isLoaded = true;
        _isLoading = false;
        InventoryFilterChanged(null, null); // trigger initial inventory filter

    }, DispatcherPriority.Background);
}
private void ReportsTabItem_Loaded(object sender, RoutedEventArgs e)
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
        // 🚫 PREVENT CONTENT FROM EVER SHOWING
        tab.Content = null;

        // 🔒 also prevent default selection
        if (ReportsTabControl.SelectedItem == tab)
            ReportsTabControl.SelectedIndex = -1;
    }
}



   private void ReportsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (_isLoading)
        return;

    if (ReportsTabControl.SelectedItem is not TabItem tab)
        return;

    string? permissionKey = tab.Tag as string;

    // Tabs without permission are always allowed
    if (string.IsNullOrWhiteSpace(permissionKey))
    {
        _lastAllowedTab = tab;

            CheckRefresh();
        return;
    }

    bool allowed = PermissionService.Can(
        SessionService.CurrentUser.Id,
        SessionService.CurrentUser.Role,
        permissionKey
    );

    if (!allowed)
    {
        // 🚫 Prevent partial render + crash-safe rollback
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
                ReportsTabControl.SelectedItem = _lastAllowedTab;

            _isLoading = false;

        }, DispatcherPriority.Background);

        return;
    }

    // ✅ Allowed → remember
    _lastAllowedTab = tab;
        CheckRefresh();
}

private void ReportsTabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.OriginalSource is not DependencyObject source)
        return;

    var tab = ItemsControl.ContainerFromElement(
        ReportsTabControl,
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
        // ================= SALES =================

        private void LoadToday()
        {
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;
        }

        private void LoadReport_Click(object sender, RoutedEventArgs e)
        {
            LoadReport();
        }

private void LoadReport()
{
    if (FromDatePicker.SelectedDate == null ||
        ToDatePicker.SelectedDate == null)
    {
        MessageBox.Show("Select date range.");
        return;
    }

    DateTime from = FromDatePicker.SelectedDate.Value.Date;
    DateTime to = ToDatePicker.SelectedDate.Value
        .Date
        .AddDays(1)
        .AddTicks(-1);

    var summary = ReportsRepository.GetSalesSummary(from, to, SessionContext.EffectiveBranchId);

    TotalSalesText.Text = summary.TotalSales.ToString("0.00");
    ProfitText.Text = summary.Profit.ToString("0.00");
    ReceiptCountText.Text = summary.ReceiptCount.ToString();

    // ✅ NEW
    ReturnsText.Text = ReportsRepository
        .GetTotalReturns(from, to)
        .ToString("0.00");

    ExpensesText.Text = ExpenseRepository
        .GetTotalExpenses(from, to)
        .ToString("0.00");

    DamagesText.Text = DamagedRepository
        .GetTotalDamages(from, to)
        .ToString("0.00");

    NetSalesText.Text = ReportsRepository
        .GetNetSales(from, to)
        .ToString("0.00");

    // leave NetProfit empty for now
    NetProfitText.Text = "-";

    SoldItemsGrid.ItemsSource =
        ReportsRepository.GetSoldItems(from, to, SessionContext.EffectiveBranchId);
}
        // ================= INVENTORY =================

private void InventoryFilterChanged(object sender, SelectionChangedEventArgs e)
{
    if (!_isLoaded)
        return;

    if (InventoryFilterCombo.SelectedItem is not ComboBoxItem selectedItem)
        return;

    string tag = selectedItem.Tag?.ToString();

    switch (tag)
    {
        case "OutOfStock":
            LoadOutOfStock();
            break;
        case "RunningLow":
            LoadRunningLow();
            break;
        case "GoodStock":
            LoadGoodStock();
            break;
    }
    InventoryGrid.Items.Refresh(); // reset before loading new data
}


        private void LoadOutOfStock()
        {
            InventoryGrid.ItemsSource =
                InventoryRepository.GetOutOfStock();
        }

        private void LoadRunningLow()
        {
            InventoryGrid.ItemsSource =
                InventoryRepository.GetRunningLow();
        }

        private void LoadGoodStock()
        {
            InventoryGrid.ItemsSource =
                InventoryRepository.GetGoodStock();
        }
        // ================= CREDITS =================  
private void LoadCredits(DateTime? from = null, DateTime? to = null)
{
    _allCredits = ReportsRepository.GetCredits(from, to, SessionContext.EffectiveBranchId);

    CreditGrid.ItemsSource = _allCredits;

    TotalCreditText.Text = _allCredits.Sum(c => c.Balance).ToString("0.00");
}


// ================= CREDIT SEARCH =================
private void CreditSearchChanged(object sender, TextChangedEventArgs e)
{
    string text = CreditSearchTextBox.Text.Trim().ToLower();

    if (string.IsNullOrEmpty(text))
    {
        CreditGrid.ItemsSource = _allCredits;
        return;
    }

    CreditGrid.ItemsSource = _allCredits.Where(c =>
        c.CustomerName.ToLower().Contains(text) ||
        c.Phone.ToLower().Contains(text)
    ).ToList();
}
private void LoadTodayExpenses()
{
    LoadExpenses();
}
private void LoadExpenses()
{
    DateTime from = ExpenseFromDatePicker.SelectedDate?.Date
                    ?? DateTime.Today;

    DateTime to = ExpenseToDatePicker.SelectedDate?.Date
                  ?? DateTime.Today;

    // 🔥 MAKE RANGE INCLUSIVE
    to = to.AddDays(1).AddSeconds(-1);

    var list = ExpenseRepository.GetExpenses(from, to);

    ExpensesGrid.ItemsSource = list;

    TotalExpensesText.Text =
        ExpenseRepository.GetTotalExpenses(from, to)
            .ToString("0.00");
}



private void SaveExpense_Click(object sender, RoutedEventArgs e)
{
    // ===== VALIDATION =====
    if (ExpenseDatePicker.SelectedDate == null ||
        ExpenseCategoryCombo.SelectedItem == null ||
        ExpensePaymentCombo.SelectedItem == null ||
        string.IsNullOrWhiteSpace(ExpenseDescriptionTextBox.Text) ||
        string.IsNullOrWhiteSpace(ExpenseAmountTextBox.Text))
    {
        MessageBox.Show(
            "Please fill in all fields before saving the expense.",
            "Missing Information",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return;
    }

    if (!decimal.TryParse(ExpenseAmountTextBox.Text, out var amount) || amount <= 0)
    {
        MessageBox.Show(
            "Please enter a valid expense amount.",
            "Invalid Amount",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return;
    }

    var category =
        ((ComboBoxItem)ExpenseCategoryCombo.SelectedItem).Content.ToString();

    var payment =
        ((ComboBoxItem)ExpensePaymentCombo.SelectedItem).Content.ToString();

    // ===== SAVE / UPDATE =====
    if (_editingExpenseId == 0)
    {
        ExpenseRepository.AddExpense(
            ExpenseDatePicker.SelectedDate.Value,
            category,
            ExpenseDescriptionTextBox.Text.Trim(),
            amount,
            payment);

        MessageBox.Show(
            "Expense added successfully.",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    else
    {
        ExpenseRepository.UpdateExpense(
            _editingExpenseId,
            ExpenseDatePicker.SelectedDate.Value,
            category,
            ExpenseDescriptionTextBox.Text.Trim(),
            amount,
            payment);

        MessageBox.Show(
            "Expense updated successfully.",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    ClearExpenseForm();
    LoadExpenses();
}

private void EditExpense_Click(object sender, RoutedEventArgs e)
{
    if (ExpensesGrid.SelectedItem is not Expense exp)
        return;

    _editingExpenseId = exp.Id;

    ExpenseFormTitle.Text = "✏ Edit Expense";
    SaveExpenseButton.Content = "Update Expense";

    ExpenseDatePicker.SelectedDate = exp.Date;
    ExpenseDescriptionTextBox.Text = exp.Description;
    ExpenseAmountTextBox.Text = exp.Amount.ToString("0.00");

    SelectComboItem(ExpenseCategoryCombo, exp.Category);
    SelectComboItem(ExpensePaymentCombo, exp.PaymentMethod);
}
private void SelectComboItem(ComboBox combo, string value)
{
    foreach (ComboBoxItem item in combo.Items)
        if (item.Content.ToString() == value)
            combo.SelectedItem = item;
}
private void DeleteExpense_Click(object sender, RoutedEventArgs e)
{
    if (ExpensesGrid.SelectedItem is not Expense exp)
        return;

    var result = MessageBox.Show(
        "Are you sure you want to delete this expense?",
        "Confirm Delete",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result != MessageBoxResult.Yes)
        return;

    ExpenseRepository.DeleteExpense(exp.Id);

    MessageBox.Show(
        "Expense deleted successfully.",
        "Deleted",
        MessageBoxButton.OK,
        MessageBoxImage.Information);

    LoadExpenses();
}

private void ClearExpenseForm()
{
    _editingExpenseId = 0;

    ExpenseFormTitle.Text = "➕ Add Expense";
    SaveExpenseButton.Content = "Save Expense";

    ExpenseDatePicker.SelectedDate = DateTime.Today;
    ExpenseCategoryCombo.SelectedIndex = 0;
    ExpensePaymentCombo.SelectedIndex = 0;
    ExpenseDescriptionTextBox.Clear();
    ExpenseAmountTextBox.Clear();
}

private void PrintCredits()
{
    if (_allCredits.Count == 0)
    {
        MessageBox.Show("No credit records to print.");
        return;
    }

    var doc = new FlowDocument
    {
        PagePadding = new Thickness(40),
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
        FontSize = 12
    };

    // ===== HEADER =====
    doc.Blocks.Add(new Paragraph(new Run("CREDIT ACCOUNTS REPORT"))
    {
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center
    });

    doc.Blocks.Add(new Paragraph(
        new Run($"Printed on: {DateTime.Now:yyyy-MM-dd HH:mm}")));

    doc.Blocks.Add(new Paragraph(
        new Run($"Total Outstanding Credit: {TotalCreditText.Text}"))
    {
        FontWeight = FontWeights.SemiBold
    });
    // ===== DATE RANGE =====
string range =
    CreditFromDatePicker.SelectedDate != null
        ? $"Period: {CreditFromDatePicker.SelectedDate:yyyy-MM-dd} to {CreditToDatePicker.SelectedDate:yyyy-MM-dd}"
        : "Period: ALL";

doc.Blocks.Add(new Paragraph(new Run(range))
{
    FontStyle = FontStyles.Italic,
    Margin = new Thickness(0, 0, 0, 10)
});


    // ===== TABLE =====
    var table = new Table();
    doc.Blocks.Add(table);

    table.Columns.Add(new TableColumn { Width = new GridLength(50) });   // No.
    table.Columns.Add(new TableColumn { Width = new GridLength(200) });  // Customer
    table.Columns.Add(new TableColumn { Width = new GridLength(140) });  // Phone
    table.Columns.Add(new TableColumn { Width = new GridLength(120) });  // Balance
    table.Columns.Add(new TableColumn { Width = new GridLength(120) });  // Last Payment

    var header = new TableRowGroup();
    table.RowGroups.Add(header);

    header.Rows.Add(new TableRow
    {
        FontWeight = FontWeights.Bold,
        Cells =
        {
            new TableCell(new Paragraph(new Run("No."))),
            new TableCell(new Paragraph(new Run("Customer"))),
            new TableCell(new Paragraph(new Run("Phone"))),
            new TableCell(new Paragraph(new Run("Balance"))),
            new TableCell(new Paragraph(new Run("Last Payment")))
        }
    });


    var body = new TableRowGroup();
    table.RowGroups.Add(body);

        int index = 1;

        foreach (var c in _allCredits)
        {
            body.Rows.Add(new TableRow
            {
                Cells =
                {
                    new TableCell(new Paragraph(new Run(index.ToString()))),
                    new TableCell(new Paragraph(new Run(c.CustomerName))),
                    new TableCell(new Paragraph(new Run(c.Phone))),
                    new TableCell(new Paragraph(new Run(c.Balance.ToString("0.00")))),
                    new TableCell(new Paragraph(new Run(c.LastPaymentDate.ToString("yyyy-MM-dd"))))
                }
            });

            index++;
        }


    // ===== PRINT =====
        var preview = new PrintPreviewWindow(doc)
        {
            Owner = GetMainWindow()
        };

        preview.ShowDialog();


}
private void ApplyCreditDateFilter_Click(object sender, RoutedEventArgs e)
{
    DateTime? from = CreditFromDatePicker.SelectedDate;
    DateTime? to = CreditToDatePicker.SelectedDate;

    if (from != null && to != null && from > to)
    {
        MessageBox.Show("From date cannot be later than To date.");
        return;
    }

    LoadCredits(from, to);
}
private void ClearCreditDateFilter_Click(object sender, RoutedEventArgs e)
{
    CreditFromDatePicker.SelectedDate = null;
    CreditToDatePicker.SelectedDate = null;

    LoadCredits(); // reload ALL outstanding credits
}

private void PrintSales_Click(object sender, RoutedEventArgs e)
{   MessageBox.Show("Clicked");
    try
{
    PrintSales();
}
catch (Exception ex)
{
    MessageBox.Show(ex.ToString());
}
}

private void PrintInventory_Click(object sender, RoutedEventArgs e)
{
    PrintInventory();
}

private void PrintExpenses_Click(object sender, RoutedEventArgs e)
{
    PrintExpenses();
}
private void PrintSales()
{
    try
    {
        Debug.WriteLine("=== PrintSales START ===");

        if (SoldItemsGrid.Items.Count == 0)
        {
            MessageBox.Show("No sales records to print.");
            return;
        }

        Debug.WriteLine("Items count: " + SoldItemsGrid.Items.Count);

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12
        };

        Debug.WriteLine("FlowDocument created");

        doc.Blocks.Add(new Paragraph(new Run("SALES REPORT")));

        // DATE RANGE DEBUG
        Debug.WriteLine("FromDate: " + FromDatePicker.SelectedDate);
        Debug.WriteLine("ToDate: " + ToDatePicker.SelectedDate);

        string range =
            FromDatePicker.SelectedDate != null
                ? $"Period: {FromDatePicker.SelectedDate:yyyy-MM-dd} to {ToDatePicker.SelectedDate:yyyy-MM-dd}"
                : "Period: ALL";

        doc.Blocks.Add(new Paragraph(new Run(range)));

        Debug.WriteLine("Summary section...");

        doc.Blocks.Add(new Paragraph(new Run($"Total Sales: {TotalSalesText.Text}")));
        doc.Blocks.Add(new Paragraph(new Run($"Profit: {ProfitText.Text}")));
        doc.Blocks.Add(new Paragraph(new Run($"Receipts: {ReceiptCountText.Text}")));

        Debug.WriteLine("Creating table...");

        var table = new Table();
        doc.Blocks.Add(table);

        // ===== DEFINE COLUMNS =====
        for (int i = 0; i < 9; i++)
            table.Columns.Add(new TableColumn());

        // ===== HEADER =====
        var headerGroup = new TableRowGroup();
        table.RowGroups.Add(headerGroup);

        var headerRow = new TableRow();

        headerRow.Cells.Add(H("#"));
        headerRow.Cells.Add(H("Receipt"));
        headerRow.Cells.Add(H("Item"));
        headerRow.Cells.Add(H("Qty"));
        headerRow.Cells.Add(H("Price"));
        headerRow.Cells.Add(H("Total"));
        headerRow.Cells.Add(H("Payment"));
        headerRow.Cells.Add(H("Cashier"));
        headerRow.Cells.Add(H("Type"));

        headerGroup.Rows.Add(headerRow);

        // ===== BODY =====
        var body = new TableRowGroup();
        table.RowGroups.Add(body);

        int index = 1;

        foreach (var obj in SoldItemsGrid.Items)
        {
            if (obj is not SoldItemReport item)
                continue;

            body.Rows.Add(new TableRow
            {
                Cells =
                {
                    C(index.ToString()),
                    C(item.ReceiptNumber ?? ""),
                    C(item.ItemName ?? ""),
                    C(item.Quantity.ToString()),
                    C(item.Price.ToString("0.00")),
                    C(item.Total.ToString("0.00")),
                    C(item.PaymentMethod ?? ""),
                    C(item.Cashier ?? ""),
                    C(item.SaleType ?? "")
                }
            });

            index++;
        }

        Debug.WriteLine("Rows added: " + index);

        var preview = new PrintPreviewWindow(doc)
        {
            Owner = GetMainWindow()
        };

        Debug.WriteLine("Opening preview...");

        preview.ShowDialog();

        Debug.WriteLine("=== PrintSales END ===");
    }
    catch (Exception ex)
    {
        MessageBox.Show("PRINT ERROR:\n\n" + ex.ToString());
        Debug.WriteLine("ERROR: " + ex);
    }
}
private void LoadInventoryByDate_Click(object sender, RoutedEventArgs e)
{
    DateTime? from = InventoryFromDatePicker.SelectedDate;
    DateTime? to = InventoryToDatePicker.SelectedDate;

    if (from == null || to == null)
    {
        MessageBox.Show("Please select both From and To dates.");
        return;
    }

    // TODO: Replace with your repository call
    // Example:
    // InventoryGrid.ItemsSource = InventoryRepository.GetByDate(from.Value, to.Value);

    MessageBox.Show($"Inventory filtered from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
}
private void LoadExpensesByDate_Click(object sender, RoutedEventArgs e)
{
    DateTime? from = ExpenseFromDatePicker.SelectedDate;
    DateTime? to = ExpenseToDatePicker.SelectedDate;

    if (from == null || to == null)
    {
        MessageBox.Show("Please select both From and To dates.");
        return;
    }

    var list = ExpenseRepository.GetExpenses(from.Value, to.Value);

    ExpensesGrid.ItemsSource = list;

    TotalExpensesText.Text =
        ExpenseRepository.GetTotalExpenses(from.Value, to.Value)
            .ToString("0.00");
}

        private TableCell H(string text)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(6),
                BorderThickness = new Thickness(0.5),
                BorderBrush = Brushes.Black
            };
        }

        private TableCell C(string text)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(6),
                BorderThickness = new Thickness(0.5),
                BorderBrush = Brushes.Black
            };
        }
private void PrintInventory()
{
    var items = InventoryGrid.ItemsSource as IEnumerable<ItemLookup>;
    if (items == null || !items.Any())
    {
        MessageBox.Show("No inventory data to print.");
        return;
    }

    var doc = new FlowDocument
    {
        PagePadding = new Thickness(40),
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12
    };

    doc.Blocks.Add(new Paragraph(new Run("INVENTORY REPORT"))
    {
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center
    });

    var table = new Table();
    doc.Blocks.Add(table);

    table.Columns.Add(new TableColumn { Width = new GridLength(40) });
    table.Columns.Add(new TableColumn { Width = new GridLength(220) });
    table.Columns.Add(new TableColumn { Width = new GridLength(80) });
    table.Columns.Add(new TableColumn { Width = new GridLength(100) });
    table.Columns.Add(new TableColumn { Width = new GridLength(100) });

    var header = new TableRowGroup();
    table.RowGroups.Add(header);

    var headerRow = new TableRow();
    headerRow.Cells.Add(H("#"));
    headerRow.Cells.Add(H("Item"));
    headerRow.Cells.Add(H("Qty"));
    headerRow.Cells.Add(H("Target"));
    headerRow.Cells.Add(H("Deficit"));
    header.Rows.Add(headerRow);

    var body = new TableRowGroup();
    table.RowGroups.Add(body);

    int index = 1;
    foreach (var i in items)
    {
        var row = new TableRow();
        row.Cells.Add(C(index++.ToString()));
        row.Cells.Add(C(i.Name));
        row.Cells.Add(C(i.Quantity.ToString()));
        row.Cells.Add(C(i.TargetQuantity?.ToString() ?? "-"));
        row.Cells.Add(C(i.Deficit.ToString()));
        body.Rows.Add(row);
    }

    new PrintPreviewWindow(doc)
    {
        Owner = GetMainWindow()
    }.ShowDialog();
}
private void PrintExpenses()
{
    var expenses = ExpensesGrid.ItemsSource as IEnumerable<Expense>;
    if (expenses == null || !expenses.Any())
    {
        MessageBox.Show("No expenses data to print.");
        return;
    }

    var doc = new FlowDocument
    {
        PagePadding = new Thickness(40),
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12
    };

    doc.Blocks.Add(new Paragraph(new Run("EXPENSES REPORT"))
    {
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center
    });

    var table = new Table();
    doc.Blocks.Add(table);

    table.Columns.Add(new TableColumn { Width = new GridLength(40) });
    table.Columns.Add(new TableColumn { Width = new GridLength(100) });
    table.Columns.Add(new TableColumn { Width = new GridLength(140) });
    table.Columns.Add(new TableColumn { Width = new GridLength(240) });
    table.Columns.Add(new TableColumn { Width = new GridLength(100) });

    var header = new TableRowGroup();
    table.RowGroups.Add(header);

    var headerRow = new TableRow();
    headerRow.Cells.Add(H("#"));
    headerRow.Cells.Add(H("Date"));
    headerRow.Cells.Add(H("Category"));
    headerRow.Cells.Add(H("Description"));
    headerRow.Cells.Add(H("Amount"));
    header.Rows.Add(headerRow);

    var body = new TableRowGroup();
    table.RowGroups.Add(body);

    int index = 1;
    foreach (var e in expenses)
    {
        var row = new TableRow();
        row.Cells.Add(C(index++.ToString()));
        row.Cells.Add(C(e.Date.ToString("yyyy-MM-dd")));
        row.Cells.Add(C(e.Category));
        row.Cells.Add(C(e.Description));
        row.Cells.Add(C(e.Amount.ToString("0.00")));
        body.Rows.Add(row);
    }

    new PrintPreviewWindow(doc)
    {
        Owner = GetMainWindow()
    }.ShowDialog();
}
private TableCell Cell(string text, bool bold = false)
{
    return new TableCell(new Paragraph(new Run(text))
    {
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
    })
    {
        Padding = new Thickness(4)
    };
}

private FlowDocument CreateBaseDocument(string title)
{
    var doc = new FlowDocument
    {
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12
    };

    doc.Blocks.Add(new Paragraph(new Run(title))
    {
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center
    });

    doc.Blocks.Add(new Paragraph(new Run($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}"))
    {
        FontStyle = FontStyles.Italic,
        TextAlignment = TextAlignment.Center
    });

    doc.Blocks.Add(new Paragraph(new Run(" ")));
    return doc;
}
private void ExportInventoryPdf()
{
    string[] headers = new string[]
    {
        "Item", "Quantity", "Target", "Deficit"
    };

    var rows = new List<string[]>();

    foreach (var item in InventoryGrid.Items)
    {
        if (item == null) continue;
        dynamic r = item;

        rows.Add(new string[]
        {
            r.Name?.ToString() ?? "",
            r.Quantity?.ToString() ?? "",
            r.TargetQuantity?.ToString() ?? "",
            r.Deficit?.ToString() ?? ""
        });
    }

    PdfExporter.Export(
        "Inventory Report",
        "Current Inventory Status",
        headers,
        rows
    );
}
private void ExportCreditPdf()
{
    string[] headers = new string[]
    {
        "Receipt", "Customer", "Phone", "Balance", "Date"
    };

    var rows = new List<string[]>();

    foreach (var item in CreditGrid.Items)
    {
        if (item == null) continue;
        dynamic r = item;

        rows.Add(new string[]
        {
            r.ReceiptNumber?.ToString() ?? "",
            r.CustomerName?.ToString() ?? "",
            r.Phone?.ToString() ?? "",
            r.Balance?.ToString() ?? "0.00",
            r.CreatedAt?.ToString("yyyy-MM-dd") ?? ""
        });
    }

    PdfExporter.Export(
        "Credit Report",
        "Outstanding Credit Accounts",
        headers,
        rows
    );
}
private void ExportExpensesPdf()
{
    string[] headers = new string[]
    {
        "Date", "Category", "Description", "Amount"
    };

    var rows = new List<string[]>();

    foreach (var item in ExpensesGrid.Items)
    {
        if (item == null) continue;
        dynamic r = item;

        rows.Add(new string[]
        {
            r.Date?.ToString("yyyy-MM-dd") ?? "",
            r.Category?.ToString() ?? "",
            r.Description?.ToString() ?? "",
            r.Amount?.ToString("0.00") ?? "0.00"
        });
    }

    PdfExporter.Export(
        "Expenses Report",
        "Expense Summary",
        headers,
        rows
    );
}

private void ExportSalesPdf()
{
    var headers = new[]
    {
        "Receipt", "Item", "Qty", "Price", "Total", "Payment", "Cashier"    
    };

    var rows = new List<string[]>();

    foreach (var item in SoldItemsGrid.Items)
    {
        if (item is SoldItemReport r)
        {
            rows.Add(new[]
            {
                r.ReceiptNumber,
                r.ItemName,
                r.Quantity.ToString(),
                r.Price.ToString("0.00"),
                r.Total.ToString("0.00"),
                r.PaymentMethod ?? "",
                r.Cashier ?? "",
                r.SaleType
            });
        }
    }

    var period =
        FromDatePicker.SelectedDate != null
        ? $"Period: {FromDatePicker.SelectedDate:yyyy-MM-dd} to {ToDatePicker.SelectedDate:yyyy-MM-dd}"
        : "Period: ALL";

    PdfExporter.Export(
        "Sales Report",
        period,
        headers,
        rows);
}
private void ExportSalesExcel()
{
    var items = SoldItemsGrid.ItemsSource as IEnumerable<SoldItemReport>;
    if (items == null || !items.Any())
    {
        MessageBox.Show("No sales data to export.");
        return;
    }

    var wb = new XLWorkbook();
    var ws = wb.Worksheets.Add("Sales");

    int row = 1;

    // ===== TITLE =====
    ws.Cell(row, 1).Value = "SALES REPORT";
    ws.Range(row, 1, row, 8).Merge().Style
        .Font.SetBold()
        .Font.SetFontSize(16)
        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
    row += 2;

    // ===== DATE RANGE =====
    string range =
        FromDatePicker.SelectedDate != null
            ? $"Period: {FromDatePicker.SelectedDate:yyyy-MM-dd} to {ToDatePicker.SelectedDate:yyyy-MM-dd}"
            : "Period: ALL";

    ws.Cell(row, 1).Value = range;
    ws.Range(row, 1, row, 8).Merge();
    row += 2;

    // ===== SUMMARY =====
    ws.Cell(row, 1).Value = "Total Sales:";
    ws.Cell(row, 2).Value = TotalSalesText.Text;
    row++;

    ws.Cell(row, 1).Value = "Profit:";
    ws.Cell(row, 2).Value = ProfitText.Text;
    row++;

    ws.Cell(row, 1).Value = "Receipts:";
    ws.Cell(row, 2).Value = ReceiptCountText.Text;
    row += 2;

    // ===== HEADER =====
    ws.Cell(row, 1).Value = "#";
    ws.Cell(row, 2).Value = "Receipt";
    ws.Cell(row, 3).Value = "Item";
    ws.Cell(row, 4).Value = "Qty";
    ws.Cell(row, 5).Value = "Price";
    ws.Cell(row, 6).Value = "Total";
    ws.Cell(row, 7).Value = "Payment";
    ws.Cell(row, 8).Value = "Cashier";
    ws.Cell(row, 8).Value = "Type";


    ws.Range(row, 1, row, 8).Style.Font.SetBold();
    row++;

    int index = 1;
    foreach (var s in items)
    {
        ws.Cell(row, 1).Value = index++;
        ws.Cell(row, 2).Value = s.ReceiptNumber;
        ws.Cell(row, 3).Value = s.ItemName;
        ws.Cell(row, 4).Value = s.Quantity;
        ws.Cell(row, 5).Value = s.Price;
        ws.Cell(row, 6).Value = s.Total;
        ws.Cell(row, 7).Value = s.PaymentMethod ?? "";
        ws.Cell(row, 8).Value = s.Cashier ?? "";
        ws.Cell(row, 8).Value = s.SaleType ?? "";
        row++;
    }

    ws.Columns().AdjustToContents();

    // ===== SAVE =====
    var dlg = new SaveFileDialog
    {
        Filter = "Excel File (*.xlsx)|*.xlsx",
        FileName = $"Sales_Report_{DateTime.Now:yyyyMMdd}.xlsx"
    };

    if (dlg.ShowDialog() == true)
        wb.SaveAs(dlg.FileName);
}

private void ExportInventoryExcel()
{
    var items = InventoryGrid.ItemsSource as IEnumerable<ItemLookup>;
    if (items == null || !items.Any())
    {
        MessageBox.Show("No inventory data to export.");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "Excel File (*.xlsx)|*.xlsx",
        FileName = $"Inventory_Report_{DateTime.Now:yyyyMMdd}.xlsx"
    };

    if (dialog.ShowDialog() != true)
        return;

    using var wb = new XLWorkbook();
    var ws = wb.Worksheets.Add("Inventory");

    ws.Cell(1, 1).Value = "#";
    ws.Cell(1, 2).Value = "Item";
    ws.Cell(1, 3).Value = "Quantity";
    ws.Cell(1, 4).Value = "Target";
    ws.Cell(1, 5).Value = "Deficit";

    int row = 2;
    int index = 1;

    foreach (var item in items)
    {
        ws.Cell(row, 1).Value = index++;
        ws.Cell(row, 2).Value = item.Name;
        ws.Cell(row, 3).Value = item.Quantity;
        ws.Cell(row, 4).Value = item.TargetQuantity ?? 0;
        ws.Cell(row, 5).Value = item.Deficit;
        row++;
    }

    ws.Columns().AdjustToContents();
    wb.SaveAs(dialog.FileName);

    MessageBox.Show("Inventory exported successfully.");
}
private void ExportCreditExcel()
{
    if (_allCredits.Count == 0)
    {
        MessageBox.Show("No credit data to export.");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "Excel File (*.xlsx)|*.xlsx",
        FileName = $"Credit_Report_{DateTime.Now:yyyyMMdd}.xlsx"
    };

    if (dialog.ShowDialog() != true)
        return;

    using var wb = new XLWorkbook();
    var ws = wb.Worksheets.Add("Credit");

    ws.Cell(1, 1).Value = "#";
    ws.Cell(1, 2).Value = "Customer";
    ws.Cell(1, 3).Value = "Phone";
    ws.Cell(1, 4).Value = "Total";
    ws.Cell(1, 5).Value = "Paid";
    ws.Cell(1, 6).Value = "Balance";
    ws.Cell(1, 7).Value = "Last Payment";

    int row = 2;
    int index = 1;

    foreach (var c in _allCredits)
    {
        ws.Cell(row, 1).Value = index++;
        ws.Cell(row, 2).Value = c.CustomerName;
        ws.Cell(row, 3).Value = c.Phone;
        ws.Cell(row, 4).Value = c.Total;
        ws.Cell(row, 5).Value = c.Paid;
        ws.Cell(row, 6).Value = c.Balance;
        ws.Cell(row, 7).Value = c.LastPaymentDate.ToString("yyyy-MM-dd");
        row++;
    }

    ws.Columns().AdjustToContents();
    wb.SaveAs(dialog.FileName);

    MessageBox.Show("Credit report exported successfully.");
}
private void ExportExpensesExcel()
{
    var expenses = ExpensesGrid.ItemsSource as IEnumerable<Expense>;
    if (expenses == null || !expenses.Any())
    {
        MessageBox.Show("No expenses data to export.");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "Excel File (*.xlsx)|*.xlsx",
        FileName = $"Expenses_Report_{DateTime.Now:yyyyMMdd}.xlsx"
    };

    if (dialog.ShowDialog() != true)
        return;

    using var wb = new XLWorkbook();
    var ws = wb.Worksheets.Add("Expenses");

    ws.Cell(1, 1).Value = "#";
    ws.Cell(1, 2).Value = "Date";
    ws.Cell(1, 3).Value = "Category";
    ws.Cell(1, 4).Value = "Description";
    ws.Cell(1, 5).Value = "Amount";

    int row = 2;
    int index = 1;

    foreach (var e in expenses)
    {
        ws.Cell(row, 1).Value = index++;
        ws.Cell(row, 2).Value = e.Date.ToString("yyyy-MM-dd");
        ws.Cell(row, 3).Value = e.Category;
        ws.Cell(row, 4).Value = e.Description;
        ws.Cell(row, 5).Value = e.Amount;
        row++;
    }

    ws.Columns().AdjustToContents();
    wb.SaveAs(dialog.FileName);

    MessageBox.Show("Expenses exported successfully.");
}


public void OpenCreditReport()
{
    ReportsTabControl.SelectedIndex = 2; // Credit tab
}
private void SoldItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (SoldItemsGrid.SelectedItem is not SoldItemReport item)
        return;

    PreviewReceipt(item.ReceiptNumber);
}
private void PreviewReceipt(string receiptNumber)
{
    var receiptData = ReportsRepository.GetReceiptDetails(receiptNumber);

    if (receiptData == null || !receiptData.Items.Any())
    {
        MessageBox.Show("Receipt not found.");
        return;
    }

    var receiptText = ReceiptBuilder.Build(
        receiptData.Items,
        receiptData.Payment,
        receiptNumber,
        receiptData.Cashier,
        ReceiptMode.Duplicate

    );

    var document = BuildFlowDocument(receiptText, ReceiptMode.Duplicate);

    var preview = new PrintPreviewWindow(document);
    preview.ShowDialog();
}
private FlowDocument BuildFlowDocument(string text, ReceiptMode mode)
{
    var doc = new FlowDocument
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        PagePadding = new Thickness(10),
        PageWidth = 400,
    };

    var grid = new Grid();

    // 🔴 WATERMARK
    if (mode == ReceiptMode.Duplicate)
    {
        var watermark = new TextBlock
        {
            Text = "DUPLICATE",
            FontSize = 80,
            Opacity = 0.08,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            RenderTransform = new RotateTransform(-30)
        };

        grid.Children.Add(watermark);
    }

    var stack = new StackPanel();

    foreach (var line in text.Split('\n'))
    {
        stack.Children.Add(new TextBlock
        {
            Text = line.TrimEnd(),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        });
    }

    grid.Children.Add(stack);

    doc.Blocks.Add(new BlockUIContainer(grid));

    return doc;
}

private void CreditGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    if (CreditGrid.SelectedItem is CreditView selected)
    {
        try
        {
            var historyWindow = new CreditHistoryWindow(
                selected.CustomerId,
                selected.CustomerName,
                selected.Phone
            );
            historyWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load credit history: {ex.Message}");
        }
    }
}

public static string FormatAction(string action)
{
    return action switch
    {
        "ADD_ITEM" => "Added",
        "ITEM_SOLD" => "Sold",
        "DAMAGE" => "Damaged",
        "STOCK_ADJUSTED" => "Stock Adjusted",
        "CUSTOMER_RETURN" => "Customer Return",
        "CUSTOMER_RETURN_RESTOCK" => "Restocked",
        "SUPPLIER_RETURN" => "Returned to Supplier",
        "SUPPLIER_RETURN_STOCK_OUT" => "Supplier Return (Stock Out)",
        "REPACK_OUT" => "To Repack",
        "REPACK_IN" => "From Repack",
        _ => action
    };
}
public static string FormatQty(decimal qty)
{
    if (qty > 0) return $"+{qty}";
    return qty.ToString();
}
private void ApplyMovementDateFilter_Click(object sender, RoutedEventArgs e)
{
    DateTime from = MovementFromDatePicker.SelectedDate ?? DateTime.Today;
    DateTime to = MovementToDatePicker.SelectedDate ?? DateTime.Today;

    if (from > to)
    {
        MessageBox.Show("From date cannot be later than To date.");
        return;
    }

    // include full day
    to = to.Date.AddDays(1).AddSeconds(-1);

    LoadMovements(from, to);
}

private void ClearMovementDateFilter_Click(object sender, RoutedEventArgs e)
{
    MovementFromDatePicker.SelectedDate = null;
    MovementToDatePicker.SelectedDate = null;
    MovementSearchTextBox.Clear();

    _allMovements.Clear();
    _viewData.Clear();

    MovementGrid.ItemsSource = null;
}
private void MovementSearchChanged(object sender, TextChangedEventArgs e)
{
    ApplyFilters();
}
private void stockmovementTab_GotFocus(object sender, RoutedEventArgs e)
{
    if (!_allMovements.Any())
    {
        var today = DateTime.Today;
        LoadMovements(today, today.AddDays(1).AddSeconds(-1));
    }
}
private void LoadMovements(DateTime from, DateTime to)
{
    _allMovements = ActivityRepository.GetLogs(
        from,
        to,
        SessionContext.EffectiveBranchId
    );

    ApplyFilters(); // 🔥 always go through filter pipeline
}
private void ApplyFilters()
{
    string search = MovementSearchTextBox.Text.Trim().ToLower();

    var filtered = _allMovements.Where(x =>
        string.IsNullOrEmpty(search) ||
        (x.EntityName?.ToLower().Contains(search) == true) ||
        (x.Action?.ToLower().Contains(search) == true)
    ).ToList();

    _viewData = filtered.Select(x => new MovementView
    {
        Date = x.CreatedAt,
        Item = x.EntityName,
        Action = FormatAction(x.Action),

        Qty = FormatQty(x.QuantityChange),

        BeforeQty = x.BeforeValue,
        AfterQty = x.AfterValue,

        Balance = x.AfterValue,

        Unit = x.UnitType,
        Price = x.Price?.ToString("0.00") ?? "",
        User = x.PerformedBy,
        Reason = x.Reason
    }).ToList();

    MovementGrid.ItemsSource = null;
    MovementGrid.ItemsSource = _viewData;

    // 🔥 TOTAL
    TotalMovementText.Text = filtered.Count.ToString();
}

private List<MovementView> GetMovementSource()
{
    return MovementGrid.ItemsSource as List<MovementView> ?? new List<MovementView>();
}
private void PrintStockMovement()
{
    var data = GetMovementSource();

    if (data == null || data.Count == 0)
    {
        MessageBox.Show("No stock movement records to print.");
        return;
    }

    var doc = new FlowDocument
    {
        PagePadding = new Thickness(40),
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
        FontSize = 11,
        PageWidth = 794
    };

    doc.Blocks.Add(new Paragraph(new Run("STOCK MOVEMENT REPORT"))
    {
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center
    });

    doc.Blocks.Add(new Paragraph(new Run($"Printed: {DateTime.Now:yyyy-MM-dd HH:mm}"))
    {
        Margin = new Thickness(0,0,0,10)
    });

    var table = new Table();
    doc.Blocks.Add(table);

    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());
    table.Columns.Add(new TableColumn());

    var header = new TableRowGroup();
    table.RowGroups.Add(header);

    header.Rows.Add(new TableRow
    {
        FontWeight = FontWeights.Bold,
        Cells =
        {
            new TableCell(new Paragraph(new Run("Date"))),
            new TableCell(new Paragraph(new Run("Item"))),
            new TableCell(new Paragraph(new Run("Action"))),
            new TableCell(new Paragraph(new Run("Before Qty"))),
            new TableCell(new Paragraph(new Run("Qty"))),
            new TableCell(new Paragraph(new Run("After Qty"))),
            new TableCell(new Paragraph(new Run("Unit"))),
            new TableCell(new Paragraph(new Run("User"))),
            new TableCell(new Paragraph(new Run("Reason")))
        }
    });

    var body = new TableRowGroup();
    table.RowGroups.Add(body);

    foreach (var m in data)
    {
        body.Rows.Add(new TableRow
        {
            Cells =
            {
                new TableCell(new Paragraph(new Run(m.Date.ToString("yyyy-MM-dd HH:mm")))),
                new TableCell(new Paragraph(new Run(m.Item))),
                new TableCell(new Paragraph(new Run(m.Action))),
                new TableCell(new Paragraph(new Run(m.BeforeQty))),
                new TableCell(new Paragraph(new Run(m.Qty.ToString()))),
                new TableCell(new Paragraph(new Run(m.AfterQty))),
                new TableCell(new Paragraph(new Run(m.Unit))),
                new TableCell(new Paragraph(new Run(m.User))),
                new TableCell(new Paragraph(new Run(m.Reason)))
            }
        });
    }

    new PrintPreviewWindow(doc)
    {
        Owner = GetMainWindow()
    }.ShowDialog();
}

private void ExportStockMovementPdf()
{
    var data = GetMovementSource();

    if (data == null || data.Count == 0)
    {
        MessageBox.Show("No stock movement records.");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "PDF File (*.pdf)|*.pdf",
        FileName = $"StockMovement_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
    };

    if (dialog.ShowDialog() != true)
        return;

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    Document.Create(container =>
    {
        container.Page(page =>
        {   page.Size(QuestPDF.Helpers.PageSizes.A4);
            page.Margin(30);

            page.Header().Text("STOCK MOVEMENT REPORT")
                .FontSize(18).Bold().AlignCenter();

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(120);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(07);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(70);
                    columns.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().Text("Date").Bold().FontSize(11);
                    h.Cell().Text("Item").Bold().FontSize(11);
                    h.Cell().Text("Action").Bold().FontSize(11);
                    h.Cell().Text("Before").Bold().FontSize(11);
                    h.Cell().Text("Qty").Bold().FontSize(11);
                    h.Cell().Text("After").Bold().FontSize(11);
                    h.Cell().Text("Unit").Bold().FontSize(11);
                    h.Cell().Text("User").Bold().FontSize(11);
                    h.Cell().Text("Reason").Bold().FontSize(11);
                });

                foreach (var m in data)
                {
                    table.Cell().Text(m.Date.ToString("yyyy-MM-dd HH:mm")).FontSize(9);
                    table.Cell().Text(m.Item).FontSize(9);
                    table.Cell().Text(m.Action).FontSize(9);
                    table.Cell().Text(m.BeforeQty).FontSize(9);
                    table.Cell().Text(m.Qty.ToString()).FontSize(9);
                    table.Cell().Text(m.AfterQty).FontSize(9);
                    table.Cell().Text(m.Unit).FontSize(9);
                    table.Cell().Text(m.User).FontSize(9);
                    table.Cell().Text(m.Reason).FontSize(9);
                }
            });
            page.Footer().AlignCenter()
                .Text($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}");
        });
    })
    .GeneratePdf(dialog.FileName);

    MessageBox.Show("PDF exported successfully.");
}
private void ExportStockMovementExcel()
{
    var data = GetMovementSource();

    if (data == null || data.Count == 0)
    {
        MessageBox.Show("No stock movement records.");
        return;
    }

    var dialog = new SaveFileDialog
    {
        Filter = "Excel File (*.xlsx)|*.xlsx",
        FileName = $"StockMovement_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
    };

    if (dialog.ShowDialog() != true)
        return;

    using var workbook = new XLWorkbook();
    var ws = workbook.Worksheets.Add("Stock Movement");

    ws.Cell(1, 1).Value = "Date";
    ws.Cell(1, 2).Value = "Item";
    ws.Cell(1, 3).Value = "Action";
    ws.Cell(1, 5).Value = "Before Qty";
    ws.Cell(1, 4).Value = "Qty";
    ws.Cell(1, 6).Value = "After Qty";
    ws.Cell(1, 5).Value = "Unit";
    ws.Cell(1, 6).Value = "User";
    ws.Cell(1, 7).Value = "Reason";

    int row = 2;

    foreach (var m in data)
    {
        ws.Cell(row, 1).Value = m.Date;
        ws.Cell(row, 2).Value = m.Item;
        ws.Cell(row, 3).Value = m.Action;
        ws.Cell(row, 5).Value = m.BeforeQty;
        ws.Cell(row, 4).Value = m.Qty;       
        ws.Cell(row, 6).Value = m.AfterQty;
        ws.Cell(row, 7).Value = m.Unit;
        ws.Cell(row, 8).Value = m.User;
        ws.Cell(row, 9).Value = m.Reason;

        row++;
    }

    ws.Columns().AdjustToContents();
    workbook.SaveAs(dialog.FileName);

    MessageBox.Show("Excel exported successfully.");
}

  }
}