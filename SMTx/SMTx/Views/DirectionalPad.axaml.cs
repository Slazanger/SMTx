using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class DirectionalPad : UserControl
{
    private MainViewModel? _viewModel;

    public DirectionalPad()
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

    private void OnPanNorth(object? sender, RoutedEventArgs e)
    {
        _viewModel?.PanCameraNorth();
    }

    private void OnPanSouth(object? sender, RoutedEventArgs e)
    {
        _viewModel?.PanCameraSouth();
    }

    private void OnPanEast(object? sender, RoutedEventArgs e)
    {
        _viewModel?.PanCameraEast();
    }

    private void OnPanWest(object? sender, RoutedEventArgs e)
    {
        _viewModel?.PanCameraWest();
    }
}
