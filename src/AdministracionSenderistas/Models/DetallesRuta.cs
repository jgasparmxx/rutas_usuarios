using System.Collections.Generic;

namespace AdministracionSenderistas.Models;

public class DetallesRuta
{
    public Dictionary<string, object?>? RutaRaw { get; set; }
    public List<PuntoRuta> Puntos { get; set; } = new();
    public List<TrazaPunto> Traza { get; set; } = new();

    public int TotalPuntos => Puntos?.Count ?? 0;
    public int TotalPuntosConFoto => Puntos?.FindAll(p => p.TieneFoto).Count ?? 0;
    public int TotalTraza => Traza?.Count ?? 0;
    public bool TieneDatos => TotalPuntos > 0 || TotalTraza > 0;
}
