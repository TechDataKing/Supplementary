using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TechKingPOS.App.Models;
using TechKingPOS.App.Data;
using TechKingPOS.App.Services;
using ExcelDataReader;
using System.Data;

namespace TechKingPOS.App
{
    public partial class ImportPreviewWindow : Window
    {
        private readonly List<ItemImportResult> _rows;

        public ImportPreviewWindow(List<ItemImportResult> rows)
        {
            InitializeComponent();
            _rows = rows;
            ItemsGrid.ItemsSource = rows;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

private void Import_Click(object sender, RoutedEventArgs e)
{
    var validRows = _rows.Where(r => r.IsValid).ToList();

    var skipped = new List<ItemImportResult>();
    var imported = 0;

    foreach (var r in validRows)
    {
        decimal qty = r.Quantity;
        decimal marked = r.MarkedPrice;
        decimal selling = r.SellingPrice;
        decimal? wholesale = r.WholesalePrice;
        decimal unitValue = r.UnitValue ?? 1m;

        // SAFE UNIT CONVERSION
        if (!UnitConverter.TryToBase(
                r.UnitType,
                qty,
                unitValue,
                out var baseUnit,
                out var baseQty,
                out var unitError))
        {
            r.IsValid = false;
            r.Error = unitError;
            skipped.Add(r);
            continue;
        }

        string cleanName = r.Name?.Trim();
        string? cleanAlias = string.IsNullOrWhiteSpace(r.Alias) ? null : r.Alias.Trim();

        var existing = ItemRepository.GetExactItem(cleanName, cleanAlias);

        if (existing != null)
        {
            if (existing.UnitType != baseUnit)
            {
                r.IsValid = false;
                r.Error = $"Unit mismatch: existing [{existing.UnitType}], import [{baseUnit}]";
                skipped.Add(r);
                continue;
            }

            ItemRepository.AddStock(existing.Id, baseQty, marked, selling,wholesale, ActivityActions.ADD_ITEM);
        }
        else
        {
            ItemRepository.InsertItem(
                r.Name,
                r.Alias,
                marked,
                selling,
                wholesale,
                baseQty,
                baseUnit,
                unitValue,
                r.Supplier
            );
        }

        imported++;
    }


    // 📢 SUMMARY MESSAGE
    var message =
        $"Imported: {imported} items\n" +
        $"Skipped: {skipped.Count} items";

    if (skipped.Any())
    {
        message += "\n\nSkipped items:\n" +
            string.Join(
                "\n",
                skipped.Select(s => $"- {s.Name}: {s.Error}")
            );
    }

    MessageBox.Show(
        message,
        "Import Result",
        MessageBoxButton.OK,
        skipped.Any()
            ? MessageBoxImage.Warning
            : MessageBoxImage.Information
    );

    Close();
}


        // ---------- CSV ----------
        public static List<ItemImportResult> FromCsv(string path)
        {
            var results = new List<ItemImportResult>();

            try{

            
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                var parts = line.Split(',');

                var row = BuildRow(
                    parts.ElementAtOrDefault(0),
                    parts.ElementAtOrDefault(1),
                    parts.ElementAtOrDefault(2),
                    parts.ElementAtOrDefault(3),
                    parts.ElementAtOrDefault(4),
                    parts.ElementAtOrDefault(5),
                    parts.ElementAtOrDefault(6),
                    parts.ElementAtOrDefault(7),
                    parts.ElementAtOrDefault(8)
                );

                results.Add(row);
            }
            }
            catch (IOException)
            {
                MessageBox.Show(
                    "The Excel file is currently open.\n\nPlease close the file and try again.",
                    "File In Use",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to read Excel file.\n\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            return results;
        }

        // ---------- EXCEL ----------
public static List<ItemImportResult> FromExcel(string path)
{
    var results = new List<ItemImportResult>();

    try
    {
        System.Text.Encoding.RegisterProvider(
            System.Text.CodePagesEncodingProvider.Instance);

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        });

        var table = dataSet.Tables[0];

        for (int i = 0; i < table.Rows.Count; i++)
        {
            var r = table.Rows[i];

            string Get(string columnName)
            {
                return table.Columns.Contains(columnName)
                    ? r[columnName]?.ToString()
                    : null;
            }

            var row = BuildRow(
                Get("Name"),
                Get("Alias"),
                Get("Marked Price"),
                Get("Selling Price"),
                Get("Wholesale Price"),
                Get("Quantity"),
                Get("UnitType"),
                Get("UnitValue"),
                Get("Supplier")
            );

            results.Add(row);
        }
    }
    catch (IOException)
    {
        MessageBox.Show(
            "The Excel file is currently open.\n\nPlease close the file and try again.",
            "File In Use",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            $"Failed to read Excel file.\n\n{ex.Message}",
            "Import Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }

    return results;
}
        // ---------- VALIDATION ----------
private static ItemImportResult BuildRow(
    string name,
    string alias,
    string marked,
    string selling,
    string wholesale,
    string qty,
    string unitType,
    string unitValue,
    string supplier)
{
    var row = new ItemImportResult();

    row.Name = name?.Trim();
    row.Alias = alias?.Trim();

    // REQUIRED NAME
    if (string.IsNullOrWhiteSpace(row.Name))
    {
        row.IsValid = false;
        row.Error = "Name is required";
        return row;
    }

    // MARKED PRICE
    if (!decimal.TryParse(marked, out var mp))
    {
        row.IsValid = false;
        row.Error = "Invalid Marked Price";
        return row;
    }

    row.MarkedPrice = mp;

    // SELLING PRICE
    row.SellingPrice =
        decimal.TryParse(selling, out var sp)
            ? sp
            : 0;

    // WHOLESALE PRICE
    row.WholesalePrice =
        decimal.TryParse(wholesale, out var wp)
            ? wp
            : null;

    // QUANTITY
    if (!decimal.TryParse(qty, out var q) || q <= 0)
    {
        row.IsValid = false;
        row.Error = "Invalid Quantity";
        return row;
    }

    row.Quantity = q;

    // UNIT TYPE
    row.UnitType = unitType?.Trim();

    if (string.IsNullOrWhiteSpace(row.UnitType))
    {
        row.IsValid = false;
        row.Error = "Unit Type is required";
        return row;
    }

    // UNIT VALUE
    row.UnitValue =
        decimal.TryParse(unitValue, out var uv)
            ? uv
            : (decimal?)null;

    row.Supplier = supplier?.Trim();

    row.IsValid = true;
    row.Error = "OK";

    return row;
}    }
}
