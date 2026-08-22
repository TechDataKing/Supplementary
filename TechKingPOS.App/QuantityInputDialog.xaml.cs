using System;
using System.Windows;
using System.Windows.Controls;


namespace TechKingPOS.App
{
   public partial class QuantityInputDialog : Window
{
    public decimal Quantity { get; private set; }

    public QuantityInputDialog(decimal currentQuantity, decimal initialQty=0)
    {
        InitializeComponent();
        this.Left =0;
        this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
        QuantityTextBox.Text = initialQty > 0 ? initialQty.ToString() : currentQuantity.ToString();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(QuantityTextBox.Text, out decimal qty))
        {
            Quantity = qty;
            DialogResult = true;
            QuantityTextBox.Text = string.Empty; // Clear the TextBox before closing
            Close();
        }
        else
        {
            MessageBox.Show("Invalid quantity entered.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
 
}