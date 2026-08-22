using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using TechKingPOS.App.Data;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Infrastructure;


namespace TechKingPOS.App
{
    

public partial class CalculatorWindow : Window
{
    public CalculatorWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        DisplayBox.Text += button.Content.ToString();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        DisplayBox.Text = "";
    }

    private void Equals_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = new DataTable().Compute(DisplayBox.Text, null);
            DisplayBox.Text = result.ToString();
        }
        catch
        {
            DisplayBox.Text = "Error";
        }
    }
}
}