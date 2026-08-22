using TechKingPOS.App.Infrastructure;
using System;
using System.Windows;

namespace TechKingPOS.App.Services
{
    public abstract class RefreshOnFocusWindow : Window
{
    private long _lastVersion = -1;

    protected RefreshOnFocusWindow()
    {
        Activated += OnActivated;
    }

    private void OnActivated(object sender, EventArgs e)
    {
        CheckRefresh();
    }

    protected void CheckRefresh()
    {
        if (_lastVersion != AppState.GlobalVersion)
        {
            _lastVersion = AppState.GlobalVersion;
            Refresh();
        }
    }

    protected abstract void Refresh();
}
}
