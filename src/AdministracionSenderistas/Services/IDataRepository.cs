using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Services;

public interface IDataRepository
{
    event Action<string>? OnSyncStatusChanged;
    bool IsOnline { get; }

    Task<List<Senderista>> GetSenderistasAsync(bool forceRefresh = false);
    Task<bool> SaveSenderistaAsync(Senderista senderista);
    Task<bool> DeleteSenderistaAsync(string email);

    Task<List<Ruta>> GetRutasAsync(bool forceRefresh = false);
    Task<bool> SaveRutaAsync(Ruta ruta);
    Task<bool> DeleteRutaAsync(string id);

    Task<List<Agrupacion>> GetAgrupacionesAsync(bool forceRefresh = false);
    Task<bool> SaveAgrupacionAsync(Agrupacion agrupacion);
    Task<bool> DeleteAgrupacionAsync(string idAgrupacion);

    Task<List<InvitacionRuta>> GetInvitacionesAsync(bool forceRefresh = false);
    Task<bool> SaveInvitacionAsync(InvitacionRuta invitacion);
    Task<bool> DeleteInvitacionAsync(string idInvitacion);

    Task<List<ParticipanteInvitacion>> GetParticipantesAsync(bool forceRefresh = false);
    Task<bool> SaveParticipanteAsync(ParticipanteInvitacion participante);
    Task<bool> DeleteParticipanteAsync(string idInvitacion, string emailSenderista);

    Task<List<FraseDificultad>> GetFrasesDificultadAsync(bool forceRefresh = false);
    Task<bool> SaveFraseDificultadAsync(FraseDificultad frase);
    Task<bool> DeleteFraseDificultadAsync(string id);

    Task<List<RequisitoSenderismo>> GetRequisitosSenderismoAsync(bool forceRefresh = false);
    Task<bool> SaveRequisitoSenderismoAsync(RequisitoSenderismo requisito);
    Task<bool> DeleteRequisitoSenderismoAsync(string id);

    Task<(bool Success, string Message)> SyncAllTablesAsync();
}
