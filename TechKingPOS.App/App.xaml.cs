using System;
using System.Reflection;
using System.Windows;
using TechKingPOS.App.Data;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Models;

namespace TechKingPOS.App
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            // Global crash handlers...
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                if (ex.ExceptionObject is Exception exception)
                    LogFatal("APPDOMAIN", exception);

                MessageBox.Show(
                    ex.ExceptionObject.ToString(),
                    "❌ Application Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            };

            DispatcherUnhandledException += (s, ex) =>
            {
                LogFatal("DISPATCHER", ex.Exception);

                MessageBox.Show(
                    ex.Exception.ToString(),
                    "❌ UI Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ex.Handled = true;
            };

            base.OnStartup(e);
            //SettingsCache.Load();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load services
            try
            {
                DbService.Initialize();
                BranchService.Load();
                SettingsCache.Load();
            }
            catch (Exception ex)
            {
                LogFatal("STARTUP", ex);

                if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    string message;

                    switch (sqlEx.Number)
                    {
                        case 4060:
                            message =
            @"Unable to connect to the database.

            Possible reasons:

            • SQL Server service is not running.
            • The database has not been created yet.
            • SQL Server Express is not installed.

            Please start SQL Server and try again.

            If this is the first installation, create the database before opening the application.";
                            break;

                        case 53:
                        case 2:
                            message =
            @"Cannot find the SQL Server instance.

            Please ensure:

            • SQL Server Express is installed.
            • The SQL Server service is running.
            • The server name is correct.

            Current Server:
            .\SQLEXPRESS";
                            break;

                        case 18456:
                            message =
            @"Unable to log in to SQL Server.

            Please verify that Windows Authentication is enabled
            and that your account has permission to access SQL Server.";
                            break;

                        default:
                            message =
            $@"Database Error

            SQL Error: {sqlEx.Number}

            {sqlEx.Message}";
                            break;
                    }

                    MessageBox.Show(
                        message,
                        "Database Connection Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    Shutdown();
                    return;
                }

                // MessageBox.Show(
                //     ex.Message,
                //     "Startup Error",
                //     MessageBoxButton.OK,
                //     MessageBoxImage.Error);
                    MessageBox.Show(ex.StackTrace);

                Shutdown();
            }

            // 🔄 UPDATE CHECK (async-friendly)
            try
            {
                var updateResult = await UpdateService.CheckForUpdateAsync();

                if (updateResult.IsUpdateAvailable)
                {
                    var updateWindow = new UpdateWindow(
                        UpdateService.GetCurrentVersion(),
                        updateResult.LatestVersion
                    );

                    var userAccepted = updateWindow.ShowDialog();

                    if (userAccepted == true)
                    {
                        // UpdateWindow handles download + restart, so exit
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogFatal("UPDATE", ex);
            }
            if (!BusinessRepository.IsRegistered())
            {
                var win = new BusinessRegistrationWindow();

                bool? result = win.ShowDialog();

                if (result != true)
                {
                    Shutdown();
                    return;
                }
            }

            // ================= LICENSE CHECK =================
            var settings = SettingsCache.Current;
            var status = LicenseService.GetStatus(settings);

            if (status == LicenseStatus.Expired)
            {
                MessageBox.Show(
                    "Your license has expired. You can only access Settings to activate a new license.",
                    "License Expired",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                // Only open Settings window
                MainWindow = new SettingsWindow();
                MainWindow.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                return;
            }

            // NORMAL APP STARTUP
            if (settings.RequireLogin)
                MainWindow = new LoginWindow();
            else
            {
                SessionService.LoginAsGuest();
                MainWindow = new MainWindow();
            }

            MainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        private static void LogFatal(string source, Exception ex)
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TechKingPOS",
                    "crash"
                );

                System.IO.Directory.CreateDirectory(dir);

                var file = System.IO.Path.Combine(
                    dir,
                    $"{source}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                );

                System.IO.File.WriteAllText(file, ex.ToString());
            }
            catch
            {
                // never throw from logger
            }
        }
    }
}
