using System;
using Avalonia.Controls;

namespace SMTx.Views;

public partial class HomeMapHostView : UserControl
{
    private bool _attached;

    public HomeMapHostView()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_attached) return;

        var mapContainer = this.FindControl<Grid>("MapContainer");
        if (mapContainer is null) return;

        _attached = true;
        LayoutUpdated -= OnLayoutUpdated;
        MapHostSetup.AttachMapAndOverlays(mapContainer);
    }
}
