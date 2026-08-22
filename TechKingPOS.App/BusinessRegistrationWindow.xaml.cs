using System.Net.Http;
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
using TechKingPOS.App.Security;
//using TechKingPOS.App.Data;

namespace TechKingPOS.App
{
    public partial class BusinessRegistrationWindow : Window
    {
        public BusinessRegistrationWindow()

        {
            InitializeComponent();

            BusinessNameTextBox.Tag = BusinessNamePlaceholder;
            OwnerTextBox.Tag = OwnerPlaceholder;
            PhoneTextBox.Tag = PhonePlaceholder;
            EmailTextBox.Tag = EmailPlaceholder;
            TownTextBox.Tag = TownPlaceholder;
            AddressTextBox.Tag = AddressPlaceholder;
        }
private BusinessRegistrationModel GetForm()
{
    return new BusinessRegistrationModel
    {
        BusinessName = BusinessNameTextBox.Text.Trim(),
        OwnerName = OwnerTextBox.Text.Trim(),
        Phone = PhoneTextBox.Text.Trim(),
        Email = EmailTextBox.Text.Trim(),
        Town = TownTextBox.Text.Trim(),
        Address = AddressTextBox.Text.Trim(),
        Password = PasswordTextBox.Password,
        ConfirmPassword = ConfirmTextBox.Password
    };
}
private bool ValidateForm(BusinessRegistrationModel model)
{
    if (string.IsNullOrWhiteSpace(model.BusinessName))
    {
        MessageBox.Show("Business name is required.");
        BusinessNameTextBox.Focus();
        return false;
    }

    if (model.BusinessName.Length < 3)
    {
        MessageBox.Show("Business name is too short.");
        BusinessNameTextBox.Focus();
        return false;
    }

    if (string.IsNullOrWhiteSpace(model.OwnerName))
    {
        MessageBox.Show("Owner name is required.");
        OwnerTextBox.Focus();
        return false;
    }

    if (string.IsNullOrWhiteSpace(model.Phone))
    {
        MessageBox.Show("Phone number is required.");
        PhoneTextBox.Focus();
        return false;
    }

    if (!System.Text.RegularExpressions.Regex.IsMatch(model.Phone, @"^(07|01)\d{8}$"))
    {
        MessageBox.Show("Enter a valid Kenyan phone number.");
        PhoneTextBox.Focus();
        return false;
    }

    if (!string.IsNullOrWhiteSpace(model.Email))
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(model.Email);
        }
        catch
        {
            MessageBox.Show("Enter a valid email address.");
            EmailTextBox.Focus();
            return false;
        }
    }

    if (string.IsNullOrWhiteSpace(model.Town))
    {
        MessageBox.Show("Town/City is required.");
        TownTextBox.Focus();
        return false;
    }

    if (string.IsNullOrWhiteSpace(model.Address))
    {
        MessageBox.Show("Business address is required.");
        AddressTextBox.Focus();
        return false;
    }

    if (string.IsNullOrWhiteSpace(model.Password))
    {
        MessageBox.Show("Password is required.");
        PasswordTextBox.Focus();
        return false;
    }

    if (model.Password.Length < 6)
    {
        MessageBox.Show("Password must be at least 6 characters.");
        PasswordTextBox.Focus();
        return false;
    }

    if (model.Password != model.ConfirmPassword)
    {
        MessageBox.Show("Passwords do not match.");
        ConfirmTextBox.Focus();
        return false;
    }

    return true;
}
private void ClearForm()
{
    BusinessNameTextBox.Clear();
    OwnerTextBox.Clear();
    PhoneTextBox.Clear();
    EmailTextBox.Clear();
    TownTextBox.Clear();
    AddressTextBox.Clear();

    PasswordTextBox.Clear();
    ConfirmTextBox.Clear();

    BusinessNameTextBox.Focus();
}
private void SetStatus(string message, Brush color)
{
    StatusText.Text = message;
    StatusText.Foreground = color;
}
private async void RegisterButton_Click(object sender, RoutedEventArgs e)
{
    RegisterButton.IsEnabled = false;
    RegisterButton.Content = "⏳ Registering...";

    try
    {
        var model = GetForm();

        if (!ValidateForm(model))
            return;

        SetStatus("Checking internet...", Brushes.Gray);

        if (!await HasInternetAsync())
        {
            SetStatus("No internet connection.", Brushes.Red);

            MessageBox.Show(
                "Internet connection is required to register your business.");

            return;
        }

        SetStatus("Connecting to TechKing Cloud...", Brushes.Blue);

        var response = await BusinessApiService.RegisterAsync(model);

        if (!response.Success)
        {
            SetStatus(response.Message, Brushes.Red);

            MessageBox.Show(response.Message);

            return;
        }

        SetStatus("Saving business locally...", Brushes.Blue);

       
         BusinessRepository.Save(response, model);
         CloudSettingsRepository.Save(response.BusinessCode,response.BusinessId,SessionContext.EffectiveBranchId);
         ClearForm();

        SetStatus("Registration completed successfully.", Brushes.Green);
        MessageBox.Show(
            "Business registered successfully.",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        DialogResult = true;
        Close();

    }
    catch (Exception ex)
    {
        SetStatus(ex.Message, Brushes.Red);

        MessageBox.Show(
            ex.ToString(),
            "Registration Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
        finally
    {
        RegisterButton.IsEnabled = true;
        RegisterButton.Content = "🚀 Register Business";
    }
}
private void Placeholder_TextChanged(object sender, TextChangedEventArgs e)
{
    if (sender is not TextBox textBox)
        return;

    if (textBox.Tag is not TextBlock placeholder)
        return;

    placeholder.Visibility =
        string.IsNullOrWhiteSpace(textBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
}

private void Placeholder_GotFocus(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox textBox)
        return;

    if (textBox.Tag is not TextBlock placeholder)
        return;

    placeholder.Visibility = Visibility.Collapsed;
}

private void Placeholder_LostFocus(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox textBox)
        return;

    if (textBox.Tag is not TextBlock placeholder)
        return;

    placeholder.Visibility =
        string.IsNullOrWhiteSpace(textBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
}
private async Task<bool> HasInternetAsync()
{
    try
    {
        using HttpClient client = new();

        client.Timeout = TimeSpan.FromSeconds(5);

        await client.GetAsync("https://www.google.com");

        return true;
    }
    catch
    {
        return false;
    }
}


    }
}