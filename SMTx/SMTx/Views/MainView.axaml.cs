using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SMTx.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        
        // Wait for the control to be fully loaded before adding children
        // Use LayoutUpdated to ensure XAML is parsed
        LayoutUpdated += OnLayoutUpdated;
    }
    
    private bool _rendererAdded = false;
    
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_rendererAdded) return;
        
        var mapContainer = this.FindControl<Grid>("MapContainer");
        if (mapContainer == null)
        {
            // Try again next layout update
            return;
        }
        
        // Check if a canvas renderer is already added (MapContainer has Compass as child, so count > 0 is expected)
        bool hasRenderer = mapContainer.Children.OfType<MapCanvas>().Any() || 
                          mapContainer.Children.OfType<WebGLMapCanvas>().Any();
        
        if (hasRenderer)
        {
            // Already has renderer, skip
            _rendererAdded = true;
            LayoutUpdated -= OnLayoutUpdated;
            return;
        }
        
        _rendererAdded = true;
        LayoutUpdated -= OnLayoutUpdated; // Remove handler
        
        System.Diagnostics.Debug.WriteLine("MainView: MapContainer found, adding renderer");
        Console.WriteLine("MainView: MapContainer found, adding renderer");
        
        // Use WebGL on browser, regular canvas elsewhere
        if (OperatingSystem.IsBrowser())
        {
            System.Diagnostics.Debug.WriteLine("MainView: Browser detected, creating WebGLMapCanvas");
            Console.WriteLine("MainView: Browser detected, creating WebGLMapCanvas");
            
            // Also add MapCanvas as fallback (hidden by default)
            // If WebGL fails, WebGLMapCanvas will hide itself and MapCanvas will be visible
            var fallbackCanvas = new MapCanvas();
            fallbackCanvas.IsVisible = false; // Hide fallback initially
            mapContainer.Children.Add(fallbackCanvas);
            
            var webglCanvas = new WebGLMapCanvas();
            mapContainer.Children.Add(webglCanvas);
            
            System.Diagnostics.Debug.WriteLine("MainView: Using WebGLMapCanvas for browser platform (with MapCanvas fallback)");
            Console.WriteLine("MainView: Using WebGLMapCanvas for browser platform (with MapCanvas fallback)");
        }
        else
        {
            mapContainer.Children.Add(new MapCanvas());
            System.Diagnostics.Debug.WriteLine("MainView: Using MapCanvas for desktop/mobile platform");
        }
        
        // Add overlay controls after map canvas to ensure they render on top
        var zoomControlBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5)
        };
        var zoomControl = new ZoomControl();
        zoomControlBorder.Child = zoomControl;
        mapContainer.Children.Add(zoomControlBorder);
        
        var compassBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5)
        };
        var compass = new Compass 
        { 
            Width = 120, 
            Height = 120
        };
        compassBorder.Child = compass;
        mapContainer.Children.Add(compassBorder);
        
        // Add directional pad for camera panning (bottom-left corner)
        var directionalPadBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5)
        };
        var directionalPad = new DirectionalPad();
        directionalPadBorder.Child = directionalPad;
        mapContainer.Children.Add(directionalPadBorder);
    }
}
