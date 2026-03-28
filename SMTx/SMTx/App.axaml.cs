using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using SMTx.Eve;
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // MainWindow must exist before base.OnFrameworkInitializationCompleted(); otherwise the
            // classic desktop lifetime can exit immediately while EVE init still runs asynchronously.
            var dataService = CreateDesktopDataService();
            var viewModel = new MainViewModel(dataService);
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            _ = StartDesktopEveBackgroundAsync(viewModel);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            _ = OperatingSystem.IsAndroid()
                ? StartAndroidWithEveAsync(singleViewPlatform)
                : StartBrowserWithEveAsync(singleViewPlatform);

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartDesktopEveBackgroundAsync(MainViewModel viewModel)
    {
        try
        {
            await EveRuntime.InitializeAsync(false).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => viewModel.RefreshEveCharacters());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EVE desktop background init failed: {ex}");
        }
    }

    private static async Task StartAndroidWithEveAsync(ISingleViewApplicationLifetime singleViewPlatform)
    {
        System.Diagnostics.Debug.WriteLine("=== Android initialization started ===");
        Console.WriteLine("=== Android initialization started ===");

        try
        {
            await EveRuntime.InitializeAsync(false).ConfigureAwait(true);
            var dataService = CreateAndroidDataService();
            var viewModel = new MainViewModel(dataService);
            viewModel.RefreshEveCharacters();
            var mainView = new MainView { DataContext = viewModel };
            await Dispatcher.UIThread.InvokeAsync(() => singleViewPlatform.MainView = mainView);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== ERROR initializing Android MainView ===\n{ex}");
            Console.WriteLine($"=== ERROR initializing Android MainView ===\n{ex}");
            var dataService = new JsonDataService();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                singleViewPlatform.MainView = new MainView { DataContext = new MainViewModel(dataService) };
            });
        }
    }

    private static async Task StartBrowserWithEveAsync(ISingleViewApplicationLifetime singleViewPlatform)
    {
        System.Diagnostics.Debug.WriteLine("=== Browser initialization started ===");
        Console.WriteLine("=== Browser initialization started ===");

        try
        {
            await EveRuntime.InitializeAsync(true).ConfigureAwait(true);

            var httpClient = new System.Net.Http.HttpClient();
#pragma warning disable CA1416
            try
            {
                using var location = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis.GetPropertyAsJSObject("location");
                if (location != null)
                {
                    var origin = location.GetPropertyAsString("origin");
                    if (!string.IsNullOrEmpty(origin))
                        httpClient.BaseAddress = new Uri(origin + "/", UriKind.Absolute);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not get location from JavaScript: {ex.Message}");
                httpClient.BaseAddress = new Uri("http://localhost/", UriKind.Absolute);
            }
#pragma warning restore CA1416

            var dataService = new JsonDataService("", httpClient);
            var viewModel = new MainViewModel(dataService);
            viewModel.RefreshEveCharacters();
            var mainView = new MainView { DataContext = viewModel };
            await Dispatcher.UIThread.InvokeAsync(() => singleViewPlatform.MainView = mainView);
        }
        catch (Exception ex)
        {
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine("=== ERROR initializing MainView ===");
            errorDetails.AppendLine($"Message: {ex.Message}");
            errorDetails.AppendLine($"Type: {ex.GetType().FullName}");
            errorDetails.AppendLine($"Stack trace: {ex.StackTrace}");
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                errorDetails.AppendLine($"Inner: {inner.GetType().FullName} - {inner.Message}");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
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
            });
        }
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

    private static IDataService CreateAndroidDataService()
    {
        // On Android, we need to:
        // 1. Copy the database from assets to a writable location
        // 2. Use that location for SQLite
        
        // Get the app's data directory
        var dataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbDir = Path.Combine(dataDir, "SMTx");
        
        // Ensure directory exists
        if (!Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }
        
        var dbPath = Path.Combine(dbDir, "render.db");
        
        // Copy database from assets if it doesn't exist
        if (!File.Exists(dbPath))
        {
            System.Diagnostics.Debug.WriteLine($"Database not found at {dbPath}, attempting to copy from assets...");
            Console.WriteLine($"Database not found at {dbPath}, attempting to copy from assets...");
            
            try
            {
                // Get Android context to access assets using reflection to avoid direct dependency
                var androidAppType = Type.GetType("Android.App.Application, Mono.Android");
                if (androidAppType != null)
                {
                    var contextProperty = androidAppType.GetProperty("Context");
                    if (contextProperty != null)
                    {
                        var context = contextProperty.GetValue(null);
                        if (context != null)
                        {
                            var assetsProperty = context.GetType().GetProperty("Assets");
                            if (assetsProperty != null)
                            {
                                var assetManager = assetsProperty.GetValue(context);
                                if (assetManager != null)
                                {
                                    // Use reflection to call the helper method
                                    var helperType = Type.GetType("SMTx.Android.AndroidAssetHelper, SMTx.Android");
                                    if (helperType != null)
                                    {
                                        var copyMethod = helperType.GetMethod("CopyAssetToFile", 
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                        if (copyMethod != null)
                                        {
                                            var result = copyMethod.Invoke(null, new object[] { assetManager, "render.db", dbPath });
                                            bool copied = result is bool b && b;
                                            
                                            if (copied && File.Exists(dbPath))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Successfully copied database from assets to {dbPath}");
                                                Console.WriteLine($"Successfully copied database from assets to {dbPath}");
                                            }
                                            else
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Failed to copy database from assets. Falling back to JSON.");
                                                Console.WriteLine($"Failed to copy database from assets. Falling back to JSON.");
                                                return new JsonDataService();
                                            }
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"CopyAssetToFile method not found. Falling back to JSON.");
                                            Console.WriteLine($"CopyAssetToFile method not found. Falling back to JSON.");
                                            return new JsonDataService();
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"AndroidAssetHelper type not found. Falling back to JSON.");
                                        Console.WriteLine($"AndroidAssetHelper type not found. Falling back to JSON.");
                                        return new JsonDataService();
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"AssetManager is null. Falling back to JSON.");
                                    Console.WriteLine($"AssetManager is null. Falling back to JSON.");
                                    return new JsonDataService();
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Assets property not found. Falling back to JSON.");
                                Console.WriteLine($"Assets property not found. Falling back to JSON.");
                                return new JsonDataService();
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Android context is null. Falling back to JSON.");
                            Console.WriteLine($"Android context is null. Falling back to JSON.");
                            return new JsonDataService();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Context property not found. Falling back to JSON.");
                        Console.WriteLine($"Context property not found. Falling back to JSON.");
                        return new JsonDataService();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Android.App.Application type not found. Falling back to JSON.");
                    Console.WriteLine($"Android.App.Application type not found. Falling back to JSON.");
                    return new JsonDataService();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying database: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine($"Error copying database: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new JsonDataService();
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Database already exists at: {dbPath}");
            Console.WriteLine($"Database already exists at: {dbPath}");
        }
        
        System.Diagnostics.Debug.WriteLine($"Using database at: {dbPath}");
        Console.WriteLine($"Using database at: {dbPath}");
        
        return new SqliteDataService(dbPath);
    }
}
