using System.Windows.Threading;
using System.Windows;

namespace TechKingPOS.App.Services;

public class SyncManager
{
    private readonly DispatcherTimer timer =
        new();

    public void Start()
    {
        timer.Interval = TimeSpan.FromSeconds(10);

        timer.Tick += async (_, _) =>
        {
            //MessageBox.Show("Sync Running");

            await SyncService.SyncPendingAsync();
        };

        timer.Start();
    }
}