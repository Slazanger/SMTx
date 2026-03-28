using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EVEStandard.Models;
using ReactiveUI;
using System.Reactive.Linq;
using SMTx.Eve;
using SMTx.Models;
using SMTx.Services;

namespace SMTx.ViewModels;

public sealed class EvePilotRow : ViewModelBase
{
    public EvePilotRow(long id, string name)
    {
        CharacterId = id;
        Name = name;
    }

    public long CharacterId { get; }
    public string Name { get; }
}

public class MainViewModel : ViewModelBase
{
    private ObservableCollection<RenderSolarSystem> _solarSystems = new();
    private List<StargateLink> _stargateLinks = new();
    private readonly IDataService _dataService;
    
    // 3D Camera properties
    private double _cameraDistance = 20000.0;
    private double _cameraRotationX = 0.0; // Rotation around X axis (pitch) in radians
    private double _cameraRotationY = 0.0; // Rotation around Y axis (yaw) in radians
    private double _cameraRotationZ = 0.0; // Rotation around Z axis (roll) in radians
    private double _cameraCenterX = 0.0;
    private double _cameraCenterY = 0.0;
    private double _cameraCenterZ = 0.0;

    public double CameraCenterX
    {
        get => _cameraCenterX;
        set => this.RaiseAndSetIfChanged(ref _cameraCenterX, value);
    }
    
    public double CameraCenterY
    {
        get => _cameraCenterY;
        set => this.RaiseAndSetIfChanged(ref _cameraCenterY, value);
    }
    
    public double CameraCenterZ
    {
        get => _cameraCenterZ;
        set => this.RaiseAndSetIfChanged(ref _cameraCenterZ, value);
    }
    private double _fieldOfView = Math.PI / 4.0; // 45 degrees
    
    // Canvas size for 1:1 zoom calculation
    private double _canvasWidth = 800.0;
    private double _canvasHeight = 600.0;

    public ICommand ResetViewCommand { get; }
    public ICommand AddEveCharacterCommand { get; }
    public ICommand RemoveSelectedEveCharacterCommand { get; }
    public ICommand LogoutAllEveCommand { get; }
    public ICommand ProbeEsiCommand { get; }

    private ObservableCollection<EvePilotRow> _evePilots = new();
    private EvePilotRow? _selectedEvePilot;
    private string _eveStatus = "";
    private CancellationTokenSource? _detailLoadCts;
    private readonly HttpClient _eveImageHttp = CreateEveImageHttpClient();

    public EveCharacterDetailViewModel Detail { get; } = new();

    public bool IsEveAvailable => EveRuntime.IsAvailable;

    public bool IsEveUnavailable => !IsEveAvailable;

    public bool HasEvePilotSelection => SelectedEvePilot != null;

    public ObservableCollection<EvePilotRow> EvePilots
    {
        get => _evePilots;
        set => this.RaiseAndSetIfChanged(ref _evePilots, value);
    }

    public EvePilotRow? SelectedEvePilot
    {
        get => _selectedEvePilot;
        set => this.RaiseAndSetIfChanged(ref _selectedEvePilot, value);
    }

    public string EveStatus
    {
        get => _eveStatus;
        set => this.RaiseAndSetIfChanged(ref _eveStatus, value);
    }

    private MainShellTab _selectedShellTab = MainShellTab.Home;
    private ShellTabOption? _selectedShellTabOption;
    private HomeSystemListTab _homeSystemListTab = HomeSystemListTab.Region;
    private HomeIntelPanelTab _homeIntelPanelTab = HomeIntelPanelTab.Intel;
    private int _alertCount = 2;
    private int _notificationCount = 5;

    public IReadOnlyList<ShellTabOption> ShellTabOptions { get; } =
    [
        new ShellTabOption(MainShellTab.Home, "Home"),
        new ShellTabOption(MainShellTab.Intel, "Intel"),
        new ShellTabOption(MainShellTab.Characters, "Characters"),
        new ShellTabOption(MainShellTab.Settings, "Settings"),
        new ShellTabOption(MainShellTab.About, "About")
    ];

    public MainShellTab SelectedShellTab
    {
        get => _selectedShellTab;
        set
        {
            if (_selectedShellTab.Equals(value)) return;
            this.RaiseAndSetIfChanged(ref _selectedShellTab, value);
            _selectedShellTabOption = ShellTabOptions.First(o => o.Tab == value);
            this.RaisePropertyChanged(nameof(SelectedShellTabOption));
        }
    }

    public ShellTabOption? SelectedShellTabOption
    {
        get => _selectedShellTabOption;
        set
        {
            if (ReferenceEquals(_selectedShellTabOption, value)) return;
            this.RaiseAndSetIfChanged(ref _selectedShellTabOption, value);
            if (value != null && _selectedShellTab != value.Tab)
                SelectedShellTab = value.Tab;
        }
    }

    public ICommand SelectShellTabCommand { get; }

    public HomeSystemListTab SelectedHomeSystemListTab
    {
        get => _homeSystemListTab;
        set => this.RaiseAndSetIfChanged(ref _homeSystemListTab, value);
    }

    public ICommand SelectHomeSystemListTabCommand { get; }

    public HomeIntelPanelTab SelectedHomeIntelPanelTab
    {
        get => _homeIntelPanelTab;
        set
        {
            if (_homeIntelPanelTab.Equals(value)) return;
            this.RaiseAndSetIfChanged(ref _homeIntelPanelTab, value);
            this.RaisePropertyChanged(nameof(IsHomeIntelPanelIntel));
            this.RaisePropertyChanged(nameof(IsHomeIntelPanelLog));
        }
    }

    public bool IsHomeIntelPanelIntel => _homeIntelPanelTab == HomeIntelPanelTab.Intel;
    public bool IsHomeIntelPanelLog => _homeIntelPanelTab == HomeIntelPanelTab.Log;

    public ICommand SelectHomeIntelPanelTabCommand { get; }

    public int AlertCount
    {
        get => _alertCount;
        set => this.RaiseAndSetIfChanged(ref _alertCount, value);
    }

    public int NotificationCount
    {
        get => _notificationCount;
        set => this.RaiseAndSetIfChanged(ref _notificationCount, value);
    }

    public ObservableCollection<SystemListRowVm> MockSystemList { get; } = new();
    public ObservableCollection<string> MockIntelLines { get; } = new();
    public ObservableCollection<string> MockActivityLines { get; } = new();

    public string RouteSummaryLine { get; } = "Route: Jita → RF-K9W | 9 Jumps";
    public string RouteEtaLine { get; } = "Estimated Time: 12 min";
    public string RoutePathLine { get; } =
        "Perimeter (0.9) → Nalvula (0.4) → HED-GP (0.3) → … → RF-K9W (-0.2)";

    public MainViewModel(IDataService? dataService = null)
    {
        SelectShellTabCommand = ReactiveCommand.Create<MainShellTab>(tab => SelectedShellTab = tab);
        SelectHomeSystemListTabCommand =
            ReactiveCommand.Create<HomeSystemListTab>(tab => SelectedHomeSystemListTab = tab);
        SelectHomeIntelPanelTabCommand =
            ReactiveCommand.Create<HomeIntelPanelTab>(tab => SelectedHomeIntelPanelTab = tab);

        System.Diagnostics.Debug.WriteLine("MainViewModel constructor started");
        try
        {
            _dataService = dataService ?? CreateDefaultDataService();
            System.Diagnostics.Debug.WriteLine($"DataService type: {_dataService.GetType().Name}");
            _ = LoadSolarSystemsAsync(); // Fire and forget async load
            ResetViewCommand = ReactiveCommand.Create(ResetView);
            AddEveCharacterCommand = ReactiveCommand.CreateFromTask(AddEveCharacterAsync);
            RemoveSelectedEveCharacterCommand = ReactiveCommand.CreateFromTask(
                RemoveSelectedEveAsync,
                this.WhenAnyValue(x => x.SelectedEvePilot).Select(p => p != null));
            LogoutAllEveCommand = ReactiveCommand.CreateFromTask(LogoutAllEveAsync);
            ProbeEsiCommand = ReactiveCommand.CreateFromTask(ProbeEsiAsync);
            this.WhenAnyValue(x => x.SelectedEvePilot).Subscribe(OnSelectedEvePilotChanged);
            SeedMockHomeLists();
            _selectedShellTabOption = ShellTabOptions[0];
            System.Diagnostics.Debug.WriteLine("MainViewModel constructor completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in MainViewModel constructor: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public ObservableCollection<RenderSolarSystem> SolarSystems
    {
        get => _solarSystems;
        set => this.RaiseAndSetIfChanged(ref _solarSystems, value);
    }

    public List<StargateLink> StargateLinks
    {
        get => _stargateLinks;
        set
        {
            _stargateLinks = value;
            this.RaisePropertyChanged();
        }
    }

    // Camera distance (zoom)
    public double CameraDistance
    {
        get => _cameraDistance;
        set => this.RaiseAndSetIfChanged(ref _cameraDistance, value);
    }

    // Camera rotation angles in radians
    public double CameraRotationX
    {
        get => _cameraRotationX;
        set => this.RaiseAndSetIfChanged(ref _cameraRotationX, value);
    }

    public double CameraRotationY
    {
        get => _cameraRotationY;
        set => this.RaiseAndSetIfChanged(ref _cameraRotationY, value);
    }

    public double CameraRotationZ
    {
        get => _cameraRotationZ;
        set => this.RaiseAndSetIfChanged(ref _cameraRotationZ, value);
    }

    public double FieldOfView
    {
        get => _fieldOfView;
        set => this.RaiseAndSetIfChanged(ref _fieldOfView, value);
    }

    private async Task LoadSolarSystemsAsync()
    {
        System.Diagnostics.Debug.WriteLine("LoadSolarSystemsAsync started");
        Console.WriteLine("LoadSolarSystemsAsync started");
        try
        {
            System.Diagnostics.Debug.WriteLine("Loading solar systems...");
            Console.WriteLine("Loading solar systems...");
            var systems = await _dataService.LoadSolarSystemsAsync();
            System.Diagnostics.Debug.WriteLine($"Loaded {systems.Count} solar systems");
            Console.WriteLine($"Loaded {systems.Count} solar systems");
            
            System.Diagnostics.Debug.WriteLine("Loading stargate links...");
            Console.WriteLine("Loading stargate links...");
            var links = await _dataService.LoadStargateLinksAsync();
            System.Diagnostics.Debug.WriteLine($"Loaded {links.Count} stargate links");
            Console.WriteLine($"Loaded {links.Count} stargate links");
            
            SolarSystems = new ObservableCollection<RenderSolarSystem>(systems);
            StargateLinks = links;
            
            if (systems.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("Calculating initial camera...");
                Console.WriteLine("Calculating initial camera...");
                CalculateInitialCamera();
                System.Diagnostics.Debug.WriteLine("Initial camera calculated");
                Console.WriteLine("Initial camera calculated");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: No solar systems loaded!");
                Console.WriteLine("WARNING: No solar systems loaded!");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== ERROR loading solar systems ===");
            System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"=== ERROR loading solar systems ===");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    public void RefreshEveCharacters()
    {
        var previousId = SelectedEvePilot?.CharacterId;
        EvePilots.Clear();
        if (!EveRuntime.IsAvailable || EveRuntime.Store == null)
        {
            SelectedEvePilot = null;
            this.RaisePropertyChanged(nameof(IsEveAvailable));
            this.RaisePropertyChanged(nameof(IsEveUnavailable));
            return;
        }

        foreach (var r in EveRuntime.Store.ListCharacters())
            EvePilots.Add(new EvePilotRow(r.CharacterId, string.IsNullOrEmpty(r.CharacterName) ? r.CharacterId.ToString() : r.CharacterName));
        this.RaisePropertyChanged(nameof(IsEveAvailable));
        this.RaisePropertyChanged(nameof(IsEveUnavailable));

        var restored = previousId is { } id ? EvePilots.FirstOrDefault(p => p.CharacterId == id) : null;
        if (restored != null)
            SelectedEvePilot = restored;
        else if (EvePilots.Count > 0)
            SelectedEvePilot = EvePilots[0];
        else
            SelectedEvePilot = null;
    }

    private async Task AddEveCharacterAsync()
    {
        if (EveRuntime.Sso == null)
        {
            EveStatus = "EVE SSO not configured.";
            return;
        }

        try
        {
            if (EveRuntime.Sso.UsesBrowserSplitFlow)
            {
#if NET8_0_BROWSER
                var (auth, _, _) = EveRuntime.Sso.PrepareBrowserAuthorization();
                WasmNavigationInterop.NavigateTo(auth.ToString());
                EveStatus = "Redirecting to EVE SSO…";
#endif
                return;
            }

            await EveRuntime.Sso.AddCharacterAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(RefreshEveCharacters);
            EveStatus = "Character linked.";
        }
        catch (Exception ex)
        {
            EveStatus = ex.Message;
        }
    }

    private async Task RemoveSelectedEveAsync()
    {
        var p = SelectedEvePilot;
        if (p == null || EveRuntime.Sso == null)
            return;
        await EveRuntime.Sso.RemoveCharacterAsync(p.CharacterId).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SelectedEvePilot = null;
            RefreshEveCharacters();
        });
    }

    private async Task LogoutAllEveAsync()
    {
        if (EveRuntime.Sso == null)
            return;
        await EveRuntime.Sso.LogoutAllAsync().ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SelectedEvePilot = null;
            RefreshEveCharacters();
        });
    }

    private async Task ProbeEsiAsync()
    {
        if (EveRuntime.Esi == null)
        {
            EveStatus = "ESI not available.";
            return;
        }

        var id = SelectedEvePilot?.CharacterId ?? 2112625428L;
        try
        {
            var info = await EveRuntime.Esi.GetCharacterPublicInfoAsync(id).ConfigureAwait(false);
            if (info == null)
            {
                EveStatus = "ESI returned no character.";
                return;
            }

            EveStatus = $"Public ESI: {info.Name} (corp {info.CorporationId})";
        }
        catch (Exception ex)
        {
            EveStatus = $"ESI error: {ex.Message}";
        }
    }

    private static HttpClient CreateEveImageHttpClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SMTx/1.0");
        return c;
    }

    private void OnSelectedEvePilotChanged(EvePilotRow? pilot)
    {
        this.RaisePropertyChanged(nameof(HasEvePilotSelection));
        _detailLoadCts?.Cancel();
        _detailLoadCts?.Dispose();
        _detailLoadCts = null;

        if (pilot == null)
        {
            Detail.ClearSelection();
            return;
        }

        Detail.BeginLoad(pilot);
        var cts = new CancellationTokenSource();
        _detailLoadCts = cts;
        _ = LoadCharacterDetailAsync(pilot, cts.Token);
    }

    private static string EvePortraitUrl(long characterId) =>
        $"https://images.evetech.net/characters/{characterId}/portrait?tenant=tranquility&size=256";

    private static string EveCorporationLogoUrl(long corporationId) =>
        $"https://images.evetech.net/corporations/{corporationId}/logo?tenant=tranquility&size=128";

    private static string EveAllianceLogoUrl(long allianceId) =>
        $"https://images.evetech.net/alliances/{allianceId}/logo?tenant=tranquility&size=128";

    private static async Task<byte[]?> DownloadImageBytesAsync(HttpClient http, string url, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadCharacterDetailAsync(EvePilotRow pilot, CancellationToken cancellationToken)
    {
        try
        {
            if (EveRuntime.Esi == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        Detail.SetFailed("ESI not available.");
                });
                return;
            }

            var charInfo = await EveRuntime.Esi.GetCharacterPublicInfoAsync(pilot.CharacterId, cancellationToken)
                .ConfigureAwait(false);

            CorporationInfo? corp = null;
            if (charInfo.CorporationId > 0)
            {
                try
                {
                    corp = await EveRuntime.Esi
                        .GetCorporationPublicInfoAsync((int)charInfo.CorporationId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Public corp info is best-effort for the detail panel.
                }
            }

            Alliance? alliance = null;
            if (charInfo.AllianceId is { } allianceId && allianceId > 0)
            {
                try
                {
                    alliance = await EveRuntime.Esi.GetAlliancePublicInfoAsync((int)allianceId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort.
                }
            }

            var portraitTask = DownloadImageBytesAsync(_eveImageHttp, EvePortraitUrl(pilot.CharacterId), cancellationToken);
            var corpLogoTask = charInfo.CorporationId > 0
                ? DownloadImageBytesAsync(_eveImageHttp, EveCorporationLogoUrl(charInfo.CorporationId), cancellationToken)
                : Task.FromResult<byte[]?>(null);
            var allianceLogoTask = charInfo.AllianceId is { } aid && aid > 0
                ? DownloadImageBytesAsync(_eveImageHttp, EveAllianceLogoUrl(aid), cancellationToken)
                : Task.FromResult<byte[]?>(null);

            await Task.WhenAll(portraitTask, corpLogoTask, allianceLogoTask).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                return;

            var portraitBytes = await portraitTask.ConfigureAwait(false);
            var corpLogoBytes = await corpLogoTask.ConfigureAwait(false);
            var allianceLogoBytes = await allianceLogoTask.ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                static Bitmap? Decode(byte[]? bytes)
                {
                    if (bytes == null || bytes.Length == 0)
                        return null;
                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        return new Bitmap(ms);
                    }
                    catch
                    {
                        return null;
                    }
                }

                var portraitBmp = Decode(portraitBytes);
                var corpBmp = Decode(corpLogoBytes);
                var allianceBmp = Decode(allianceLogoBytes);

                if (cancellationToken.IsCancellationRequested)
                {
                    portraitBmp?.Dispose();
                    corpBmp?.Dispose();
                    allianceBmp?.Dispose();
                    return;
                }

                var corpLine = corp != null ? $"{corp.Name} [{corp.Ticker}]" : "";
                var allianceLine = alliance != null ? $"{alliance.Name} [{alliance.Ticker}]" : "";

                Detail.ApplyLoaded(
                    charInfo.Name,
                    pilot.CharacterId,
                    corpLine,
                    corp != null,
                    allianceLine,
                    alliance != null,
                    portraitBmp,
                    corpBmp,
                    allianceBmp);
            });
        }
        catch (OperationCanceledException)
        {
            // Selection changed; new load superseded this one.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                    Detail.SetFailed(ex.Message);
            });
        }
    }

    private static IDataService CreateDefaultDataService()
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
                // Return a JSON service that will try to load from default location
                return new JsonDataService();
            }
        }

        return new SqliteDataService(dbPath);
    }

    private void CalculateInitialCamera()
    {
        if (_solarSystems.Count == 0)
            return;

        // Calculate bounding box in 3D
        var minX = _solarSystems.Min(s => s.WorldX);
        var maxX = _solarSystems.Max(s => s.WorldX);
        var minY = _solarSystems.Min(s => s.WorldY);
        var maxY = _solarSystems.Max(s => s.WorldY);
        var minZ = _solarSystems.Min(s => s.WorldZ);
        var maxZ = _solarSystems.Max(s => s.WorldZ);

        // Center of the universe
        _cameraCenterX = (minX + maxX) / 2.0;
        _cameraCenterY = (minY + maxY) / 2.0;
        _cameraCenterZ = (minZ + maxZ) / 2.0;

        // Calculate distance to fit everything in view
        var width = maxX - minX;
        var height = maxY - minY;
        var depth = maxZ - minZ;
        var maxDimension = Math.Max(Math.Max(width, height), depth);
        
        // Set camera distance to show everything with some padding
        // Use a reasonable distance that will work with the projection
        CameraDistance = maxDimension * 2.0;
        
        System.Diagnostics.Debug.WriteLine($"Bounding box: X=[{minX:F2}, {maxX:F2}], Y=[{minY:F2}, {maxY:F2}], Z=[{minZ:F2}, {maxZ:F2}]");
        System.Diagnostics.Debug.WriteLine($"Max dimension: {maxDimension:F2}, Camera distance: {CameraDistance:F2}");

        // Start with a top-down view (looking straight down, north at top)
        CameraRotationX = -Math.PI / 2.0; // -90 degrees pitch (look straight down)
        CameraRotationY = 0.0; // No yaw (north at top)
        CameraRotationZ = 0.0; // No roll
    }

    public void ResetView()
    {
        // Always reset to top-down view
        CameraRotationX = -Math.PI / 2.0; // -90 degrees pitch (look straight down)
        CameraRotationY = 0.0; // No yaw (north at top)
        CameraRotationZ = 0.0; // No roll
        
        // Recalculate camera position and zoom if systems are loaded
        if (_solarSystems.Count > 0)
        {
            CalculateInitialCamera();
        }
    }

    public void UpdateCanvasSize(double width, double height)
    {
        // Canvas size is used in the projection calculation
        // This method can be used to adjust FOV if needed
        _canvasWidth = width;
        _canvasHeight = height;
    }
    
    public void SetZoomToMaximum()
    {
        // Reset to top-down view and recalculate camera position/zoom (same as Reset View)
        ResetView();
    }
    
    public void SetZoomTo1To1(double canvasWidth, double canvasHeight)
    {
        // For 1:1 scale, set CameraDistance to match the smaller canvas dimension
        // This ensures 1 world unit = 1 pixel on screen
        CameraDistance = Math.Min(canvasWidth, canvasHeight);
    }
    
    public void SetZoomTo1To1()
    {
        // Use stored canvas size
        SetZoomTo1To1(_canvasWidth, _canvasHeight);
    }
    
    // Pan camera in screen space (relative to current view direction)
    public void PanCamera(double deltaX, double deltaY)
    {
        // Convert screen-space delta to world-space movement
        // The pan amount should be proportional to the current zoom level
        // Use a fraction of the camera distance as the pan speed
        var panSpeed = _cameraDistance * 0.001; // Adjust this multiplier to control pan sensitivity
        
        // Calculate pan direction in world space based on current rotation
        // For top-down view (rotationX = -PI/2), we want:
        // - deltaX to move in X direction (east/west)
        // - deltaY to move in Z direction (north/south)
        
        // Get rotation angles
        var cosY = Math.Cos(-_cameraRotationY);
        var sinY = Math.Sin(-_cameraRotationY);
        
        // Calculate world-space movement
        // For a top-down view, screen X maps to world X (east/west), screen Y maps to world Z (north/south)
        // We need to rotate this based on the camera's yaw rotation
        var worldDeltaX = deltaX * cosY - deltaY * sinY;
        var worldDeltaZ = deltaX * sinY + deltaY * cosY;
        
        // Apply pan speed
        CameraCenterX += worldDeltaX * panSpeed;
        CameraCenterZ += worldDeltaZ * panSpeed;
    }
    
    // Pan camera in specific directions (for button controls)
    public void PanCameraNorth(double amount = 1.0) => PanCamera(0, -amount);
    public void PanCameraSouth(double amount = 1.0) => PanCamera(0, amount);
    public void PanCameraEast(double amount = 1.0) => PanCamera(amount, 0);
    public void PanCameraWest(double amount = 1.0) => PanCamera(-amount, 0);

    // Helper method to project 3D point to 2D screen coordinates
    public (double screenX, double screenY, double depth) Project3DTo2D(double worldX, double worldY, double worldZ, double canvasWidth, double canvasHeight)
    {
        // Translate to camera center (center of universe)
        var x = worldX - _cameraCenterX;
        var y = worldY - _cameraCenterY;
        var z = worldZ - _cameraCenterZ;

        // Apply rotations (in order: Z, Y, X) - these rotate the world, not the camera
        // Rotation around Z axis
        var cosZ = Math.Cos(-_cameraRotationZ);
        var sinZ = Math.Sin(-_cameraRotationZ);
        var tempX = x * cosZ - y * sinZ;
        var tempY = x * sinZ + y * cosZ;
        x = tempX;
        y = tempY;

        // Rotation around Y axis
        var cosY = Math.Cos(-_cameraRotationY);
        var sinY = Math.Sin(-_cameraRotationY);
        tempX = x * cosY + z * sinY;
        var tempZ = -x * sinY + z * cosY;
        x = tempX;
        z = tempZ;

        // Rotation around X axis
        var cosX = Math.Cos(-_cameraRotationX);
        var sinX = Math.Sin(-_cameraRotationX);
        tempY = y * cosX - z * sinX;
        tempZ = y * sinX + z * cosX;
        y = tempY;
        z = tempZ;

        // Translate camera back along Z axis (camera is at distance from center)
        z = z + _cameraDistance;

        // Perspective projection
        if (z <= 0.1) // Behind camera, don't render
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        // Perspective divide
        var perspectiveScale = _cameraDistance / z;
        var projectedX = x * perspectiveScale;
        var projectedY = y * perspectiveScale;
        var depth = z; // Store depth for sorting

        // Scale to canvas size
        // The projected coordinates are in world space at the camera plane
        // We need to scale them to fit on the canvas
        // Use a scale based on the camera distance to show a reasonable view
        
        // Calculate the world size we want to show (based on camera distance)
        // At distance d, we want to show roughly d units of world space
        var worldViewSize = _cameraDistance;
        
        // Scale to fit on canvas (use the smaller dimension to ensure everything fits)
        var scale = Math.Min(canvasWidth, canvasHeight) / worldViewSize;
        
        // Convert to screen coordinates (center of screen is origin)
        var screenX = projectedX * scale + canvasWidth / 2.0;
        var screenY = projectedY * scale + canvasHeight / 2.0;

        return (screenX, screenY, depth);
    }

    // Optimized batch projection method that caches trigonometric values
    // This is much faster when projecting many systems at once
    public List<(RenderSolarSystem system, double screenX, double screenY, double depth)> ProjectSystemsBatch(
        IEnumerable<RenderSolarSystem> systems, double canvasWidth, double canvasHeight)
    {
        // Pre-calculate trigonometric values once (expensive operations)
        var cosZ = Math.Cos(-_cameraRotationZ);
        var sinZ = Math.Sin(-_cameraRotationZ);
        var cosY = Math.Cos(-_cameraRotationY);
        var sinY = Math.Sin(-_cameraRotationY);
        var cosX = Math.Cos(-_cameraRotationX);
        var sinX = Math.Sin(-_cameraRotationX);
        
        // Pre-calculate scale factor
        var worldViewSize = _cameraDistance;
        var scale = Math.Min(canvasWidth, canvasHeight) / worldViewSize;
        var halfWidth = canvasWidth / 2.0;
        var halfHeight = canvasHeight / 2.0;
        
        var results = new List<(RenderSolarSystem, double, double, double)>();
        
        foreach (var system in systems)
        {
            // Translate to camera center
            var x = system.WorldX - _cameraCenterX;
            var y = system.WorldY - _cameraCenterY;
            var z = system.WorldZ - _cameraCenterZ;

            // Apply rotations using pre-calculated values
            // Rotation around Z axis
            var tempX = x * cosZ - y * sinZ;
            var tempY = x * sinZ + y * cosZ;
            x = tempX;
            y = tempY;

            // Rotation around Y axis
            tempX = x * cosY + z * sinY;
            var tempZ = -x * sinY + z * cosY;
            x = tempX;
            z = tempZ;

            // Rotation around X axis
            tempY = y * cosX - z * sinX;
            tempZ = y * sinX + z * cosX;
            y = tempY;
            z = tempZ;

            // Translate camera back along Z axis
            z = z + _cameraDistance;

            // Perspective projection
            if (z <= 0.1) // Behind camera, skip
            {
                continue;
            }

            // Perspective divide
            var perspectiveScale = _cameraDistance / z;
            var projectedX = x * perspectiveScale;
            var projectedY = y * perspectiveScale;
            var depth = z;

            // Convert to screen coordinates
            var screenX = projectedX * scale + halfWidth;
            var screenY = projectedY * scale + halfHeight;

            results.Add((system, screenX, screenY, depth));
        }
        
        return results;
    }

    private void SeedMockHomeLists()
    {
        MockSystemList.Clear();
        foreach (var row in new SystemListRowVm[]
             {
                 new("Jita", "High Sec", "#4CAF50"),
                 new("Amarr", "High Sec", "#4CAF50"),
                 new("HED-GP", "Low Sec", "#FF9800"),
                 new("RF-K9W", "Null Sec", "#F44336")
             })
            MockSystemList.Add(row);

        MockIntelLines.Clear();
        MockIntelLines.Add("Enemy fleet spotted in RF-K9W");
        MockIntelLines.Add("Neutral scout in HED-GP");

        MockActivityLines.Clear();
        MockActivityLines.Add("4m ago — Ship destroyed in HED-GP");
        MockActivityLines.Add("12m ago — Gate fire in Perimeter");
    }
}
