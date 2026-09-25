using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Services;

public interface ISqliteCacheService
{
    Task InitializeAsync();
    string DatabasePath { get; }

    // Senderistas
    Task<List<Senderista>> GetSenderistasAsync();
    Task UpsertSenderistaAsync(Senderista item);
    Task DeleteSenderistaAsync(string email);
    Task ReplaceSenderistasAsync(IEnumerable<Senderista> items);

    // Rutas
    Task<List<Ruta>> GetRutasAsync();
    Task UpsertRutaAsync(Ruta item);
    Task DeleteRutaAsync(string id);
    Task ReplaceRutasAsync(IEnumerable<Ruta> items);

    // Agrupaciones
    Task<List<Agrupacion>> GetAgrupacionesAsync();
    Task UpsertAgrupacionAsync(Agrupacion item);
    Task DeleteAgrupacionAsync(string idAgrupacion);
    Task ReplaceAgrupacionesAsync(IEnumerable<Agrupacion> items);

    // Invitaciones
    Task<List<InvitacionRuta>> GetInvitacionesAsync();
    Task UpsertInvitacionAsync(InvitacionRuta item);
    Task DeleteInvitacionAsync(string idInvitacion);
    Task ReplaceInvitacionesAsync(IEnumerable<InvitacionRuta> items);

    // Participantes
    Task<List<ParticipanteInvitacion>> GetParticipantesAsync();
    Task UpsertParticipanteAsync(ParticipanteInvitacion item);
    Task DeleteParticipanteAsync(string idInvitacion, string emailSenderista);
    Task ReplaceParticipantesAsync(IEnumerable<ParticipanteInvitacion> items);

    // Frases Dificultad
    Task<List<FraseDificultad>> GetFrasesDificultadAsync();
    Task UpsertFraseDificultadAsync(FraseDificultad item);
    Task DeleteFraseDificultadAsync(string id);
    Task ReplaceFrasesDificultadAsync(IEnumerable<FraseDificultad> items);

    // Requisitos Senderismo
    Task<List<RequisitoSenderismo>> GetRequisitosSenderismoAsync();
    Task UpsertRequisitoSenderismoAsync(RequisitoSenderismo item);
    Task DeleteRequisitoSenderismoAsync(string id);
    Task ReplaceRequisitosSenderismoAsync(IEnumerable<RequisitoSenderismo> items);

    // Sync Metadata
    Task SetLastSyncTimeAsync(string tableName);
    Task<DateTime?> GetLastSyncTimeAsync(string tableName);
}
