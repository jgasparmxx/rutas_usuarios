using System;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using AdministracionSenderistas.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdministracionSenderistas.ViewModels;

public partial class PuntoRutaItem : ObservableObject
{
    public PuntoRuta Punto { get; }

    [ObservableProperty]
    private Bitmap? _fotoBitmap;

    [ObservableProperty]
    private bool _isLoadingPhoto;

    public int Indice { get; }
    public int Index => Indice;
    public string Nombre => string.IsNullOrWhiteSpace(Punto.Descripcion) ? $"Punto #{Indice}" : Punto.Descripcion;
    public string Descripcion => string.IsNullOrWhiteSpace(Punto.Descripcion) ? $"Punto de Control #{Indice}" : Punto.Descripcion;
    public string CoordenadasTexto => Punto.CoordenadasTexto;
    public string CoordenadasFormateadas => $"Lat: {Latitud:F3}, Lon: {Longitud:F3}";
    public string AltitudM => $"{Latitud:F3}, {Longitud:F3}";
    public bool TieneConectividad => Punto.TieneConectividad;
    public string ConectividadPctFormateada => TieneConectividad ? "📶 Con señal" : "📵 Sin señal";
    public bool TieneFoto => Punto.TieneFoto;
    public bool HasFoto => TieneFoto;
    public string? FotoPath => Punto.FotoPath;
    public double Latitud => Punto.Latitud;
    public double Longitud => Punto.Longitud;

    private readonly Action<PuntoRutaItem>? _onOpenPhoto;
    private readonly Action<PuntoRutaItem>? _onRemovePhoto;
    private readonly Action<PuntoRutaItem>? _onFocusMap;
    private readonly Action<PuntoRutaItem>? _onDeletePunto;

    public PuntoRutaItem(
        PuntoRuta punto,
        int indice,
        Action<PuntoRutaItem>? onOpenPhoto = null,
        Action<PuntoRutaItem>? onRemovePhoto = null,
        Action<PuntoRutaItem>? onFocusMap = null,
        Action<PuntoRutaItem>? onDeletePunto = null)
    {
        Punto = punto;
        Indice = indice;
        _onOpenPhoto = onOpenPhoto;
        _onRemovePhoto = onRemovePhoto;
        _onFocusMap = onFocusMap;
        _onDeletePunto = onDeletePunto;

        if (TieneFoto)
        {
            _ = LoadPhotoAsync();
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void VerEnMapa()
    {
        _onFocusMap?.Invoke(this);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void VerFoto()
    {
        _onOpenPhoto?.Invoke(this);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void BloquearFoto()
    {
        _onRemovePhoto?.Invoke(this);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void EliminarPunto()
    {
        _onDeletePunto?.Invoke(this);
    }

    public void NotifyFotoChanged()
    {
        OnPropertyChanged(nameof(TieneFoto));
        OnPropertyChanged(nameof(HasFoto));
        OnPropertyChanged(nameof(FotoPath));
        OnPropertyChanged(nameof(FotoBitmap));
    }

    public async Task LoadPhotoAsync()
    {
        if (!TieneFoto || FotoBitmap != null || IsLoadingPhoto) return;

        IsLoadingPhoto = true;
        try
        {
            FotoBitmap = await ImageLoaderService.LoadImageAsync(FotoPath);
        }
        finally
        {
            IsLoadingPhoto = false;
        }
    }
}
