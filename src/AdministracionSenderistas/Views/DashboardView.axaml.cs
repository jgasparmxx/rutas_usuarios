using Avalonia.Controls;
using Avalonia.Interactivity;
using AdministracionSenderistas.ViewModels;

namespace AdministracionSenderistas.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        AttachedToVisualTree += (s, e) =>
        {
            var map = this.FindControl<RouteMapControl>("RouteMap");
            if (map != null)
            {
                map.OnPuntoSelected = (punto) =>
                {
                    if (DataContext is DashboardViewModel vm)
                    {
                        vm.OnMapPointSelected(punto);
                    }
                };

                if (DataContext is DashboardViewModel vm)
                {
                    vm.RequestCenterMapOnPunto = (punto) => map.CenterOnPunto(punto);
                }
            }
        };

        DataContextChanged += (s, e) =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                var map = this.FindControl<RouteMapControl>("RouteMap");
                if (map != null)
                {
                    vm.RequestCenterMapOnPunto = (punto) => map.CenterOnPunto(punto);
                }
            }
        };
    }

    public void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.ZoomIn();
    }

    public void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.ZoomOut();
    }

    public void OnResetMapClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.ResetView();
    }

    public void OnSetOsmLayer(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.SetLayer(MapLayerType.OpenStreetMap);
    }

    public void OnSetCartoLayer(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.SetLayer(MapLayerType.CartoVoyager);
    }

    public void OnSetTopoLayer(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.SetLayer(MapLayerType.OpenTopoMap);
    }

    public void OnSetVectorLayer(object? sender, RoutedEventArgs e)
    {
        this.FindControl<RouteMapControl>("RouteMap")?.SetLayer(MapLayerType.VectorOscuro);
    }

    public void OnDataGridDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm && vm.HasSelectedRecord)
        {
            vm.EditSelected();
        }
    }
}
