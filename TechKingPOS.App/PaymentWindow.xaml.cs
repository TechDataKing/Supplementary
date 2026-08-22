using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TechKingPOS.App.Models;

using TechKingPOS.App.Data;


namespace TechKingPOS.App
{
    public partial class PaymentWindow : Window
    {
        private bool _isLoaded = false;
        private decimal _extraAmount = 0;

        public PaymentResult Result { get; private set; }

        private readonly decimal _total;
        private Customer? _selectedCustomer;
        private bool _allowClose = false;

        public PaymentWindow(PaymentResult payment)
        {
            InitializeComponent();

            Result = payment;
            
            _total= payment.Total;

            TotalText.Text = payment.Total.ToString("0.00");

            _isLoaded = true;
            
            ChangePanel.Visibility = Visibility.Collapsed;
            ChangeText.Text = "0.00";

        }


        // ----------------------------
        // PAYMENT METHOD CHANGED
        // UI VISIBILITY ONLY
        // ----------------------------
        private void PaymentMethodChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded)
                return;

            string method = GetSelectedMethod();

            // Default: hide all
            CashPanel.Visibility = Visibility.Collapsed;
            MpesaPanel.Visibility = Visibility.Collapsed;
            CustomerPanel.Visibility = Visibility.Collapsed;
            ChangePanel.Visibility = Visibility.Collapsed;

            switch (method)
            {
                case "Cash only":
                    CashPanel.Visibility = Visibility.Visible;
                    break;

                case "Cash + Mpesa":
                    CashPanel.Visibility = Visibility.Visible;
                    MpesaPanel.Visibility = Visibility.Visible;
                    break;

                case "Mpesa only":
                    MpesaPanel.Visibility = Visibility.Visible;
                    break;

                case "Credit":
                    CustomerPanel.Visibility = Visibility.Visible;
                    break;
            }

            UpdateCustomerPanelByAmount();
            UpdateChangeVisibility();
        }

        // ----------------------------
        // PAYMENT CHANGED
        // AUTO CREDIT VISIBILITY
        // ----------------------------
        private void PaymentChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCustomerPanelByAmount();
            UpdateChangeVisibility();
        }

        // ----------------------------
        // CONFIRM
        // BUSINESS RULES ONLY
        // ----------------------------
private void Confirm_Click(object sender, RoutedEventArgs e)
{
    decimal cash = ParseDecimal(CashTextBox.Text);
    decimal mpesa = ParseDecimal(MpesaTextBox.Text);
    decimal paid = cash + mpesa;

    bool isCredit = CustomerPanel.Visibility == Visibility.Visible;

    // ❗ Credit → customer details ONLY
    if (isCredit)
    {
        if (string.IsNullOrWhiteSpace(CustomerNameTextBox.Text) ||
            string.IsNullOrWhiteSpace(PhoneTextBox.Text))
        {
            MessageBox.Show(
                "Customer name and phone are required for credit sales.",
                "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
    }
    else
    {
        // ❗ Non-credit → must pay something
        if (paid <= 0)
        {
            MessageBox.Show(
                "Enter cash or mpesa amount.",
                "Payment Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
    }
    // 🔥 APPLY OVERPAY TO CREDIT
    if (_extraAmount > 0)
    {
        bool applyToCredit =
            GetSelectedMethod() == "Mpesa only" // 🔥 AUTO
            || ApplyCreditOption.IsChecked == true;

        if (applyToCredit)
        {
            if (_selectedCustomer == null)
            {
                MessageBox.Show("Select customer to apply credit.");
                return;
            }

            try
            {
                Result.IsDebtPayment = true;
                Result.CreditPaid = _extraAmount;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }
    }

    // ✅ ACCEPT PAYMENT
    Result.SetPayment(
        cash,
        mpesa,
        CustomerNameTextBox.Text?.Trim(),
        PhoneTextBox.Text?.Trim()
    );
    _allowClose = true;
    DialogResult = true;
    Close();
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

        // ----------------------------
        // CUSTOMER PANEL AUTO LOGIC
        // ----------------------------
private void UpdateCustomerPanelByAmount()
{
    string method = GetSelectedMethod();

    decimal cash = ParseDecimal(CashTextBox.Text);
    decimal mpesa = ParseDecimal(MpesaTextBox.Text);
    decimal paid = cash + mpesa;

    // 🔥 MPESA ONLY → NO CHANGE → AUTO CREDIT IF EXTRA
    if (method == "Mpesa only")
    {
        if (paid > _total)
        {
            CustomerPanel.Visibility = Visibility.Visible;
            return;
        }

        // normal credit behavior
        if (paid > 0 && paid < _total)
        {
            CustomerPanel.Visibility = Visibility.Visible;
        }
        else
        {
            CustomerPanel.Visibility = Visibility.Collapsed;
        }

        return;
    }

    // 🔥 OVERPAY (Cash or Cash+Mpesa) → RADIO CONTROLS
    if (paid > _total)
    {
        if (ApplyCreditOption.IsChecked == true)
        {
            CustomerPanel.Visibility = Visibility.Visible;
        }
        else
        {
            CustomerPanel.Visibility = Visibility.Collapsed;
        }
        return;
    }

    // 🔥 DIRECT CREDIT MODE
    if (method == "Credit")
    {
        CustomerPanel.Visibility = Visibility.Visible;
        return;
    }

    // 🔥 UNDERPAY → CREDIT SALE
    if (paid > 0 && paid < _total)
    {
        CustomerPanel.Visibility = Visibility.Visible;
    }
    else
    {
        CustomerPanel.Visibility = Visibility.Collapsed;
    }
}
private bool _isSelectingCustomer = false;

private void CustomerNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
{
    if (_isSelectingCustomer) return;

    string text = CustomerNameTextBox.Text;

    if (string.IsNullOrWhiteSpace(text))
    {
        CustomerPopup.IsOpen = false;
        return;
    }

    var results = CreditRepository.SearchCustomers(text);

    CustomerListBox.ItemsSource = results;
    CustomerPopup.IsOpen = results.Count > 0;
}

private void CustomerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (CustomerListBox.SelectedItem is Customer selected)
    {
        _isSelectingCustomer = true;
        _selectedCustomer=selected;

        CustomerNameTextBox.Text = selected.Name;
        PhoneTextBox.Text = selected.Phone;

        CustomerPopup.IsOpen = false;

        _isSelectingCustomer = false;
    }
}

        // ----------------------------
        // HELPERS
        // ----------------------------
        private string GetSelectedMethod()
        {
            return (PaymentMethodComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                   ?? "Cash only";
        }

        private decimal ParseDecimal(string? text)
        {
            if (decimal.TryParse(
                text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var value))
                return value;

            return 0;
        }

private void UpdateChangeVisibility()
{
    string method = GetSelectedMethod();

    decimal cash = ParseDecimal(CashTextBox.Text);
    decimal mpesa = ParseDecimal(MpesaTextBox.Text);
    decimal paid = cash + mpesa;

    decimal extra = paid - _total;

    // 🔥 MPESA ONLY → NO CHANGE EVER
    if (method == "Mpesa only")
    {
        ChangePanel.Visibility = Visibility.Collapsed;

        if (extra > 0)
        {
            _extraAmount = extra;

            // 🔥 force credit behavior
            OverpayPanel.Visibility = Visibility.Collapsed;
            CustomerPanel.Visibility = Visibility.Visible;
        }
        else
        {
            _extraAmount = 0;
            OverpayPanel.Visibility = Visibility.Collapsed;
        }

        return;
    }

    // 🔥 CASH / CASH+MPESA
    bool allowsChange =
        method == "Cash only" ||
        method == "Cash + Mpesa";

    if (!allowsChange)
    {
        ChangePanel.Visibility = Visibility.Collapsed;
        OverpayPanel.Visibility = Visibility.Collapsed;
        return;
    }

    if (extra > 0)
    {
        _extraAmount = extra;

        OverpayPanel.Visibility = Visibility.Visible;

        if (ReturnChangeOption.IsChecked == true)
        {
            ChangePanel.Visibility = Visibility.Visible;
            ChangeText.Text = extra.ToString("0.00");

            CustomerPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ChangePanel.Visibility = Visibility.Collapsed;
            CustomerPanel.Visibility = Visibility.Visible;
        }
    }
    else
    {
        _extraAmount = 0;

        ChangePanel.Visibility = Visibility.Collapsed;
        OverpayPanel.Visibility = Visibility.Collapsed;
    }
}

private void OverpayOptionChanged(object sender, RoutedEventArgs e)
{
    if (_extraAmount <= 0)
        return;

    if (ApplyCreditOption.IsChecked == true)
    {
        // 👉 CREDIT MODE
        ChangePanel.Visibility = Visibility.Collapsed;
        CustomerPanel.Visibility = Visibility.Visible;
    }
    else
    {
        // 👉 CHANGE MODE
        ChangePanel.Visibility = Visibility.Visible;
        ChangeText.Text = _extraAmount.ToString("0.00");

        // only show customer if needed for underpayment or credit method
        UpdateCustomerPanelByAmount();
    }
}
    }
}
