using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TechKingPOS.App.Models
{
    public class SaleItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int _itemId;
        public int ItemId
        {
            get => _itemId;
            set { _itemId = value; OnChanged(); }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnChanged();
                OnChanged(nameof(Display));
            }
        }

        private string _alias = string.Empty;
        public string Alias
        {
            get => _alias;
            set { _alias = value; OnChanged(); }
        }

        private string _unitType = string.Empty;
        public string UnitType
        {
            get => _unitType;
            set
            {
                _unitType = value;
                OnChanged();
                OnChanged(nameof(Display));
            }
        }

        private decimal _markedPrice;
        public decimal MarkedPrice
        {
            get => _markedPrice;
            set { _markedPrice = value; OnChanged(); }
        }

        private decimal _unitValue;
        public decimal UnitValue
        {
            get => _unitValue;
            set { _unitValue = value; OnChanged(); }
        }

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnChanged();
                OnChanged(nameof(Total));   // 🔥 IMPORTANT
                OnChanged(nameof(SubTotal));
            }
        }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnChanged();
                OnChanged(nameof(Total));   // 🔥 IMPORTANT
                OnChanged(nameof(SubTotal));
            }
        }

        public int BranchId { get; set; }

        public decimal CostPrice { get; set; }
        public decimal Profit { get; set; }
        public decimal costTotal { get; set; }

        public bool IsRepack { get; set; }

        private string _saleType = string.Empty;
        public string SaleType
        {
            get => _saleType;
            set { _saleType = value; OnChanged(); }
        }

        public decimal OriginalPrice { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public int? RepackId { get; set; }

        public decimal RepackAvailableQty { get; set; }

        public decimal SubTotal => Quantity * Price;
        public decimal AvailableQuantity { get; set; }

        public decimal Total => Quantity * Price;

        public string Display => $"{Name} {UnitType}";

        protected void OnChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}