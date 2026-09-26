using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace AdministracionSenderistas.Services;

public static class ImageGeneratorService
{
    public const int DefaultWidth = 800;
    public const int DefaultHeight = 600;

    /// <summary>
    /// Genera una imagen JPG con fondo amarillo y texto en negrita negra exponiendo el motivo de eliminación.
    /// Retorna los bytes del archivo JPG.
    /// </summary>
    public static byte[] GenerateMotiveJpgBytes(string motivo, string? puntoInfo = null, int width = DefaultWidth, int height = DefaultHeight)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        // Fondo Amarillo vibrante
        var yellowColor = SKColor.Parse("#FACC15");
        canvas.Clear(yellowColor);

        // Borde decorativo de precaución / contraste
        using (var borderPaint = new SKPaint
        {
            Color = SKColor.Parse("#CA8A04"),
            StrokeWidth = 8,
            IsStroke = true,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(12, 12, width - 12, height - 12), 16, 16), borderPaint);
        }

        using (var innerBorderPaint = new SKPaint
        {
            Color = SKColor.Parse("#000000"),
            StrokeWidth = 3,
            IsStroke = true,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(20, 20, width - 20, height - 20), 12, 12), innerBorderPaint);
        }

        // Encabezado de advertencia
        using (var iconPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 44,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI Emoji", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        })
        {
            canvas.DrawText("⚠️", width / 2f, 75, iconPaint);
        }

        using (var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 26,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.ExtraBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        })
        {
            canvas.DrawText("FOTOGRAFÍA RETIRADA POR MODERACIÓN", width / 2f, 115, titlePaint);
        }

        // Línea divisoria negra
        using (var linePaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 3,
            IsAntialias = true
        })
        {
            canvas.DrawLine(60, 135, width - 60, 135, linePaint);
        }

        // Etiqueta: "MOTIVO DE RETIRO:"
        using (var labelPaint = new SKPaint
        {
            Color = SKColor.Parse("#1E293B"),
            TextSize = 18,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        })
        {
            canvas.DrawText("MOTIVO REGISTRADO:", width / 2f, 175, labelPaint);
        }

        // Cuadro contenedor blanco/crema para el motivo
        var boxRect = new SKRect(60, 200, width - 60, height - 120);
        using (var bgBoxPaint = new SKPaint
        {
            Color = SKColor.Parse("#FEF08A"),
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(new SKRoundRect(boxRect, 12, 12), bgBoxPaint);
        }

        using (var boxBorder = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 2,
            IsStroke = true,
            IsAntialias = true
        })
        {
            canvas.DrawRoundRect(new SKRoundRect(boxRect, 12, 12), boxBorder);
        }

        // Dibujar el motivo con letras bold negras y salto de línea (Word Wrap)
        string textoMotivo = string.IsNullOrWhiteSpace(motivo) ? "Imagen no apta para publicación" : motivo.Trim();
        using (var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 28,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        })
        {
            float maxLineWidth = boxRect.Width - 60;
            var lines = WrapText(textoMotivo, textPaint, maxLineWidth);

            float totalTextHeight = lines.Count * (textPaint.TextSize * 1.35f);
            float startY = boxRect.MidY - (totalTextHeight / 2f) + (textPaint.TextSize * 0.85f);

            for (int i = 0; i < lines.Count; i++)
            {
                canvas.DrawText(lines[i], width / 2f, startY + (i * textPaint.TextSize * 1.35f), textPaint);
            }
        }

        // Pie de imagen: Información adicional (Punto de ruta, fecha, auditoría)
        using (var footerPaint = new SKPaint
        {
            Color = SKColor.Parse("#334155"),
            TextSize = 14,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        })
        {
            string puntoStr = string.IsNullOrWhiteSpace(puntoInfo) ? "Punto de Control / Faro" : puntoInfo;
            string footer = $"{puntoStr}  •  Auditoría: {DateTime.Now:yyyy-MM-dd HH:mm}  •  Conservación de Traza Activa";
            canvas.DrawText(footer, width / 2f, height - 60, footerPaint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        return data.ToArray();
    }

    /// <summary>
    /// Genera la imagen JPG y devuelve una URI Base64 Data URL (data:image/jpeg;base64,...) lista para guardar en FotoPath.
    /// </summary>
    public static string GenerateMotiveJpgDataUrl(string motivo, string? puntoInfo = null)
    {
        byte[] bytes = GenerateMotiveJpgBytes(motivo, puntoInfo);
        string base64 = Convert.ToBase64String(bytes);
        return $"data:image/jpeg;base64,{base64}";
    }

    private static List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        var result = new List<string>();
        var paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var p in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(p))
            {
                result.Add(string.Empty);
                continue;
            }

            var words = p.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                float testWidth = paint.MeasureText(testLine);

                if (testWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    result.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                result.Add(currentLine);
            }
        }

        return result;
    }
}
