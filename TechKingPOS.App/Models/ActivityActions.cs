public static class ActivityActions
{
    public const string ADD_ITEM = "ADD_ITEM";

    public const string SALE = "SALE"; // high-level
    public const string ITEM_SOLD = "ITEM_SOLD";
    public const string DAMAGE = "DAMAGE";
    public const string REPACK_OUT = "REPACK_OUT"; // item → repack
    public const string REPACK_IN = "REPACK_IN";   // repack created
    public const string CUSTOMER_RETURN = "CUSTOMER_RETURN";
    public const string CUSTOMER_RETURN_RESTOCK = "CUSTOMER_RETURN_RESTOCK";
    public const string SUPPLIER_RETURN = "SUPPLIER_RETURN";
    public const string STOCK_ADJUSTED = "STOCK_ADJUSTED";
    public const string STOCK_OUT = "STOCK_OUT";
}