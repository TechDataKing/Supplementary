using TechKingPOS.App.Models;

public class ItemListDisplay
{
    public ItemLookup Item { get; set; }
    public bool IsRepack { get; set; }
    public RepackStockModel? Repack { get; set; }
}