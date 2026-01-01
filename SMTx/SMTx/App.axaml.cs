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
            // Check if we're on Android or Browser
            bool isAndroid = OperatingSystem.IsAndroid();
            
            if (isAndroid)
            {
                // Android: Use SQLite
                System.Diagnostics.Debug.WriteLine("=== Android initialization started ===");
                Console.WriteLine("=== Android initialization started ===");
                
                try
                {
                    dataService = CreateAndroidDataService();
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
                    System.Diagnostics.Debug.WriteLine($"=== ERROR initializing Android MainView ===");
                    System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    Console.WriteLine($"=== ERROR initializing Android MainView ===");
                    Console.WriteLine($"Message: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    
                    // Fallback to JSON on error
                    dataService = new JsonDataService();
                    singleViewPlatform.MainView = new MainView
                    {
                        DataContext = new MainViewModel(dataService)
                    };
                }
            }
            else
            {
                // Browser: Use JSON
                System.Diagnostics.Debug.WriteLine("=== Browser initialization started ===");
                Console.WriteLine("=== Browser initialization started ===");
                
                try
                {
                    // Create HttpClient with base address for browser
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
