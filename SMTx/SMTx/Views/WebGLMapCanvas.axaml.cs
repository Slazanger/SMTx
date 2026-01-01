using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using SMTx.Models;
using SMTx.ViewModels;
#if NET8_0_BROWSER
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.InteropServices;
#endif

namespace SMTx.Views;

public partial class WebGLMapCanvas : UserControl
{
    private MainViewModel? _viewModel;
    private Point _lastMousePosition;
    private bool _isDragging = false;
    private IPointer? _capturedPointer;
    
    // Throttle mouse updates to reduce redraw frequency
    private DateTime _lastMouseUpdate = DateTime.MinValue;
    private const int MouseUpdateThrottleMs = 16; // ~60 FPS max
    
    private bool _webglInitialized = false;
    private string _canvasId = "webgl-canvas-" + Guid.NewGuid().ToString("N")[..8];
    private bool _webglAvailable = false;
    
#if NET8_0_BROWSER
public static partial class WebGLInterop
{
    // Use JSImport with module name to import from ES module
    // webgl-renderer.js is now an ES module with proper exports
    [JSImport("isWebGLSupported", "webgl-renderer.js")]
    internal static partial bool IsWebGLSupported();
    
    [JSImport("createWebGLCanvas", "webgl-renderer.js")]
    internal static partial bool CreateWebGLCanvas(string canvasId, int width, int height);
    
    [JSImport("removeWebGLCanvas", "webgl-renderer.js")]
    internal static partial void RemoveWebGLCanvas(string canvasId);
    
    [JSImport("webglRendererInit", "webgl-renderer.js")]
    internal static partial bool WebGLRendererInit(string canvasId);
    
    [JSImport("webglRendererResize", "webgl-renderer.js")]
    internal static partial void WebGLRendererResize(int width, int height);
    
    [JSImport("webglRendererUpdateSystems", "webgl-renderer.js")]
    internal static partial void WebGLRendererUpdateSystems(string systemDataJson, int count);
    
    [JSImport("webglRendererUpdateLinks", "webgl-renderer.js")]
    internal static partial void WebGLRendererUpdateLinks(string linkDataJson, string linkTypesJson, int count);
    
    [JSImport("webglRendererRender", "webgl-renderer.js")]
    internal static partial void WebGLRendererRender();
    
    [JSImport("webglRendererDispose", "webgl-renderer.js")]
    internal static partial void WebGLRendererDispose();
}
#endif

    public WebGLMapCanvas()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        
        // Mouse controls for 3D rotation
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        
        // Initialize WebGL when control is attached to visual tree (browser only)
        if (OperatingSystem.IsBrowser())
        {
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }
    }

    private async void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("WebGLMapCanvas: Attached to visual tree, initializing WebGL");
        Console.WriteLine("WebGLMapCanvas: Attached to visual tree, initializing WebGL");
        await InitializeWebGLAsync();
    }
    
    private async void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        await DisposeWebGLAsync();
    }
    
    private async Task InitializeWebGLAsync()
    {
        System.Diagnostics.Debug.WriteLine($"WebGLMapCanvas: InitializeWebGLAsync called, IsBrowser: {OperatingSystem.IsBrowser()}");
        Console.WriteLine($"WebGLMapCanvas: InitializeWebGLAsync called, IsBrowser: {OperatingSystem.IsBrowser()}");
        
        if (!OperatingSystem.IsBrowser())
        {
            System.Diagnostics.Debug.WriteLine("WebGLMapCanvas: Not a browser, returning early");
            Console.WriteLine("WebGLMapCanvas: Not a browser, returning early");
            return;
        }
        
#if NET8_0_BROWSER
        System.Diagnostics.Debug.WriteLine("WebGLMapCanvas: NET8_0_BROWSER defined, entering browser-specific code");
        Console.WriteLine("WebGLMapCanvas: NET8_0_BROWSER defined, entering browser-specific code");
        
#pragma warning disable CA1416 // JavaScript interop is only supported on 'browser'.
        try
        {
            System.Diagnostics.Debug.WriteLine("WebGLMapCanvas: Starting WebGL initialization");
            Console.WriteLine("WebGLMapCanvas: Starting WebGL initialization");
            
            // Wait a bit to ensure webgl-renderer.js has loaded
            // The script is loaded in index.html, but there might be a timing issue
            await Task.Delay(100);
            
            // Verify the function exists before trying to bind
            using var globalThis = JSHost.GlobalThis;
            var funcCheck = globalThis.GetPropertyAsJSObject("isWebGLSupported");
            if (funcCheck == null)
            {
                System.Diagnostics.Debug.WriteLine("WebGL: isWebGLSupported function not found - webgl-renderer.js may not be loaded");
                Console.WriteLine("WebGL: isWebGLSupported function not found - webgl-renderer.js may not be loaded");
                _webglAvailable = false;
                this.IsVisible = false;
                ShowFallbackCanvas();
                return;
            }
            funcCheck.Dispose();
            
            // Check WebGL availability first
            bool webglSupported;
            try
            {
                webglSupported = WebGLInterop.IsWebGLSupported();
            }
            catch (Exception bindEx)
            {
                System.Diagnostics.Debug.WriteLine($"WebGL: Failed to call JavaScript function 'isWebGLSupported': {bindEx.Message}");
                Console.WriteLine($"WebGL: Failed to call JavaScript function 'isWebGLSupported': {bindEx.Message}");
                Console.WriteLine($"WebGL: Stack trace: {bindEx.StackTrace}");
                _webglAvailable = false;
                this.IsVisible = false;
                ShowFallbackCanvas();
                return;
            }
            System.Diagnostics.Debug.WriteLine($"WebGL: WebGL supported: {webglSupported}");
            Console.WriteLine($"WebGL: WebGL supported: {webglSupported}");
            
            if (!webglSupported)
            {
                System.Diagnostics.Debug.WriteLine("WebGL: WebGL is not supported in this browser");
                Console.WriteLine("WebGL: WebGL is not supported in this browser");
                _webglAvailable = false;
                // Fallback: Hide this control and show MapCanvas instead
                this.IsVisible = false;
                // Show the fallback MapCanvas
                ShowFallbackCanvas();
                return;
            }
            
            // Get actual size - use a minimum size if bounds are invalid
            var width = Math.Max(800, (int)Bounds.Width);
            var height = Math.Max(600, (int)Bounds.Height);
            
            System.Diagnostics.Debug.WriteLine($"WebGLMapCanvas: Creating canvas with ID: {_canvasId}, size: {width}x{height}, Bounds: {Bounds.Width}x{Bounds.Height}");
            Console.WriteLine($"WebGLMapCanvas: Creating canvas with ID: {_canvasId}, size: {width}x{height}, Bounds: {Bounds.Width}x{Bounds.Height}");
            
            // Create canvas element via JavaScript
            var canvasCreated = WebGLInterop.CreateWebGLCanvas(_canvasId, width, height);
            System.Diagnostics.Debug.WriteLine($"WebGLMapCanvas: Canvas creation result: {canvasCreated}");
            Console.WriteLine($"WebGLMapCanvas: Canvas creation result: {canvasCreated}");
            
            if (!canvasCreated)
            {
                System.Diagnostics.Debug.WriteLine("WebGL: Failed to create canvas element");
                Console.WriteLine("WebGL: Failed to create canvas element");
                _webglAvailable = false;
                this.IsVisible = false;
                // Show fallback
                ShowFallbackCanvas();
                return;
            }
            
            // Initialize WebGL renderer
            System.Diagnostics.Debug.WriteLine($"WebGLMapCanvas: Initializing WebGL renderer with canvas ID: {_canvasId}");
            Console.WriteLine($"WebGLMapCanvas: Initializing WebGL renderer with canvas ID: {_canvasId}");
            var initialized = WebGLInterop.WebGLRendererInit(_canvasId);
            System.Diagnostics.Debug.WriteLine($"WebGLMapCanvas: WebGL initialization result: {initialized}");
            Console.WriteLine($"WebGLMapCanvas: WebGL initialization result: {initialized}");
            
            if (initialized)
            {
                _webglInitialized = true;
                _webglAvailable = true;
                System.Diagnostics.Debug.WriteLine($"WebGL: Initialized successfully with canvas ID: {_canvasId}");
                Console.WriteLine($"WebGL: Initialized successfully with canvas ID: {_canvasId}");
                
                // Wait a bit for the control to be properly sized, then trigger initial render
                await Task.Delay(200);
                await RenderToWebGLAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WebGL: Failed to initialize WebGL context");
                Console.WriteLine("WebGL: Failed to initialize WebGL context");
                _webglAvailable = false;
                this.IsVisible = false;
                // Show fallback
                ShowFallbackCanvas();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebGL: Error during initialization: {ex.Message}");
            Console.WriteLine($"WebGL: Error during initialization: {ex.Message}");
            Console.WriteLine($"WebGL: Stack trace: {ex.StackTrace}");
            _webglAvailable = false;
            this.IsVisible = false;
            // Show fallback
            ShowFallbackCanvas();
        }
#pragma warning restore CA1416
#else
        // Not a browser target - this code should not be reached due to OperatingSystem.IsBrowser() check
        System.Diagnostics.Debug.WriteLine("WebGL: InitializeWebGLAsync called on non-browser platform");
        Console.WriteLine("WebGL: InitializeWebGLAsync called on non-browser platform");
#endif
    }
    
    private void ShowFallbackCanvas()
    {
        var parent = this.Parent as Panel;
        if (parent != null)
        {
            foreach (var child in parent.Children)
            {
                if (child is MapCanvas mapCanvas && child != this)
                {
                    mapCanvas.IsVisible = true;
                    System.Diagnostics.Debug.WriteLine("WebGL: Showing fallback MapCanvas");
                    Console.WriteLine("WebGL: Showing fallback MapCanvas");
                    break;
                }
            }
        }
    }
    
    private async Task DisposeWebGLAsync()
    {
        if (!OperatingSystem.IsBrowser() || !_webglInitialized)
        {
            return;
        }
        
#if NET8_0_BROWSER
#pragma warning disable CA1416 // JavaScript interop is only supported on 'browser'.
        try
        {
            WebGLInterop.WebGLRendererDispose();
            WebGLInterop.RemoveWebGLCanvas(_canvasId);
            _webglInitialized = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebGL: Error during disposal: {ex.Message}");
        }
#pragma warning restore CA1416
#endif
    }
    
    private async Task RenderToWebGLAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        if (!_webglInitialized)
        {
            System.Diagnostics.Debug.WriteLine("WebGL: Not initialized, skipping render");
            return;
        }
        
        if (_viewModel?.SolarSystems == null)
        {
            System.Diagnostics.Debug.WriteLine("WebGL: No solar systems data available");
            return;
        }
        
#if NET8_0_BROWSER
#pragma warning disable CA1416 // JavaScript interop is only supported on 'browser'.
        try
        {
            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine($"WebGL: Invalid bounds: {bounds.Width}x{bounds.Height}");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"WebGL: Rendering {_viewModel.SolarSystems.Count} systems, bounds: {bounds.Width}x{bounds.Height}");
            
            // Resize canvas if needed
            WebGLInterop.WebGLRendererResize((int)bounds.Width, (int)bounds.Height);
            
            var pixelSize = new PixelSize((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
            
            // Project systems using existing batch method
            var projectedResults = _viewModel.ProjectSystemsBatch(
                _viewModel.SolarSystems,
                pixelSize.Width,
                pixelSize.Height);
            
            // Filter and prepare system data for WebGL
            var visibleSystems = projectedResults
                .Where(p => !double.IsNaN(p.screenX) && !double.IsNaN(p.screenY))
                .Where(p => p.screenX >= -50 && p.screenX <= pixelSize.Width + 50 &&
                           p.screenY >= -50 && p.screenY <= pixelSize.Height + 50)
                .OrderBy(p => p.depth)
                .ToList();
            
            // Prepare system array: [x, y, size] for each system
            var systemData = new List<float>();
            foreach (var projected in visibleSystems)
            {
                // Depth is the Z coordinate after translation (distance from camera)
                // Closer systems (smaller depth) should be larger
                // Use inverse relationship: closer = larger
                var minDepth = _viewModel.CameraDistance * 0.1; // Minimum visible depth
                var maxDepth = _viewModel.CameraDistance * 3.0; // Maximum visible depth
                var normalizedDepth = Math.Clamp((projected.depth - minDepth) / (maxDepth - minDepth), 0.0, 1.0);
                var depthFactor = 1.0 - normalizedDepth; // Invert so closer = larger
                depthFactor = Math.Max(0.2, Math.Min(1.0, depthFactor)); // Clamp between 0.2 and 1.0
                var radius = 15.0 * depthFactor;
                
                systemData.Add((float)projected.screenX);
                systemData.Add((float)projected.screenY);
                systemData.Add((float)radius);
            }
            
            System.Diagnostics.Debug.WriteLine($"WebGL: Prepared {systemData.Count / 3} systems for rendering");
            Console.WriteLine($"WebGL: Prepared {systemData.Count / 3} systems for rendering");
            if (systemData.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"WebGL: First system at ({systemData[0]}, {systemData[1]}) size {systemData[2]}");
                Console.WriteLine($"WebGL: First system at ({systemData[0]}, {systemData[1]}) size {systemData[2]}");
            }
            
            // Update systems in WebGL
            if (systemData.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"WebGL: Sending {visibleSystems.Count} visible systems to renderer");
                var systemArray = systemData.ToArray();
                var systemDataJson = System.Text.Json.JsonSerializer.Serialize(systemArray);
                WebGLInterop.WebGLRendererUpdateSystems(systemDataJson, visibleSystems.Count);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WebGL: No visible systems to render");
                WebGLInterop.WebGLRendererUpdateSystems("[]", 0);
            }
            
            // Prepare link data
            if (_viewModel.StargateLinks != null && _viewModel.StargateLinks.Count > 0)
            {
                var systemLookup = visibleSystems.ToDictionary(p => p.system.Id);
                var linkData = new List<float>();
                var linkTypes = new List<byte>();
                var linkCount = 0;
                
                var linkScreenMargin = 100.0;
                var minScreenX = -linkScreenMargin;
                var maxScreenX = pixelSize.Width + linkScreenMargin;
                var minScreenY = -linkScreenMargin;
                var maxScreenY = pixelSize.Height + linkScreenMargin;
                
                foreach (var link in _viewModel.StargateLinks)
                {
                    if (!systemLookup.TryGetValue(link.SourceSystemId, out var source) ||
                        !systemLookup.TryGetValue(link.DestinationSystemId, out var dest))
                    {
                        continue;
                    }
                    
                    var sourceVisible = source.screenX >= minScreenX && source.screenX <= maxScreenX &&
                                       source.screenY >= minScreenY && source.screenY <= maxScreenY;
                    var destVisible = dest.screenX >= minScreenX && dest.screenX <= maxScreenX &&
                                      dest.screenY >= minScreenY && dest.screenY <= maxScreenY;
                    
                    if (!sourceVisible && !destVisible)
                        continue;
                    
                    // Add link endpoints: [x1, y1, x2, y2]
                    linkData.Add((float)source.screenX);
                    linkData.Add((float)source.screenY);
                    linkData.Add((float)dest.screenX);
                    linkData.Add((float)dest.screenY);
                    
                    // Determine link type: 0=regular, 1=constellation, 2=regional
                    byte linkType = 0;
                    var linkTypeLower = link.LinkType?.ToLower() ?? "regular";
                    if (linkTypeLower == "constellation")
                        linkType = 1;
                    else if (linkTypeLower == "regional")
                        linkType = 2;
                    
                    linkTypes.Add(linkType);
                    linkCount++;
                }
                
                if (linkData.Count > 0)
                {
                    var linkArray = linkData.ToArray();
                    var linkTypesArray = linkTypes.ToArray();
                    var linkDataJson = System.Text.Json.JsonSerializer.Serialize(linkArray);
                    var linkTypesJson = System.Text.Json.JsonSerializer.Serialize(linkTypesArray);
                    WebGLInterop.WebGLRendererUpdateLinks(linkDataJson, linkTypesJson, linkCount);
                }
                else
                {
                    WebGLInterop.WebGLRendererUpdateLinks("[]", "[]", 0);
                }
            }
            else
            {
                WebGLInterop.WebGLRendererUpdateLinks("[]", "[]", 0);
            }
            
            // Render
            System.Diagnostics.Debug.WriteLine("WebGL: Calling render");
            Console.WriteLine("WebGL: Calling render");
            WebGLInterop.WebGLRendererRender();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebGL: Error during render: {ex.Message}");
            Console.WriteLine($"WebGL: Error during render: {ex.Message}");
            Console.WriteLine($"WebGL: Stack trace: {ex.StackTrace}");
        }
#pragma warning restore CA1416
#else
        // Not a browser target - this code should not be reached due to OperatingSystem.IsBrowser() check
        System.Diagnostics.Debug.WriteLine("WebGL: RenderToWebGLAsync called on non-browser platform");
        Console.WriteLine("WebGL: RenderToWebGLAsync called on non-browser platform");
#endif
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (_viewModel.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged -= OnSolarSystemsCollectionChanged;
            }
        }

        _viewModel = DataContext as MainViewModel;
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (_viewModel.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged += OnSolarSystemsCollectionChanged;
            }
        }
        
        if (OperatingSystem.IsBrowser())
        {
            // Delay render slightly to ensure WebGL is initialized
            Task.Delay(100).ContinueWith(_ => RenderToWebGLAsync());
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SolarSystems))
        {
            if (_viewModel?.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged -= OnSolarSystemsCollectionChanged;
            }
            
            if (_viewModel?.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged += OnSolarSystemsCollectionChanged;
            }
        }
        
        if (e.PropertyName == nameof(MainViewModel.SolarSystems) ||
            e.PropertyName == nameof(MainViewModel.StargateLinks) ||
            e.PropertyName == nameof(MainViewModel.CameraDistance) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationX) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationY) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationZ) ||
            e.PropertyName == nameof(MainViewModel.FieldOfView))
        {
            if (OperatingSystem.IsBrowser())
            {
                _ = RenderToWebGLAsync();
            }
            else
            {
                InvalidateVisual();
            }
        }
    }

    private void OnSolarSystemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (OperatingSystem.IsBrowser())
        {
            _ = RenderToWebGLAsync();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_viewModel != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _viewModel.UpdateCanvasSize(e.NewSize.Width, e.NewSize.Height);
        }
        
        if (OperatingSystem.IsBrowser())
        {
            // Delay render slightly to ensure resize is complete
            Task.Delay(50).ContinueWith(_ => RenderToWebGLAsync());
        }
        else
        {
            InvalidateVisual();
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(this);
            _capturedPointer = e.Pointer;
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && _viewModel != null)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            // Rotate camera based on mouse movement
            _viewModel.CameraRotationY += deltaX * 0.01; // Horizontal rotation (yaw)
            _viewModel.CameraRotationX += deltaY * 0.01; // Vertical rotation (pitch)

            // Clamp pitch to avoid gimbal lock
            _viewModel.CameraRotationX = Math.Max(-Math.PI / 2.0 + 0.1, Math.Min(Math.PI / 2.0 - 0.1, _viewModel.CameraRotationX));

            _lastMousePosition = currentPosition;
            
            // Throttle redraws to improve performance
            var now = DateTime.UtcNow;
            if ((now - _lastMouseUpdate).TotalMilliseconds >= MouseUpdateThrottleMs)
            {
                _lastMouseUpdate = now;
                if (OperatingSystem.IsBrowser())
                {
                    _ = RenderToWebGLAsync();
                }
                else
                {
                    InvalidateVisual();
                }
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _capturedPointer != null)
        {
            _isDragging = false;
            _capturedPointer.Capture(null);
            _capturedPointer = null;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel != null)
        {
            // Zoom in/out with mouse wheel
            var zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            _viewModel.CameraDistance *= zoomFactor;
            
            // Clamp distance
            _viewModel.CameraDistance = Math.Max(1000.0, Math.Min(100000.0, _viewModel.CameraDistance));
            
            if (OperatingSystem.IsBrowser())
            {
                _ = RenderToWebGLAsync();
            }
            else
            {
                InvalidateVisual();
            }
        }
    }

    // Fallback rendering for non-browser platforms
    public override void Render(DrawingContext context)
    {
        if (OperatingSystem.IsBrowser())
        {
            // On browser, WebGL handles rendering, so we don't render here
            return;
        }
        
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        context.FillRectangle(Brushes.Black, bounds);
        
        var center = new Point(bounds.Width / 2.0, bounds.Height / 2.0);
        var text = new FormattedText(
            "WebGL rendering is only available in browser",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Arial"),
            14,
            Brushes.White);
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }
}

