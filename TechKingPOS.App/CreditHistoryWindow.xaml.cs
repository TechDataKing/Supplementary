using System.Collections.Generic;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Printing;
using System.Windows.Documents;
using System.Linq;

using TechKingPOS.App.Models;
using TechKingPOS.App.Data;

namespace TechKingPOS.App
{
    public partial class CreditHistoryWindow : Window
    {
        private int _customerId;
        private string _customerName;
        private string _phone;
        private string _receipt;

public CreditHistoryWindow(int customerId, string customerName, string phone)
{
    InitializeComponent();

    _customerId = customerId;
    _customerName = customerName;
    _phone = phone;

    LoadData();
}

private void LoadData()
{
    // Get the current month and year
    DateTime currentDate = DateTime.Now;

    // Set the first day of the current month
    DateTime fromDate = new DateTime(currentDate.Year, currentDate.Month, 1);

    // Set the last day of the current month
    DateTime toDate = fromDate.AddMonths(1).AddDays(-1);

    // Now, use these dates to filter the customer statement for the current month
    var history = ReportsRepository.GetCustomerStatement(
        _customerId,
        fromDate,
        toDate
    );

    // Check if data is returned
    if (history != null && history.Any())
    {
        // Bind the data to the HistoryGrid
        HistoryGrid.ItemsSource = history;
    }
    else
    {
        MessageBox.Show("No credit history available for the selected customer.");
    }
}

private void Filter_Click(object sender, RoutedEventArgs e)
{
    // Check if FromDate and ToDate are not null
    if (FromDate.SelectedDate != null && ToDate.SelectedDate != null)
    {
        LoadData();
    }
    else
    {
        MessageBox.Show("Please select a valid date range.");
    }
}
private void Print_Click(object sender, RoutedEventArgs e)
{
    PrintDialog printDialog = new PrintDialog();

    if (printDialog.ShowDialog() != true) return;

    FlowDocument doc = BuildDocument();

    printDialog.PrintDocument(
        ((IDocumentPaginatorSource)doc).DocumentPaginator,
        "Credit Statement"
    );
}

        // ================= PDF EXPORT =================
private void ExportPdf_Click(object sender, RoutedEventArgs e)
{

    PrintDialog printDialog = new PrintDialog();

    if (printDialog.ShowDialog() != true) return;

    FlowDocument doc = BuildDocument();

    printDialog.PrintDocument(
        ((IDocumentPaginatorSource)doc).DocumentPaginator,
        "Credit Statement"
    );
}

        // ================= EXCEL EXPORT =================
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                FileName = "CreditStatement.xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            var history = (List<CreditPaymentView>)HistoryGrid.ItemsSource;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Statement");

            int row = 1;

            // HEADER (MATCH PDF)
            ws.Cell(row, 1).Value = "CREDIT STATEMENT";
            ws.Range(row, 1, row, 5).Merge().Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = $"Customer: {_customerName}";
            ws.Cell(row++, 1).Style.Font.Bold = true;

            ws.Cell(row, 1).Value = $"Phone: {_phone}";
            ws.Cell(row++, 1).Style.Font.Bold = true;

            ws.Cell(row, 1).Value = $"Receipt: {_receipt}";
            ws.Cell(row++, 1).Style.Font.Bold = true;

            ws.Cell(row, 1).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
            ws.Cell(row++, 1).Style.Font.Bold = true;
            row++;

            // TABLE HEADER
            ws.Cell(row, 1).Value = "Date";
            ws.Cell(row, 2).Value = "Description";
            ws.Cell(row, 3).Value = "DR";
            ws.Cell(row, 4).Value = "CR";
            ws.Cell(row, 5).Value = "Balance";

            row++;

            // DATA
            foreach (var item in history)
            {
                ws.Cell(row, 1).Value = item.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 2).Value = item.Description;
                ws.Cell(row, 3).Value = item.Debit;
                ws.Cell(row, 4).Value = item.Credit;
                ws.Cell(row, 5).Value = item.Balance;
                row++;
            }

            // TOTAL
            decimal totalDr = history.Sum(x => x.Debit);
            decimal totalCr = history.Sum(x => x.Credit);

            ws.Cell(row, 2).Value = "TOTAL";
            ws.Cell(row, 3).Value = totalDr;
            ws.Cell(row, 4).Value = totalCr;

            ws.Range(row, 2, row, 4).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();

            workbook.SaveAs(dialog.FileName);

            MessageBox.Show("Excel Exported Successfully!");
        }

        // ================= BUILD DOCUMENT =================
private FlowDocument BuildDocument()
{
    FlowDocument doc = new FlowDocument();

    // Set margins and page size (A4 size)
    var pageSize = new Size(595, 842); // A4 paper size
    doc.PageWidth = pageSize.Width;
    doc.PageHeight = pageSize.Height;
    doc.PagePadding = new Thickness(20);

    doc.FontFamily = new FontFamily("Segoe UI");
    doc.FontSize = 12;

    // TITLE
    doc.Blocks.Add(new Paragraph(new Run("CREDIT STATEMENT"))
    {
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center
    });

    // CUSTOMER INFO (BOLD)
    var customerBlock = new Paragraph();
    customerBlock.Inlines.Add(new Run($"Customer: {_customerName}\n") { FontWeight = FontWeights.Bold });
    customerBlock.Inlines.Add(new Run($"Phone: {_phone}\n") { FontWeight = FontWeights.Bold });
    customerBlock.Inlines.Add(new Run($"Receipt: {_receipt}\n") { FontWeight = FontWeights.Bold });
    customerBlock.Inlines.Add(new Run($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}\n") { FontWeight = FontWeights.Bold });

    customerBlock.Margin = new Thickness(0, 0, 0, 10);
    doc.Blocks.Add(customerBlock);

    // TABLE
    Table table = new Table();
    doc.Blocks.Add(table);

    // Define column sizes
    table.Columns.Add(new TableColumn() { Width = new GridLength(150) });
    table.Columns.Add(new TableColumn() { Width = new GridLength(200) });
    table.Columns.Add(new TableColumn() { Width = new GridLength(80) });
    table.Columns.Add(new TableColumn() { Width = new GridLength(80) });
    table.Columns.Add(new TableColumn() { Width = new GridLength(100) });

    var headerGroup = new TableRowGroup();
    table.RowGroups.Add(headerGroup);

    var headerRow = new TableRow();
    headerGroup.Rows.Add(headerRow);

    headerRow.Cells.Add(CreateHeaderCell("Date"));
    headerRow.Cells.Add(CreateHeaderCell("Description"));
    headerRow.Cells.Add(CreateHeaderCell("Debit"));
    headerRow.Cells.Add(CreateHeaderCell("Credit"));
    headerRow.Cells.Add(CreateHeaderCell("Balance"));

    var dataGroup = new TableRowGroup();
    table.RowGroups.Add(dataGroup);

    var history = (List<CreditPaymentView>)HistoryGrid.ItemsSource;

    foreach (var item in history)
    {
        var row = new TableRow();

        row.Cells.Add(CreateCell(item.CreatedAt.ToString("yyyy-MM-dd HH:mm")));
        row.Cells.Add(CreateCell(item.Description));
        row.Cells.Add(CreateCell(item.Debit.ToString("N2")));
        row.Cells.Add(CreateCell(item.Credit.ToString("N2")));
        row.Cells.Add(CreateCell(item.Balance.ToString("N2")));

        dataGroup.Rows.Add(row);
    }

    // TOTAL
    decimal totalDr = history.Sum(x => x.Debit);
    decimal totalCr = history.Sum(x => x.Credit);

    var totalRow = new TableRow();

    totalRow.Cells.Add(CreateCell(""));
    totalRow.Cells.Add(CreateBoldCell("TOTAL"));
    totalRow.Cells.Add(CreateBoldCell(totalDr.ToString("N2")));
    totalRow.Cells.Add(CreateBoldCell(totalCr.ToString("N2")));
    totalRow.Cells.Add(CreateCell(""));

    dataGroup.Rows.Add(totalRow);

    return doc;
}
private TableCell CreateBoldCell(string text)
{
    return new TableCell(new Paragraph(new Run(text)))
    {
        FontWeight = FontWeights.Bold,
        Padding = new Thickness(5),
        BorderBrush = Brushes.Black,
        BorderThickness = new Thickness(0.5)
    };
}

        private TableCell CreateHeaderCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                FontWeight = FontWeights.Bold,
                Background = Brushes.LightGray,
                Padding = new Thickness(5),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5)
            };
        }

        private TableCell CreateCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text)))
            {
                Padding = new Thickness(5),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0.3)
            };
        }
    }
}