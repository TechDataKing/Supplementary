using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using TechKingPOS.App.Data;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
//using TechKingPOS.App.Data;

namespace TechKingPOS.App
{
    public partial class PendingWindow : Window
    {
        private List<MpesaMessage> Messages = new();
        private List<PendingSale> PendingSales = new();
        public List<int> UsedMessageIds { get; private set; } = new();

        DispatcherTimer timer = new DispatcherTimer();
        private string _targetReceipt;
        private bool _allowClose = false;
        public event Action PaymentCompleted;
        public string MpesaCodes { get; private set; } = "";
        private decimal _saleTotal;
        private bool _autoMode = true;
        public enum PaymentMode
        {
            Cash,
            Mpesa,
            Mixed,
            Credit
        }

        private PaymentMode CurrentMode = PaymentMode.Mpesa;

        private PaymentResult _payment;

        public PendingWindow(string receipt,PaymentResult payment)
        {
            InitializeComponent();

            _targetReceipt = receipt;
            _payment = payment;

            _saleTotal = payment.Total;

            TotalText.Text = payment.Total.ToString("N2");
            TxtMpesaAmount.Text = payment.Total.ToString("N2");

            LoadData();

            timer.Interval = TimeSpan.FromSeconds(10);
            timer.Tick += (s, e) => LoadData();
            timer.Start();
        }

        // 🔥 LOAD DATA INTO GRIDS
        private void LoadData()
        {   
            try{
            Messages = MpesaRepository.GetUnusedMessages();
            PendingSales = SalesRepository.GetPendingSales();

            MessagesGrid.ItemsSource = Messages;

            TotalText.Text = _payment.Total.ToString("N2");
            TxtMpesaAmount.Text = _payment.Total.ToString("N2");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load data: " + ex.Message);
            }
            UpdatePaymentSummary();
        }

private void Match_Click(object sender, RoutedEventArgs e)
{

    // ================= GET PAYMENTS =================

    decimal cashPaid = 0m;
    decimal.TryParse(CashBox.Text, out cashPaid);

    var selectedMessages = Messages
        .Where(x => x.IsSelected)
        .ToList();

    decimal mpesaPaid = selectedMessages.Sum(x => x.Amount);

    decimal amountPaid = cashPaid + mpesaPaid;
    UsedMessageIds = selectedMessages
            .Select(x => x.Id)
            .ToList();

    // ================= VALIDATION =================

    switch (CurrentMode)
    {
        case PaymentMode.Cash:

            if (cashPaid <= 0)
            {
                MessageBox.Show("Enter cash amount.");
                return;
            }

            if (cashPaid < _payment.Total)
            {
                MessageBox.Show(
                    $"Customer still owes Ksh {(_payment.Total - cashPaid):N2}");
                return;
            }
            if (!decimal.TryParse(CashBox.Text, out decimal cashGiven) || cashGiven <= 0)
            {
                MessageBox.Show("Enter a valid cash amount.");
                return;
            }

            amountPaid = cashPaid;
            break;

        case PaymentMode.Mpesa:
            if (_autoMode)
            {

                if (!selectedMessages.Any())
                {
                    MessageBox.Show("Select one or more Mpesa messages.");
                    return;
                }

                if (mpesaPaid < _payment.Total)
                {
                    MessageBox.Show(
                        $"Customer still owes Ksh {(_payment.Total - mpesaPaid):N2}");
                    return;
                }

                amountPaid = mpesaPaid;
            }
            else
            {
                // 🔹 MANUAL MODE
                string mpesaCode = TxtMpesaCode.Text;

                if (string.IsNullOrWhiteSpace(mpesaCode))
                {
                    MessageBox.Show("Enter MPESA code.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (mpesaCode.Length < 10)
                {
                    MessageBox.Show("Invalid MPESA code.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            break;

        case PaymentMode.Mixed:

            if (cashPaid <= 0 && mpesaPaid <= 0)
            {
                MessageBox.Show("Enter payment.");
                return;
            }

            if (amountPaid < _payment.Total)
            {
                MessageBox.Show(
                    $"Customer still owes Ksh {(_payment.Total - amountPaid):N2}");
                return;
            }

            break;
    }

    // ================= CHANGE =================

    decimal change = Math.Max(amountPaid - _payment.Total, 0);

    if (change > 0)
    {
        MessageBox.Show($"Customer change is Ksh {change:N2}");
    }

    // ================= MPESA CODES =================

    List<string> codes = new();

    if (CurrentMode != PaymentMode.Cash)
    {
        if (_autoMode)
        {
            codes = selectedMessages
                .Select(x => x.Code)
                .ToList();
        }
        else
        {
            codes.Add(TxtMpesaCode.Text.Trim());
        }
    }

    MpesaCodes = string.Join(",", codes);


    MessageBox.Show("Sale completed successfully.");

    PendingSaleStore.Completed = true;

    PaymentCompleted?.Invoke();

    _allowClose = true;

    Close();
}
private void UpdatePaymentSummary()
{
    decimal cashPaid = 0m;
    decimal mpesaPaid = 0m;

    // Cash amount
    if (CurrentMode == PaymentMode.Cash ||
        CurrentMode == PaymentMode.Mixed)
    {
        decimal.TryParse(CashBox.Text, out cashPaid);
    }

    // Mpesa amount
    if (CurrentMode == PaymentMode.Mpesa ||
        CurrentMode == PaymentMode.Mixed)
    {
        mpesaPaid = Messages
            .Where(x => x.IsSelected)
            .Sum(x => x.Amount);
    }

    decimal amountPaid = cashPaid + mpesaPaid;

    decimal change = 0m;
    decimal balance = 0m;

    if (amountPaid >= _saleTotal)
    {
        change = amountPaid - _saleTotal;
    }
    else
    {
        balance = _saleTotal - amountPaid;
    }

    TotalText.Text = _saleTotal.ToString("N2");
    TxtMpesaAmount.Text = _saleTotal.ToString("N2");
    PaidText.Text = amountPaid.ToString("N2");
    ChangeText.Text = change.ToString("N2");
    BalanceText.Text = balance.ToString("N2");
}
private void CashBox_TextChanged(object sender, TextChangedEventArgs e)
{
    UpdatePaymentSummary();
}
private void PaymentMode_Checked(object sender, RoutedEventArgs e)
{
    CashPanel.Visibility = Visibility.Collapsed;
    AutoPanel.Visibility = Visibility.Collapsed;

    if (CashRadio.IsChecked == true)
    {
        CurrentMode = PaymentMode.Cash;
        CashPanel.Visibility = Visibility.Visible;
    }
    else if (MpesaRadio.IsChecked == true)
    {
        CurrentMode = PaymentMode.Mpesa;
        AutoPanel.Visibility = Visibility.Visible;
    }
    else if (MixedRadio.IsChecked == true)
    {
        CurrentMode = PaymentMode.Mixed;

        CashPanel.Visibility = Visibility.Visible;
        AutoPanel.Visibility = Visibility.Visible;
        ManualPanel.Visibility = Visibility.Collapsed;
    }
    else
    {
        CurrentMode = PaymentMode.Credit;
    }

    UpdatePaymentSummary();
}
private void CashRadio_Checked(object sender, RoutedEventArgs e)
{
    CurrentMode = PaymentMode.Cash;

    CashPanel.Visibility = Visibility.Visible;
    AutoPanel.Visibility = Visibility.Collapsed;
    ManualPanel.Visibility = Visibility.Collapsed;

    UpdatePaymentSummary();
}

private void MpesaRadio_Checked(object sender, RoutedEventArgs e)
{
    CurrentMode = PaymentMode.Mpesa;

    if (_autoMode)
    {
        CashPanel.Visibility = Visibility.Collapsed;
        AutoPanel.Visibility = Visibility.Visible;
        ManualPanel.Visibility = Visibility.Collapsed;
        
    }
    else
    {
      CashPanel.Visibility = Visibility.Collapsed;
      AutoPanel.Visibility = Visibility.Collapsed;
      ManualPanel.Visibility = Visibility.Visible;  
    }

    UpdatePaymentSummary();
}

private void MixedRadio_Checked(object sender, RoutedEventArgs e)
{
    CurrentMode = PaymentMode.Mixed;

    CashPanel.Visibility = Visibility.Visible;
    AutoPanel.Visibility = Visibility.Visible;
    ManualPanel.Visibility = Visibility.Collapsed;

    UpdatePaymentSummary();
}

public void Manual_Click(object sender, RoutedEventArgs e)
{
    _autoMode=false;
    
        CashPanel.Visibility = Visibility.Collapsed;
        AutoPanel.Visibility = Visibility.Collapsed;
        ManualPanel.Visibility = Visibility.Visible;

    UpdatePaymentSummary();
}
public void Auto_Click(object sender, RoutedEventArgs e)
{
    _autoMode=true;
    
        CashPanel.Visibility = Visibility.Collapsed;
        AutoPanel.Visibility = Visibility.Visible;
        ManualPanel.Visibility = Visibility.Collapsed;
    
    UpdatePaymentSummary();
}

private void MessagesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    DependencyObject obj = (DependencyObject)e.OriginalSource;

    while (obj != null && obj is not DataGridRow)
        obj = VisualTreeHelper.GetParent(obj);

    if (obj is DataGridRow row && row.Item is MpesaMessage msg)
    {
        msg.IsSelected = !msg.IsSelected;

        MessagesGrid.Items.Refresh();

        UpdatePaymentSummary();

        e.Handled = true;
    }
}
protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
{
    if (!_allowClose)
    {
        e.Cancel = true;
        MessageBox.Show("Complete the sale or click Cancel to return to Sales.");
        return;
    }

    base.OnClosing(e);
}


private void Cancel_Click(object sender, RoutedEventArgs e)
{
    _allowClose = true;

    Owner?.Show();

    Close();
}
        // 🔥 MARK MESSAGE USED

        protected override void OnClosed(EventArgs e)
        {
            timer.Stop();
            base.OnClosed(e);
        }
    }
}