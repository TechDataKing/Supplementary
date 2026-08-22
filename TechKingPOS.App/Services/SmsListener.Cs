using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using TechKingPOS.App.Data;
using System.Linq;
using System.Text.RegularExpressions;

//using TechKingPOS.App.Data;
using TechKingPOS.App.Models;

namespace TechKingPOS.App.Services {


public class SmsListener
{
    private HttpListener _listener;

    public void Start()
    {
        _listener = new HttpListener();

        _listener.Prefixes.Add("http://+:5000/api/sms/");
        _listener.Start();

        Task.Run(() => Listen());
    }

    private async Task Listen()
    {
        while (true)
        {
            var context = await _listener.GetContextAsync();
            var request = context.Request;

            using (var reader = new StreamReader(request.InputStream))
            {
                var body = await reader.ReadToEndAsync();

                Console.WriteLine("SMS RECEIVED:");
                Console.WriteLine(body);

                ProcessSms(body);
            }

            var response = context.Response;
            var buffer = Encoding.UTF8.GetBytes("OK");

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
private void SaveMessage(string message, MpesaParsed parsed)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO MpesaMessages 
        (MessageText, MpesaCode, ExtractedAmount, SenderName, SenderPhone, IsProcessed)
        VALUES (@msg, @code, @amount, @name, @phone, 0)";

    cmd.Parameters.AddWithValue("@msg", message);
    cmd.Parameters.AddWithValue("@code", parsed.Code ?? "");
    cmd.Parameters.AddWithValue("@amount", parsed.Amount);
    cmd.Parameters.AddWithValue("@name", parsed.SenderName ?? "");
    cmd.Parameters.AddWithValue("@phone", parsed.SenderPhone ?? "");

    cmd.ExecuteNonQuery();
}
private void ProcessSms(string body)
{
    var parsed = ParseMpesaMessage(body);
    SaveMessage(body, parsed);
    if (parsed.Amount <= 0 || string.IsNullOrEmpty(parsed.Code))
        return;

    if (MpesaCodeExists(parsed.Code))
    {
        Console.WriteLine("Duplicate Mpesa message ignored.");
        return;
    }

    // ✅ Save first
    //SaveMessage(body, parsed.Code, parsed.Amount, parsed.Sender);

    // ✅ SMART AUTO MATCH
    var matches = SalesRepository.GetPendingByAmount(parsed.Amount);

    if (matches.Count == 1)
    {
        var sale = matches.First();

        SalesRepository.CompleteMpesaSale(
            sale.Receipt,
            parsed.Amount,
            parsed.Code
        );
        if (PendingReceiptStore.Data.TryGetValue(sale.Receipt, out var data))
        {
            var receiptText = ReceiptBuilder.Build(
                data.Items,
                data.Payment,
                sale.Receipt,
                data.Cashier,
                ReceiptMode.Original
            );

            PrintService.PrintReceiptText(receiptText, "Receipt",1);

            PendingReceiptStore.Data.Remove(sale.Receipt);
        }

        MarkMessageAsUsedByCode(parsed.Code);

        Console.WriteLine("✅ Auto-matched & completed");
    }
    else if (matches.Count > 1)
    {
        Console.WriteLine("⚠ Multiple matches → manual selection required");
    }
    else
    {
        Console.WriteLine("⚠ No matching sale found");
    }
}

public class MpesaParsed
{
    public string Code { get; set; }
    public decimal Amount { get; set; }
    public string SenderName { get; set; }
    public string SenderPhone { get; set; }
}

public static MpesaParsed ParseMpesaMessage(string message)
{
    var result = new MpesaParsed();

    // CODE
    var codeMatch = Regex.Match(message, @"\b([A-Z0-9]{6,})\b");
    if (codeMatch.Success)
        result.Code = codeMatch.Groups[1].Value;

    // AMOUNT
    var amountMatch = Regex.Match(message, @"Ksh([\d,]+\.\d{2})");
    if (amountMatch.Success)
        result.Amount = decimal.Parse(amountMatch.Groups[1].Value.Replace(",", ""));

// FULL SENDER BLOCK (FIXED CLEAN SPLIT)
var senderMatch = Regex.Match(message, @"from (.+?) on", RegexOptions.IgnoreCase);

if (senderMatch.Success)
{
    var senderRaw = senderMatch.Groups[1].Value.Trim();

    // STEP 1: remove provider part
    var cleaned = senderRaw;

    if (cleaned.Contains("-"))
    {
        cleaned = cleaned.Split('-')[1].Trim();
    }

    // STEP 2: extract phone FIRST (important fix)
    var phoneMatch = Regex.Match(cleaned, @"(0\d{9,12})");
    if (phoneMatch.Success)
    {
        result.SenderPhone = phoneMatch.Value;
        cleaned = cleaned.Replace(phoneMatch.Value, "").Trim();
    }

    // STEP 3: remaining is name
    result.SenderName = cleaned;
}
    return result;
}
private void MarkMessageAsUsedByCode(string code)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE MpesaMessages SET IsUsed = 1 WHERE MpesaCode = @code";
    cmd.Parameters.AddWithValue("@code", code);

    cmd.ExecuteNonQuery();
}
private bool MpesaCodeExists(string code)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM MpesaMessages WHERE MpesaCode = @code";
    cmd.Parameters.AddWithValue("@code", code);

    return (int)cmd.ExecuteScalar() > 0;
}
}
}