using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Services;

public class DataRepository : IDataRepository
{
    private readonly IPostgrestApiClient _apiClient;
    private readonly ISqliteCacheService _cacheService;
    private readonly IAuthService _authService;

    public event Action<string>? OnSyncStatusChanged;
    public bool IsOnline { get; private set; } = true;

    public DataRepository(
        IPostgrestApiClient apiClient,
        ISqliteCacheService cacheService,
        IAuthService authService)
    {
        _apiClient = apiClient;
        _cacheService = cacheService;
        _authService = authService;
    }

    private string CurrentToken => _authService.CurrentSession?.Token ?? string.Empty;

    private void NotifyStatus(string message)
    {
        OnSyncStatusChanged?.Invoke(message);
    }

    // --- SENDERISTAS ---
    public async Task<List<Senderista>> GetSenderistasAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<Senderista>>(
                "/senderistas?order=nombre.asc",
                CurrentToken);

            if (apiData != null && apiData.Count >= 0)
            {
                IsOnline = true;
                await _cacheService.ReplaceSenderistasAsync(apiData);
                NotifyStatus($"Senderistas actualizados desde API ({apiData.Count} registros sincronizados con caché)");
                return apiData;
            }
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Modo sin conexión API: {ex.Message}. Mostrando datos locales de SQLite.");
        }

        var localData = await _cacheService.GetSenderistasAsync();
        return localData;
    }

    public async Task<bool> SaveSenderistaAsync(Senderista senderista)
    {
        // 1. Guardar en API si hay conexión
        bool apiSuccess = false;
        try
        {
            // Intentar primero PATCH con los campos modificables (rol, bloqueado, minimo_longitud)
            var patchPayload = new
            {
                rol = senderista.Rol,
                bloqueado = senderista.Bloqueado,
                minimo_longitud = senderista.MinimoLongitud ?? 0
            };
            string patchEndpoint = $"/senderistas?email=eq.{Uri.EscapeDataString(senderista.Email)}";
            apiSuccess = await _apiClient.PatchAsync(patchEndpoint, patchPayload, CurrentToken);

            if (!apiSuccess)
            {
                // Si PATCH no coincidió (registro nuevo), enviar POST completo
                await _apiClient.PostAsync<List<Senderista>>("/senderistas", senderista, CurrentToken, upsert: true);
                apiSuccess = true;
            }
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        // 2. Siempre persistir en SQLite local como caché inmediata
        await _cacheService.UpsertSenderistaAsync(senderista);

        if (apiSuccess)
        {
            NotifyStatus($"Senderista '{senderista.Email}' guardado con éxito en API y caché local.");
        }
        else
        {
            NotifyStatus($"Guardado en caché SQLite local (sin conexión con API).");
        }

        return true;
    }

    public async Task<bool> DeleteSenderistaAsync(string email)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/senderistas?email=eq.{Uri.EscapeDataString(email)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteSenderistaAsync(email);
        NotifyStatus(apiSuccess
            ? $"Senderista '{email}' eliminado en API y caché local."
            : $"Senderista '{email}' eliminado del caché local.");
        return true;
    }

    // --- RUTAS ---
    public async Task<List<Ruta>> GetRutasAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<Ruta>>(
                "/rutas?order=id.desc",
                CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                await _cacheService.ReplaceRutasAsync(apiData);
                NotifyStatus($"Rutas actualizadas desde API ({apiData.Count} registros sincronizados con caché)");
                return apiData;
            }
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Modo sin conexión API en Rutas: {ex.Message}. Cargado desde SQLite.");
        }

        return await _cacheService.GetRutasAsync();
    }

    public async Task<bool> SaveRutaAsync(Ruta ruta)
    {
        bool apiSuccess = false;
        try
        {
            // Intentar primero PATCH por ID (estándar REST en PostgREST para actualizar filas existentes)
            bool patchOk = await _apiClient.PatchAsync($"/rutas?id=eq.{Uri.EscapeDataString(ruta.Id)}", ruta, CurrentToken);
            if (patchOk)
            {
                apiSuccess = true;
                IsOnline = true;
            }
            else
            {
                // Si PATCH devolvió false (ej: no encontró registro por id o es nueva), intentar POST con upsert
                await _apiClient.PostAsync<List<Ruta>>("/rutas", ruta, CurrentToken, upsert: true);
                apiSuccess = true;
                IsOnline = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DataRepository] Error al guardar ruta en API: {ex.Message}");
            IsOnline = false;
        }

        await _cacheService.UpsertRutaAsync(ruta);
        NotifyStatus(apiSuccess ? $"Ruta '{ruta.Nombre}' guardada en API y caché." : $"Ruta guardada en caché local.");
        return apiSuccess;
    }

    public async Task<bool> DeleteRutaAsync(string id)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/rutas?id=eq.{Uri.EscapeDataString(id)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteRutaAsync(id);
        NotifyStatus(apiSuccess ? $"Ruta '{id}' eliminada en API y caché." : $"Ruta '{id}' eliminada del caché local.");
        return true;
    }

    // --- AGRUPACIONES ---
    public async Task<List<Agrupacion>> GetAgrupacionesAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<Agrupacion>>(
                "/agrupaciones?order=nombre.asc",
                CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                await _cacheService.ReplaceAgrupacionesAsync(apiData);
                NotifyStatus($"Agrupaciones sincronizadas ({apiData.Count} registros)");
                return apiData;
            }
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Sin conexión API en Agrupaciones: {ex.Message}. Usando SQLite.");
        }

        return await _cacheService.GetAgrupacionesAsync();
    }

    public async Task<bool> SaveAgrupacionAsync(Agrupacion agrupacion)
    {
        bool apiSuccess = false;
        try
        {
            bool patchOk = await _apiClient.PatchAsync($"/agrupaciones?id_agrupacion=eq.{Uri.EscapeDataString(agrupacion.IdAgrupacion)}", agrupacion, CurrentToken);
            if (!patchOk)
            {
                await _apiClient.PostAsync<List<Agrupacion>>("/agrupaciones", agrupacion, CurrentToken, upsert: true);
            }
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            try
            {
                await _apiClient.PostAsync<List<Agrupacion>>("/agrupaciones", agrupacion, CurrentToken, upsert: true);
                apiSuccess = true;
                IsOnline = true;
            }
            catch
            {
                IsOnline = false;
            }
        }

        await _cacheService.UpsertAgrupacionAsync(agrupacion);
        NotifyStatus(apiSuccess ? $"Agrupación '{agrupacion.Nombre}' guardada en API y caché." : "Guardado en caché local.");
        return true;
    }

    public async Task<bool> DeleteAgrupacionAsync(string idAgrupacion)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/agrupaciones?id_agrupacion=eq.{Uri.EscapeDataString(idAgrupacion)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteAgrupacionAsync(idAgrupacion);
        NotifyStatus(apiSuccess ? $"Agrupación '{idAgrupacion}' eliminada." : $"Eliminada de caché.");
        return true;
    }

    // --- INVITACIONES ---
    public async Task<List<InvitacionRuta>> GetInvitacionesAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<InvitacionRuta>>(
                "/invitaciones_ruta?order=id_invitacion.desc",
                CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                await _cacheService.ReplaceInvitacionesAsync(apiData);
                return apiData;
            }
        }
        catch
        {
            IsOnline = false;
        }

        return await _cacheService.GetInvitacionesAsync();
    }

    public async Task<bool> SaveInvitacionAsync(InvitacionRuta invitacion)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.PostAsync<List<InvitacionRuta>>("/invitaciones_ruta", invitacion, CurrentToken, upsert: true);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.UpsertInvitacionAsync(invitacion);
        NotifyStatus(apiSuccess ? $"Invitación '{invitacion.Titulo}' guardada en API y caché." : "Invitación guardada en caché local.");
        return true;
    }

    public async Task<bool> DeleteInvitacionAsync(string idInvitacion)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/invitaciones_ruta?id_invitacion=eq.{Uri.EscapeDataString(idInvitacion)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteInvitacionAsync(idInvitacion);
        NotifyStatus(apiSuccess ? $"Invitación '{idInvitacion}' eliminada en API y caché." : "Invitación eliminada en caché local.");
        return true;
    }

    // --- PARTICIPANTES ---
    public async Task<List<ParticipanteInvitacion>> GetParticipantesAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<ParticipanteInvitacion>>(
                "/participantes_invitacion?order=fecha_registro.desc",
                CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                await _cacheService.ReplaceParticipantesAsync(apiData);
                return apiData;
            }
        }
        catch
        {
            IsOnline = false;
        }

        return await _cacheService.GetParticipantesAsync();
    }

    public async Task<bool> SaveParticipanteAsync(ParticipanteInvitacion participante)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.PostAsync<List<ParticipanteInvitacion>>("/participantes_invitacion", participante, CurrentToken, upsert: true);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.UpsertParticipanteAsync(participante);
        NotifyStatus(apiSuccess ? $"Participante '{participante.EmailSenderista}' guardado en API y caché." : "Participante guardado en caché local.");
        return true;
    }

    public async Task<bool> DeleteParticipanteAsync(string idInvitacion, string emailSenderista)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/participantes_invitacion?id_invitacion=eq.{Uri.EscapeDataString(idInvitacion)}&email_senderista=eq.{Uri.EscapeDataString(emailSenderista)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteParticipanteAsync(idInvitacion, emailSenderista);
        NotifyStatus(apiSuccess ? $"Participante '{emailSenderista}' eliminado en API y caché." : "Participante eliminado en caché local.");
        return true;
    }

    // --- FRASES DIFICULTAD ---
    public async Task<List<FraseDificultad>> GetFrasesDificultadAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<FraseDificultad>>(
                "/frases_dificultad?order=id.asc",
                CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                await _cacheService.ReplaceFrasesDificultadAsync(apiData);
                NotifyStatus($"Frases de dificultad sincronizadas ({apiData.Count} registros)");
                return apiData;
            }
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Sin conexión API en Frases: {ex.Message}. Usando SQLite.");
        }

        return await _cacheService.GetFrasesDificultadAsync();
    }

    public async Task<bool> SaveFraseDificultadAsync(FraseDificultad frase)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.PostAsync<List<FraseDificultad>>("/frases_dificultad", frase, CurrentToken, upsert: true);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.UpsertFraseDificultadAsync(frase);
        NotifyStatus(apiSuccess ? $"Frase '{frase.Id}' guardada en API y caché." : "Frase guardada en caché local.");
        return true;
    }

    public async Task<bool> DeleteFraseDificultadAsync(string id)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/frases_dificultad?id=eq.{Uri.EscapeDataString(id)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteFraseDificultadAsync(id);
        NotifyStatus(apiSuccess ? $"Frase '{id}' eliminada en API y caché." : "Frase eliminada en caché local.");
        return true;
    }

    // --- REQUISITOS SENDERISMO ---
    public async Task<List<RequisitoSenderismo>> GetRequisitosSenderismoAsync(bool forceRefresh = false)
    {
        try
        {
            var apiData = await _apiClient.GetAsync<List<RequisitoSenderismo>>(
                "/requisitos_senderismo?order=id.asc",
                CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                await _cacheService.ReplaceRequisitosSenderismoAsync(apiData);
                NotifyStatus($"Requisitos de senderismo sincronizados ({apiData.Count} registros)");
                return apiData;
            }
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Sin conexión API en Requisitos: {ex.Message}. Usando SQLite.");
        }

        return await _cacheService.GetRequisitosSenderismoAsync();
    }

    public async Task<bool> SaveRequisitoSenderismoAsync(RequisitoSenderismo requisito)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.PostAsync<List<RequisitoSenderismo>>("/requisitos_senderismo", requisito, CurrentToken, upsert: true);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.UpsertRequisitoSenderismoAsync(requisito);
        NotifyStatus(apiSuccess ? $"Requisito '{requisito.Id}' guardado en API y caché." : "Requisito guardado en caché local.");
        return true;
    }

    public async Task<bool> DeleteRequisitoSenderismoAsync(string id)
    {
        bool apiSuccess = false;
        try
        {
            await _apiClient.DeleteAsync($"/requisitos_senderismo?id=eq.{Uri.EscapeDataString(id)}", CurrentToken);
            apiSuccess = true;
            IsOnline = true;
        }
        catch
        {
            IsOnline = false;
        }

        await _cacheService.DeleteRequisitoSenderismoAsync(id);
        NotifyStatus(apiSuccess ? $"Requisito '{id}' eliminado en API y caché." : "Requisito eliminado en caché local.");
        return true;
    }

    // --- SINCRONIZACIÓN GENERAL ---
    public async Task<(bool Success, string Message)> SyncAllTablesAsync()
    {
        try
        {
            NotifyStatus("Iniciando sincronización completa con PostgREST...");

            var s = await GetSenderistasAsync(true);
            var r = await GetRutasAsync(true);
            var a = await GetAgrupacionesAsync(true);
            var i = await GetInvitacionesAsync(true);
            var p = await GetParticipantesAsync(true);
            var f = await GetFrasesDificultadAsync(true);
            var req = await GetRequisitosSenderismoAsync(true);

            string summary = $"Sincronización exitosa: {s.Count} senderistas, {r.Count} rutas, {a.Count} agrupaciones, {i.Count} invitaciones, {p.Count} participantes, {f.Count} frases, {req.Count} requisitos en caché SQLite.";
            NotifyStatus(summary);
            return (true, summary);
        }
        catch (Exception ex)
        {
            string err = $"Error en sincronización: {ex.Message}";
            NotifyStatus(err);
            return (false, err);
        }
    }
}
