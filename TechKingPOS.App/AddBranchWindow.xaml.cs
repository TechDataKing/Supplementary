using System.Windows;
using TechKingPOS.App.Data;
using TechKingPOS.App.Models;

namespace TechKingPOS.App
{
    public partial class AddBranchWindow : Window
    {
        public AddBranchWindow()
        {
            InitializeComponent();
        }

private void Save_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Branch name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(CodeBox.Text))
            {
                MessageBox.Show("Code is required.");
                return;
            }
        BranchRepository.Insert(
            NameBox.Text.Trim(),
            CodeBox.Text.Trim(),
            ActiveRadio.IsChecked == true
        );

        MessageBox.Show(
            "Branch saved successfully!",
            "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
        NameBox.Clear();
        CodeBox.Clear();
        ActiveRadio.IsChecked= false;
        
        DialogResult = true;
        Close();
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }
}

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
