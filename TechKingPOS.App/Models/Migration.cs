public class Migration
{
    public string Key { get; }
    public string Sql { get; }

    public Migration(string key, string sql)
    {
        Key = key;
        Sql = sql;
    }
}