using System.ComponentModel;

public class SupplierReturnItem : INotifyPropertyChanged
{
    public int Id { get; set; }

    public string ItemName { get; set; }

    public decimal Stock { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Price { get; set; }
    public decimal MarkedPrice { get; set; }     // MP
    public decimal SellingPrice { get; set; }    // SP

    private decimal _returnQty;
    public decimal ReturnQty
    {
        get => _returnQty;
        set
        {
            _returnQty = value;
            OnPropertyChanged(nameof(ReturnQty));
        }
    }

    private string _reason;
    public string Reason
    {
        get => _reason;
        set
        {
            _reason = value;
            OnPropertyChanged(nameof(Reason));
        }
    }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            OnPropertyChanged(nameof(Selected));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}