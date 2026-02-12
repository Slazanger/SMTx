using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class ZoomControl : UserControl
{
    private MainViewModel? _viewModel;

    public ZoomControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Handle property changes if needed
    }

    private void OnSetZoomToMaximum(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SetZoomToMaximum();
    }

    private void OnSetZoomTo1To1(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            // Try to get canvas size from parent MapContainer
            var parent = this.Parent;
            while (parent != null)
            {
                if (parent is Grid grid && grid.Name == "MapContainer")
                {
                    var width = grid.Bounds.Width;
                    var height = grid.Bounds.Height;
                    
                    if (width > 0 && height > 0)
                    {
                        _viewModel.SetZoomTo1To1(width, height);
                        return;
                    }
                    break;
                }
                parent = parent.Parent;
            }
            
            // Fallback: use parameterless version which uses stored canvas size
            _viewModel.SetZoomTo1To1();
        }
    }
}
