namespace TechKingPOS.App.Infrastructure
{
    public static class AppState
    {
        public static long GlobalVersion { get; private set; }

        public static void NotifyDataChanged()
        {
            GlobalVersion++;
        }
    }
}
