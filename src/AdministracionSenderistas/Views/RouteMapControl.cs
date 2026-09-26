using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AdministracionSenderistas.Views;

public enum MapLayerType
{
    OpenStreetMap,
    CartoVoyager,
    OpenTopoMap,
    VectorOscuro
}

public class RouteMapControl : Control
{
    public static readonly StyledProperty<List<TrazaPunto>?> TrazaProperty =
        AvaloniaProperty.Register<RouteMapControl, List<TrazaPunto>?>(nameof(Traza));

    public static readonly StyledProperty<List<PuntoRuta>?> PuntosProperty =
        AvaloniaProperty.Register<RouteMapControl, List<PuntoRuta>?>(nameof(Puntos));

    public static readonly StyledProperty<Action<PuntoRuta>?> OnPuntoSelectedProperty =
        AvaloniaProperty.Register<RouteMapControl, Action<PuntoRuta>?>(nameof(OnPuntoSelected));

    public List<TrazaPunto>? Traza
    {
        get => GetValue(TrazaProperty);
        set => SetValue(TrazaProperty, value);
    }

    public List<PuntoRuta>? Puntos
    {
        get => GetValue(PuntosProperty);
        set => SetValue(PuntosProperty, value);
    }

    public Action<PuntoRuta>? OnPuntoSelected
    {
        get => GetValue(OnPuntoSelectedProperty);
        set => SetValue(OnPuntoSelectedProperty, value);
    }

    private MapLayerType _activeLayer = MapLayerType.OpenStreetMap;
    public MapLayerType ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (_activeLayer != value)
            {
                _activeLayer = value;
                InvalidateVisual();
            }
        }
    }

    private double _zoomFactor = 1.0;
    private Point _panOffset = new(0, 0);
    private Point _lastMousePos;
    private Point _pressedMousePos;
    private bool _isDragging;
    private bool _hasDragged;
    private PuntoRuta? _hoveredPunto;

    // Tile cache y descargador HTTP asíncrono
    private static readonly ConcurrentDictionary<string, Bitmap> _tileMemoryCache = new();
    private static readonly ConcurrentDictionary<string, byte> _pendingDownloads = new();
    private static readonly HttpClient _httpClient;
    private static readonly string _cacheDirectory;
    private static readonly SemaphoreSlim _downloadThrottle = new(4, 4);

    static RouteMapControl()
    {
        AffectsRender<RouteMapControl>(TrazaProperty, PuntosProperty);

        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Senderistas",
            "MapTiles"
        );

        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }
        catch { }

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SenderistasDesktopApp/1.0 (administracion@senderistas.org)");
    }

    public RouteMapControl()
    {
        ClipToBounds = true;
    }

    public void SetLayer(MapLayerType layer)
    {
        ActiveLayer = layer;
    }

    public void ResetView()
    {
        _zoomFactor = 1.0;
        _panOffset = new Point(0, 0);
        InvalidateVisual();
    }

    /// <summary>
    /// Calcula límites globales de latitud y longitud a partir de la traza y los puntos.
    /// </summary>
    private bool TryGetBounds(out double minLat, out double maxLat, out double minLon, out double maxLon)
    {
        minLat = double.MaxValue; maxLat = double.MinValue;
        minLon = double.MaxValue; maxLon = double.MinValue;

        var traza = Traza;
        if (traza != null && traza.Count > 0)
        {
            foreach (var t in traza)
            {
                if (t.Latitud < minLat) minLat = t.Latitud;
                if (t.Latitud > maxLat) maxLat = t.Latitud;
                if (t.Longitud < minLon) minLon = t.Longitud;
                if (t.Longitud > maxLon) maxLon = t.Longitud;
            }
        }

        var puntos = Puntos;
        if (puntos != null && puntos.Count > 0)
        {
            foreach (var p in puntos)
            {
                if (p.Latitud < minLat) minLat = p.Latitud;
                if (p.Latitud > maxLat) maxLat = p.Latitud;
                if (p.Longitud < minLon) minLon = p.Longitud;
                if (p.Longitud > maxLon) maxLon = p.Longitud;
            }
        }

        return minLat != double.MaxValue && minLon != double.MaxValue;
    }

    private double GetBaseZoomScale(double width, double height)
    {
        if (!TryGetBounds(out double minLat, out double maxLat, out double minLon, out double maxLon))
            return 1.0;

        double padding = 48;
        double usableW = Math.Max(10, width - padding * 2);
        double usableH = Math.Max(10, height - padding * 2);

        double deltaX = Math.Max(0.000001, LonToMercatorX(maxLon) - LonToMercatorX(minLon));
        double deltaY = Math.Max(0.000001, LatToMercatorY(minLat) - LatToMercatorY(maxLat));

        return Math.Min(usableW / (deltaX * 256.0), usableH / (deltaY * 256.0));
    }

    private double GetEffectiveZoom(double zoomFactor, double width, double height)
    {
        double baseScale = GetBaseZoomScale(width, height);
        return Math.Clamp(Math.Log2(Math.Max(1.0, baseScale)) + Math.Log2(Math.Max(0.0001, zoomFactor)), 1.0, 20.0);
    }

    /// <summary>
    /// Realiza un zoom anclado al punto pivote (ej: posición del cursor del ratón),
    /// garantizando que las coordenadas geográficas bajo el cursor permanezcan exactamente
    /// en la misma posición de pantalla sin moverse ni desviarse.
    /// </summary>
    public void ZoomAt(Point pivot, double multiplier)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double oldEffectiveZoom = GetEffectiveZoom(_zoomFactor, w, h);
        double targetZoomFactor = Math.Clamp(_zoomFactor * multiplier, 0.1, 1024.0);
        double newEffectiveZoom = GetEffectiveZoom(targetZoomFactor, w, h);

        if (Math.Abs(newEffectiveZoom - oldEffectiveZoom) < 0.0001)
            return;

        double ratio = Math.Pow(2.0, newEffectiveZoom - oldEffectiveZoom);

        double cx = pivot.X - w / 2.0;
        double cy = pivot.Y - h / 2.0;

        // Ajuste exacto del desplazamiento de paneo para mantener el punto bajo el cursor estático
        _panOffset = new Point(
            cx - (cx - _panOffset.X) * ratio,
            cy - (cy - _panOffset.Y) * ratio
        );

        _zoomFactor = targetZoomFactor;
        InvalidateVisual();
    }

    public void ZoomIn()
    {
        double w = Bounds.Width > 0 ? Bounds.Width : 600;
        double h = Bounds.Height > 0 ? Bounds.Height : 400;
        ZoomAt(new Point(w / 2.0, h / 2.0), 1.35);
    }

    public void ZoomOut()
    {
        double w = Bounds.Width > 0 ? Bounds.Width : 600;
        double h = Bounds.Height > 0 ? Bounds.Height : 400;
        ZoomAt(new Point(w / 2.0, h / 2.0), 1.0 / 1.35);
    }

    /// <summary>
    /// Centra y amplía el mapa directamente en un Faro / Punto de ruta específico.
    /// </summary>
    public void CenterOnPunto(PuntoRuta punto, double targetZoomFactor = 6.0)
    {
        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        if (!TryGetBounds(out double minLat, out double maxLat, out double minLon, out double maxLon))
            return;

        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;

        _zoomFactor = Math.Max(_zoomFactor, targetZoomFactor);
        double effectiveZoom = GetEffectiveZoom(_zoomFactor, w, h);
        int intTileZoom = Math.Clamp((int)Math.Round(effectiveZoom), 1, 19);
        double tileScale = Math.Pow(2.0, effectiveZoom - intTileZoom);
        double tileSizeOnScreen = 256.0 * tileScale;

        double centerTileX = LonToTileX(centerLon, intTileZoom);
        double centerTileY = LatToTileY(centerLat, intTileZoom);

        double puntoTileX = LonToTileX(punto.Longitud, intTileZoom);
        double puntoTileY = LatToTileY(punto.Latitud, intTileZoom);

        _panOffset = new Point(
            -(puntoTileX - centerTileX) * tileSizeOnScreen,
            -(puntoTileY - centerTileY) * tileSizeOnScreen
        );

        _hoveredPunto = punto;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pos = e.GetPosition(this);
        double delta = e.Delta.Y;
        if (delta > 0)
        {
            ZoomAt(pos, 1.25);
        }
        else if (delta < 0)
        {
            ZoomAt(pos, 1.0 / 1.25);
        }
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var currentPoint = e.GetCurrentPoint(this);
        if (currentPoint.Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                // Doble clic: centrar y ampliar en el punto pulsado
                ZoomAt(currentPoint.Position, 1.6);
                e.Handled = true;
                return;
            }

            _lastMousePos = currentPoint.Position;
            _pressedMousePos = currentPoint.Position;
            _isDragging = true;
            _hasDragged = false;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        if (_isDragging)
        {
            var delta = pos - _lastMousePos;
            if (!_hasDragged && (Math.Abs(pos.X - _pressedMousePos.X) > 3 || Math.Abs(pos.Y - _pressedMousePos.Y) > 3))
            {
                _hasDragged = true;
            }

            _panOffset = new Point(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _lastMousePos = pos;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            InvalidateVisual();
        }
        else
        {
            CheckHoveredPoint(pos);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);

            // Si fue un clic simple sin arrastre (menos de 4px)
            if (!_hasDragged)
            {
                if (_hoveredPunto != null)
                {
                    OnPuntoSelected?.Invoke(_hoveredPunto);
                }
            }

            CheckHoveredPoint(e.GetPosition(this));
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        double width = bounds.Width;
        double height = bounds.Height;

        if (width <= 0 || height <= 0) return;

        // Fondo base
        var bgBrush = new SolidColorBrush(_activeLayer == MapLayerType.VectorOscuro ? Color.Parse("#080D1A") : Color.Parse("#E5E7EB"));
        context.FillRectangle(bgBrush, bounds);

        var traza = Traza;
        var puntos = Puntos;

        if ((traza == null || traza.Count == 0) && (puntos == null || puntos.Count == 0))
        {
            var noDataText = new FormattedText(
                "No hay coordenadas GPS o traza disponible para esta ruta",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Medium),
                13,
                new SolidColorBrush(Color.Parse("#64748B"))
            );
            context.DrawText(noDataText, new Point((width - noDataText.Width) / 2, (height - noDataText.Height) / 2));
            return;
        }

        // Calcular límites de lat/lon
        double minLat = double.MaxValue, maxLat = double.MinValue;
        double minLon = double.MaxValue, maxLon = double.MinValue;

        if (traza != null && traza.Count > 0)
        {
            foreach (var t in traza)
            {
                if (t.Latitud < minLat) minLat = t.Latitud;
                if (t.Latitud > maxLat) maxLat = t.Latitud;
                if (t.Longitud < minLon) minLon = t.Longitud;
                if (t.Longitud > maxLon) maxLon = t.Longitud;
            }
        }

        if (puntos != null && puntos.Count > 0)
        {
            foreach (var p in puntos)
            {
                if (p.Latitud < minLat) minLat = p.Latitud;
                if (p.Latitud > maxLat) maxLat = p.Latitud;
                if (p.Longitud < minLon) minLon = p.Longitud;
                if (p.Longitud > maxLon) maxLon = p.Longitud;
            }
        }

        if (minLat == double.MaxValue || minLon == double.MaxValue) return;

        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;

        double padding = 48;
        double usableW = Math.Max(10, width - padding * 2);
        double usableH = Math.Max(10, height - padding * 2);

        // Bounding box en Mercator
        double mercMinX = LonToMercatorX(minLon);
        double mercMaxX = LonToMercatorX(maxLon);
        double mercMinY = LatToMercatorY(maxLat);
        double mercMaxY = LatToMercatorY(minLat);

        double deltaX = Math.Max(0.000001, mercMaxX - mercMinX);
        double deltaY = Math.Max(0.000001, mercMaxY - mercMinY);

        double effectiveZoom = GetEffectiveZoom(_zoomFactor, width, height);
        int intTileZoom = Math.Clamp((int)Math.Round(effectiveZoom), 1, 19);
        double tileScale = Math.Pow(2.0, effectiveZoom - intTileZoom);
        double tileSizeOnScreen = 256.0 * tileScale;

        double centerTileX = LonToTileX(centerLon, intTileZoom);
        double centerTileY = LatToTileY(centerLat, intTileZoom);

        Point ToScreen(double lat, double lon)
        {
            double tx = LonToTileX(lon, intTileZoom);
            double ty = LatToTileY(lat, intTileZoom);
            double x = (width / 2.0) + (tx - centerTileX) * tileSizeOnScreen + _panOffset.X;
            double y = (height / 2.0) + (ty - centerTileY) * tileSizeOnScreen + _panOffset.Y;
            return new Point(x, y);
        }

        // 1. DIBUJAR CAPA DE MAPA (OpenStreetMap / Carto / Topo / Vector)
        if (_activeLayer != MapLayerType.VectorOscuro)
        {
            // Determinar tiles visibles en viewport
            int minTx = (int)Math.Floor(centerTileX - (width / 2.0 + _panOffset.X) / tileSizeOnScreen);
            int maxTx = (int)Math.Ceiling(centerTileX + (width / 2.0 - _panOffset.X) / tileSizeOnScreen);
            int minTy = (int)Math.Floor(centerTileY - (height / 2.0 + _panOffset.Y) / tileSizeOnScreen);
            int maxTy = (int)Math.Ceiling(centerTileY + (height / 2.0 - _panOffset.Y) / tileSizeOnScreen);

            int maxCoord = (1 << intTileZoom) - 1;
            minTx = Math.Clamp(minTx, 0, maxCoord);
            maxTx = Math.Clamp(maxTx, 0, maxCoord);
            minTy = Math.Clamp(minTy, 0, maxCoord);
            maxTy = Math.Clamp(maxTy, 0, maxCoord);

            string providerPrefix = _activeLayer switch
            {
                MapLayerType.CartoVoyager => "carto",
                MapLayerType.OpenTopoMap => "topo",
                _ => "osm"
            };

            int count = 0;
            for (int tx = minTx; tx <= maxTx && count < 64; tx++)
            {
                for (int ty = minTy; ty <= maxTy && count < 64; ty++)
                {
                    count++;
                    double sx = (width / 2.0) + (tx - centerTileX) * tileSizeOnScreen + _panOffset.X;
                    double sy = (height / 2.0) + (ty - centerTileY) * tileSizeOnScreen + _panOffset.Y;
                    var destRect = new Rect(sx, sy, tileSizeOnScreen + 0.5, tileSizeOnScreen + 0.5);

                    var tileBmp = GetOrFetchTile(intTileZoom, tx, ty, _activeLayer);
                    if (tileBmp != null)
                    {
                        context.DrawImage(tileBmp, new Rect(0, 0, tileBmp.PixelSize.Width, tileBmp.PixelSize.Height), destRect);
                    }
                    else
                    {
                        // Fallback suave a tile padre (intTileZoom - 1) para evitar parpadeos
                        Bitmap? parentBmp = null;
                        if (intTileZoom > 1)
                        {
                            string parentKey = $"{providerPrefix}_{intTileZoom - 1}_{tx / 2}_{ty / 2}";
                            _tileMemoryCache.TryGetValue(parentKey, out parentBmp);
                        }

                        if (parentBmp != null)
                        {
                            double srcW = parentBmp.PixelSize.Width / 2.0;
                            double srcH = parentBmp.PixelSize.Height / 2.0;
                            double srcX = (tx % 2) * srcW;
                            double srcY = (ty % 2) * srcH;
                            context.DrawImage(parentBmp, new Rect(srcX, srcY, srcW, srcH), destRect);
                        }
                        else
                        {
                            var phPen = new Pen(new SolidColorBrush(Color.Parse("#D1D5DB")), 0.5);
                            context.DrawRectangle(new SolidColorBrush(Color.Parse("#F3F4F6")), phPen, destRect);
                        }
                    }
                }
            }
        }
        else
        {
            // Modo vectorial oscuro con cuadrícula
            var gridPen = new Pen(new SolidColorBrush(Color.Parse("#131D33")), 1);
            double gridSize = 40 * _zoomFactor;
            while (gridSize < 30) gridSize *= 2;
            while (gridSize > 120) gridSize /= 2;

            double startX = (_panOffset.X % gridSize);
            double startY = (_panOffset.Y % gridSize);
            for (double x = startX; x < width; x += gridSize)
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, height));
            for (double y = startY; y < height; y += gridSize)
                context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        // 2. DIBUJAR TRAZA GPS
        if (traza != null && traza.Count > 1)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                var pt0 = ToScreen(traza[0].Latitud, traza[0].Longitud);
                ctx.BeginFigure(pt0, false);
                for (int i = 1; i < traza.Count; i++)
                {
                    ctx.LineTo(ToScreen(traza[i].Latitud, traza[i].Longitud));
                }
                ctx.EndFigure(false);
            }

            // Glow / Sombra exterior para máxima legibilidad sobre el mapa
            var glowPen = new Pen(new SolidColorBrush(Color.Parse("#065F46"), 0.5), 8);
            context.DrawGeometry(null, glowPen, geo);

            // Línea principal en verde esmeralda brillante
            var mainPen = new Pen(new SolidColorBrush(Color.Parse("#10B981")), 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, mainPen, geo);
        }

        // 3. DIBUJAR PUNTOS DE CONTROL (WAYPOINTS / FAROS)
        if (puntos != null)
        {
            int idx = 1;
            PuntoRuta? hoveredPointToDrawLast = null;
            Point hoveredScr = default;
            int hoveredIdx = 0;

            foreach (var pt in puntos)
            {
                var scr = ToScreen(pt.Latitud, pt.Longitud);
                bool hasPhoto = pt.TieneFoto;
                bool isHovered = ReferenceEquals(_hoveredPunto, pt);

                if (isHovered)
                {
                    hoveredPointToDrawLast = pt;
                    hoveredScr = scr;
                    hoveredIdx = idx;
                }

                double radius = hasPhoto ? (isHovered ? 13 : 11) : (isHovered ? 10 : 8);
                var circleColor = hasPhoto ? Color.Parse("#EC4899") : (pt.TieneConectividad ? Color.Parse("#0284C7") : Color.Parse("#F59E0B"));

                // Sombra de contraste
                context.DrawEllipse(new SolidColorBrush(Colors.Black, 0.45), null, new Point(scr.X, scr.Y + 2), radius + 2, radius + 2);

                // Círculo del punto
                context.DrawEllipse(new SolidColorBrush(circleColor), new Pen(Brushes.White, isHovered ? 3 : 2), scr, radius, radius);

                if (hasPhoto)
                {
                    context.DrawEllipse(Brushes.White, null, scr, 3.5, 3.5);
                }

                if (!isHovered && hasPhoto)
                {
                    string label = $"📷 #{idx}";
                    var labelText = new FormattedText(
                        label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
                        10,
                        Brushes.White
                    );

                    // Badge de fondo oscuro para el texto
                    var badgeRect = new Rect(scr.X - labelText.Width / 2 - 5, scr.Y - radius - 16, labelText.Width + 10, 16);
                    context.DrawRectangle(new SolidColorBrush(Color.Parse("#0F172A"), 0.85), null, new RoundedRect(badgeRect, 4));
                    context.DrawText(labelText, new Point(scr.X - labelText.Width / 2, scr.Y - radius - 15));
                }

                idx++;
            }

            // Dibujar el tooltip del faro bajo el cursor por encima de todo
            if (hoveredPointToDrawLast != null)
            {
                bool hasPhoto = hoveredPointToDrawLast.TieneFoto;
                string puntoNombre = string.IsNullOrWhiteSpace(hoveredPointToDrawLast.Descripcion)
                    ? $"Faro #{hoveredIdx}"
                    : $"Faro #{hoveredIdx}: {hoveredPointToDrawLast.Descripcion}";
                string title = hasPhoto ? $"📷 {puntoNombre}" : $"📍 {puntoNombre}";
                string coords = $"\n{hoveredPointToDrawLast.Latitud:F4}, {hoveredPointToDrawLast.Longitud:F4}";
                string hint = hasPhoto ? "\n(Clic para ver foto)" : "";

                string fullText = $"{title}{coords}{hint}";
                var labelText = new FormattedText(
                    fullText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
                    11,
                    Brushes.White
                );

                double bWidth = labelText.Width + 16;
                double bHeight = labelText.Height + 12;
                double bX = Math.Clamp(hoveredScr.X - bWidth / 2, 8, width - bWidth - 8);
                double bY = hoveredScr.Y - bHeight - 16;
                if (bY < 8) bY = hoveredScr.Y + 16;

                var badgeRect = new Rect(bX, bY, bWidth, bHeight);
                context.DrawRectangle(new SolidColorBrush(Color.Parse("#0F172A"), 0.95), new Pen(new SolidColorBrush(Color.Parse("#38BDF8")), 1.5), new RoundedRect(badgeRect, 6));
                context.DrawText(labelText, new Point(bX + 8, bY + 6));
            }
        }

        // 4. Marcador de Inicio (Salida)
        if (traza != null && traza.Count > 0)
        {
            var startPt = ToScreen(traza[0].Latitud, traza[0].Longitud);
            context.DrawEllipse(new SolidColorBrush(Colors.Black, 0.4), null, new Point(startPt.X, startPt.Y + 2), 11, 11);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#2563EB")), new Pen(Brushes.White, 2.5), startPt, 9, 9);
            var startText = new FormattedText("INICIO", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold), 10, Brushes.White);
            var startBadge = new Rect(startPt.X - startText.Width / 2 - 4, startPt.Y + 11, startText.Width + 8, 15);
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#1E3A8A"), 0.9), null, new RoundedRect(startBadge, 3));
            context.DrawText(startText, new Point(startPt.X - startText.Width / 2, startPt.Y + 11));
        }

        // 5. Marcador de Fin (Meta)
        if (traza != null && traza.Count > 1)
        {
            var endPt = ToScreen(traza[^1].Latitud, traza[^1].Longitud);
            context.DrawEllipse(new SolidColorBrush(Colors.Black, 0.4), null, new Point(endPt.X, endPt.Y + 2), 11, 11);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#DC2626")), new Pen(Brushes.White, 2.5), endPt, 9, 9);
            var endText = new FormattedText("FIN", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold), 10, Brushes.White);
            var endBadge = new Rect(endPt.X - endText.Width / 2 - 4, endPt.Y + 11, endText.Width + 8, 15);
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#7F1D1D"), 0.9), null, new RoundedRect(endBadge, 3));
            context.DrawText(endText, new Point(endPt.X - endText.Width / 2, endPt.Y + 11));
        }

        // 6. HUD de Información y Atribución OpenStreetMap
        var hudBg = new SolidColorBrush(Color.Parse("#0F172A"), 0.85);
        var hudBorder = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1);
        var hudRect = new Rect(12, 12, 230, 68);
        context.DrawRectangle(hudBg, hudBorder, new RoundedRect(hudRect, 8));

        string layerName = _activeLayer switch
        {
            MapLayerType.OpenStreetMap => "OpenStreetMap (Libre)",
            MapLayerType.CartoVoyager => "Carto Voyager (Calles & Senderos)",
            MapLayerType.OpenTopoMap => "OpenTopoMap (Topográfico)",
            _ => "Vectorial Oscuro"
        };

        string statsStr = $"Mapa: {layerName}\nTraza: {traza?.Count ?? 0} nodos | Puntos: {puntos?.Count ?? 0}\nZoom: {effectiveZoom:F1}x (Nivel {intTileZoom})";
        var hudText = new FormattedText(
            statsStr,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
            10.5,
            new SolidColorBrush(Color.Parse("#94A3B8"))
        );
        context.DrawText(hudText, new Point(20, 18));

        // Atribución OSM obligatoria en la esquina inferior derecha
        if (_activeLayer != MapLayerType.VectorOscuro)
        {
            var attrText = new FormattedText(
                "© OpenStreetMap contributors",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Medium),
                9,
                new SolidColorBrush(Color.Parse("#475569"))
            );
            context.DrawText(attrText, new Point(width - attrText.Width - 10, height - attrText.Height - 6));
        }
    }

    private Bitmap? GetOrFetchTile(int zoom, int x, int y, MapLayerType layer)
    {
        string providerPrefix = layer switch
        {
            MapLayerType.CartoVoyager => "carto",
            MapLayerType.OpenTopoMap => "topo",
            _ => "osm"
        };

        string cacheKey = $"{providerPrefix}_{zoom}_{x}_{y}";

        if (_tileMemoryCache.TryGetValue(cacheKey, out var memBitmap))
        {
            return memBitmap;
        }

        string localFile = Path.Combine(_cacheDirectory, $"{cacheKey}.png");
        if (File.Exists(localFile))
        {
            try
            {
                using var fs = File.OpenRead(localFile);
                var bmp = new Bitmap(fs);
                _tileMemoryCache[cacheKey] = bmp;
                return bmp;
            }
            catch
            {
                try { File.Delete(localFile); } catch { }
            }
        }

        // Descarga asíncrona
        if (_pendingDownloads.TryAdd(cacheKey, 1))
        {
            _ = Task.Run(async () =>
            {
                await _downloadThrottle.WaitAsync();
                try
                {
                    string url = layer switch
                    {
                        MapLayerType.CartoVoyager => $"https://basemaps.cartocdn.com/rastertiles/voyager/{zoom}/{x}/{y}.png",
                        MapLayerType.OpenTopoMap => $"https://tile.opentopomap.org/{zoom}/{x}/{y}.png",
                        _ => $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png"
                    };

                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        try
                        {
                            await File.WriteAllBytesAsync(localFile, bytes);
                        }
                        catch { }

                        using var ms = new MemoryStream(bytes);
                        var loadedBmp = new Bitmap(ms);
                        _tileMemoryCache[cacheKey] = loadedBmp;

                        Dispatcher.UIThread.Post(() => InvalidateVisual());
                    }
                }
                catch
                {
                    // Fallback silencioso si no hay conexión en este instante
                }
                finally
                {
                    _downloadThrottle.Release();
                    _pendingDownloads.TryRemove(cacheKey, out _);
                }
            });
        }

        return null;
    }

    private void CheckHoveredPoint(Point mousePos)
    {
        var puntos = Puntos;
        if (puntos == null || puntos.Count == 0) return;

        if (!TryGetBounds(out double minLat, out double maxLat, out double minLon, out double maxLon))
            return;

        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;

        double effectiveZoom = GetEffectiveZoom(_zoomFactor, Bounds.Width, Bounds.Height);
        int intTileZoom = Math.Clamp((int)Math.Round(effectiveZoom), 1, 19);
        double tileScale = Math.Pow(2.0, effectiveZoom - intTileZoom);
        double tileSizeOnScreen = 256.0 * tileScale;

        double centerTileX = LonToTileX(centerLon, intTileZoom);
        double centerTileY = LatToTileY(centerLat, intTileZoom);

        PuntoRuta? found = null;
        foreach (var pt in puntos)
        {
            double tx = LonToTileX(pt.Longitud, intTileZoom);
            double ty = LatToTileY(pt.Latitud, intTileZoom);
            double sx = (Bounds.Width / 2.0) + (tx - centerTileX) * tileSizeOnScreen + _panOffset.X;
            double sy = (Bounds.Height / 2.0) + (ty - centerTileY) * tileSizeOnScreen + _panOffset.Y;

            double dx = mousePos.X - sx;
            double dy = mousePos.Y - sy;
            if (Math.Sqrt(dx * dx + dy * dy) <= 18)
            {
                found = pt;
                break;
            }
        }

        if (!ReferenceEquals(_hoveredPunto, found))
        {
            _hoveredPunto = found;
            if (!_isDragging)
            {
                Cursor = found != null ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);
            }
            InvalidateVisual();
        }
    }

    // Funciones matemáticas estándar de Web Mercator (EPSG:3857)
    private static double LonToMercatorX(double lon) => (lon + 180.0) / 360.0;

    private static double LatToMercatorY(double lat)
    {
        double latRad = Math.Clamp(lat, -85.05112878, 85.05112878) * Math.PI / 180.0;
        return (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0;
    }

    private static double LonToTileX(double lon, int zoom) => LonToMercatorX(lon) * (1 << zoom);

    private static double LatToTileY(double lat, int zoom) => LatToMercatorY(lat) * (1 << zoom);
}
