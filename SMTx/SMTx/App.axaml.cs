using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SMTx.Services;
using SMTx.ViewModels;
using SMTx.Views;

namespace SMTx;

public partial class App : Application
{
    public override void Initialize()
    {
        System.Diagnostics.Debug.WriteLine("App.Initialize() called");
        Console.WriteLine("App.Initialize() called");
        AvaloniaXamlLoader.Load(this);
        System.Diagnostics.Debug.WriteLine("AvaloniaXamlLoader.Load() completed");
        Console.WriteLine("AvaloniaXamlLoader.Load() completed");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IDataService dataService;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Desktop: Use SQLite
            dataService = CreateDesktopDataService();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(dataService)
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            // Browser/Mobile: Use JSON
            System.Diagnostics.Debug.WriteLine("=== Browser initialization started ===");
            Console.WriteLine("=== Browser initialization started ===");
            
            // First, try to create a simple test view to verify initialization works
            try
            {
                System.Diagnostics.Debug.WriteLine("Creating test view first...");
                Console.WriteLine("Creating test view first...");
                
                // Create a simple test view that shows immediately
                var testView = new Border
                {
                    Background = Brushes.DarkBlue,
                    Child = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Avalonia Browser App Initialized!",
                                FontSize = 24,
                                Foreground = Brushes.White,
                                Margin = new Thickness(0, 0, 0, 20)
                            },
                            new TextBlock
                            {
                                Text = "Loading main view...",
                                Foreground = Brushes.White
                            }
                        }
                    }
                };
                
                singleViewPlatform.MainView = testView;
                System.Diagnostics.Debug.WriteLine("Test view set, now creating MainView...");
                Console.WriteLine("Test view set, now creating MainView...");
                
                // Now try to create the real MainView
                // Create HttpClient with base address for browser
                // Use JavaScript interop to get the current origin
                var httpClient = new System.Net.Http.HttpClient();
                
#pragma warning disable CA1416 // This code only runs in browser platform
                try
                {
                    // Get the current location from JavaScript
                    using var location = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis.GetPropertyAsJSObject("location");
                    
                    if (location != null)
                    {
                        var origin = location.GetPropertyAsString("origin");
                        if (!string.IsNullOrEmpty(origin))
                        {
                            httpClient.BaseAddress = new Uri(origin + "/", UriKind.Absolute);
                            System.Diagnostics.Debug.WriteLine($"Set BaseAddress to: {httpClient.BaseAddress}");
                            Console.WriteLine($"Set BaseAddress to: {httpClient.BaseAddress}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not get location from JavaScript: {ex.Message}");
                    Console.WriteLine($"Could not get location from JavaScript: {ex.Message}");
                    // Fallback: try to construct from a default
                    // In development, this is usually http://localhost:port
                    httpClient.BaseAddress = new Uri("http://localhost/", UriKind.Absolute);
                }
#pragma warning restore CA1416
                
                dataService = new JsonDataService("", httpClient);
                System.Diagnostics.Debug.WriteLine("Creating MainViewModel...");
                Console.WriteLine("Creating MainViewModel...");
                
                var viewModel = new MainViewModel(dataService);
                System.Diagnostics.Debug.WriteLine("MainViewModel created, creating MainView...");
                Console.WriteLine("MainViewModel created, creating MainView...");
                
                var mainView = new MainView
                {
                    DataContext = viewModel
                };
                System.Diagnostics.Debug.WriteLine("MainView created, setting MainView...");
                Console.WriteLine("MainView created, setting MainView...");
                
                singleViewPlatform.MainView = mainView;
                System.Diagnostics.Debug.WriteLine("=== MainView set successfully ===");
                Console.WriteLine("=== MainView set successfully ===");
            }
            catch (Exception ex)
            {
                var errorDetails = new System.Text.StringBuilder();
                errorDetails.AppendLine($"=== ERROR initializing MainView ===");
                errorDetails.AppendLine($"Message: {ex.Message}");
                errorDetails.AppendLine($"Type: {ex.GetType().FullName}");
                errorDetails.AppendLine($"Stack trace: {ex.StackTrace}");
                
                System.Diagnostics.Debug.WriteLine(errorDetails.ToString());
                Console.WriteLine(errorDetails.ToString());
                
                Exception? inner = ex.InnerException;
                int depth = 0;
                while (inner != null && depth < 5)
                {
                    errorDetails.AppendLine($"\nInner exception #{depth + 1}:");
                    errorDetails.AppendLine($"  Type: {inner.GetType().FullName}");
                    errorDetails.AppendLine($"  Message: {inner.Message}");
                    errorDetails.AppendLine($"  Stack: {inner.StackTrace}");
                    
                    System.Diagnostics.Debug.WriteLine($"Inner exception #{depth + 1}: {inner.GetType().FullName} - {inner.Message}");
                    Console.WriteLine($"Inner exception #{depth + 1}: {inner.GetType().FullName} - {inner.Message}");
                    
                    inner = inner.InnerException;
                    depth++;
                }
                
                // Create a simple error view as fallback
                singleViewPlatform.MainView = new Border
                {
                    Background = Brushes.DarkRed,
                    Child = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = errorDetails.ToString(),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(20),
                            Foreground = Brushes.White,
                            FontFamily = new Avalonia.Media.FontFamily("Consolas, monospace")
                        }
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IDataService CreateDesktopDataService()
    {
        // Try to find the database path relative to workspace root
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var workspaceRoot = appDirectory;
        
        var directory = new DirectoryInfo(appDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "DataExport")))
        {
            directory = directory.Parent;
        }
        
        if (directory != null)
        {
            workspaceRoot = directory.FullName;
        }
        
        var dbPath = Path.Combine(workspaceRoot, "DataExport", "3142455", "render.db");
        
        if (!File.Exists(dbPath))
        {
            var altPath = Path.Combine("DataExport", "3142455", "render.db");
            if (File.Exists(altPath))
            {
                dbPath = altPath;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Database not found. Searched: {dbPath} and {altPath}");
                // Fallback to JSON
                return new JsonDataService();
            }
        }

        return new SqliteDataService(dbPath);
    }
}
