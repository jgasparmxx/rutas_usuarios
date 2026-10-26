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
            var lastSync = await _cacheService.GetLastSyncTimeAsync("senderistas");
            string endpoint = "/senderistas?order=nombre.asc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/senderistas?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=nombre.asc";
            }

            var apiData = await _apiClient.GetAsync<List<Senderista>>(
                endpoint,
                CurrentToken);

            if (apiData != null && apiData.Count >= 0)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManySenderistasAsync(apiData);
                    NotifyStatus($"Senderistas actualizados (incremental: {apiData.Count} registros sincronizados)");
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceSenderistasAsync(apiData);
                    NotifyStatus($"Senderistas actualizados desde API ({apiData.Count} registros completos)");
                }
            }
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Modo sin conexión API: {ex.Message}. Mostrando datos locales de SQLite.");
        }

        return await _cacheService.GetSenderistasAsync();
    }

    public async Task<bool> SaveSenderistaAsync(Senderista senderista)
    {
        // 1. Siempre persistir en SQLite local como caché inmediata
        await _cacheService.UpsertSenderistaAsync(senderista);
        NotifyStatus($"Guardado en caché SQLite local.");

        // 2. Disparar a la API de forma asincrónica (en un hilo de fondo)
        _ = Task.Run(async () =>
        {
            try
            {
                senderista.Fupdate = null; // No mandar Fupdate, Postgres lo asigna con el trigger
                var patchPayload = new
                {
                    rol = senderista.Rol,
                    bloqueado = senderista.Bloqueado,
                    minimo_longitud = senderista.MinimoLongitud ?? 0
                };
                string patchEndpoint = $"/senderistas?email=eq.{Uri.EscapeDataString(senderista.Email)}";
                bool apiSuccess = await _apiClient.PatchAsync(patchEndpoint, patchPayload, CurrentToken);

                if (!apiSuccess)
                {
                    await _apiClient.PostAsync<List<Senderista>>("/senderistas", senderista, CurrentToken, upsert: true);
                }
                IsOnline = true;
                NotifyStatus($"Senderista '{senderista.Email}' sincronizado en background con API.");
            }
            catch (Exception ex)
            {
                IsOnline = false;
                NotifyStatus($"Error de sincronización background para Senderista '{senderista.Email}': {ex.Message}");
            }
        });

        return true;
    }

    public async Task<bool> DeleteSenderistaAsync(string email)
    {
        await _cacheService.DeleteSenderistaAsync(email);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/senderistas?email=eq.{Uri.EscapeDataString(email)}", CurrentToken);
                IsOnline = true;
                NotifyStatus($"Senderista '{email}' eliminado en API.");
            }
            catch
            {
                IsOnline = false;
            }
        });
        return true;
    }

    // --- RUTAS ---
    public async Task<List<Ruta>> GetRutasAsync(bool forceRefresh = false)
    {
        try
        {
            var lastSync = await _cacheService.GetLastSyncTimeAsync("rutas");
            string endpoint = "/rutas?order=id.desc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/rutas?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=id.desc";
            }

            var apiData = await _apiClient.GetAsync<List<Ruta>>(endpoint, CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManyRutasAsync(apiData);
                    NotifyStatus($"Rutas actualizadas (incremental: {apiData.Count} registros sincronizados)");
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceRutasAsync(apiData);
                    NotifyStatus($"Rutas actualizadas desde API ({apiData.Count} registros completos)");
                }
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
        await _cacheService.UpsertRutaAsync(ruta);
        NotifyStatus($"Ruta guardada en caché local.");

        _ = Task.Run(async () =>
        {
            try
            {
                ruta.Fupdate = null; // No mandar Fupdate
                bool patchOk = await _apiClient.PatchAsync($"/rutas?id=eq.{Uri.EscapeDataString(ruta.Id)}", ruta, CurrentToken);
                if (!patchOk)
                {
                    await _apiClient.PostAsync<List<Ruta>>("/rutas", ruta, CurrentToken, upsert: true);
                }
                IsOnline = true;
                NotifyStatus($"Ruta '{ruta.Nombre}' sincronizada en background con API.");
            }
            catch (Exception ex)
            {
                IsOnline = false;
                NotifyStatus($"Error de sincronización background para Ruta '{ruta.Nombre}': {ex.Message}");
            }
        });

        return true;
    }

    public async Task<bool> DeleteRutaAsync(string id)
    {
        await _cacheService.DeleteRutaAsync(id);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/rutas?id=eq.{Uri.EscapeDataString(id)}", CurrentToken);
                IsOnline = true;
                NotifyStatus($"Ruta '{id}' eliminada en API.");
            }
            catch
            {
                IsOnline = false;
            }
        });
        return true;
    }

    // --- AGRUPACIONES ---
    public async Task<List<Agrupacion>> GetAgrupacionesAsync(bool forceRefresh = false)
    {
        try
        {
            var lastSync = await _cacheService.GetLastSyncTimeAsync("agrupaciones");
            string endpoint = "/agrupaciones?order=nombre.asc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/agrupaciones?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=nombre.asc";
            }

            var apiData = await _apiClient.GetAsync<List<Agrupacion>>(endpoint, CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManyAgrupacionesAsync(apiData);
                    NotifyStatus($"Agrupaciones actualizadas (incremental: {apiData.Count} registros)");
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceAgrupacionesAsync(apiData);
                    NotifyStatus($"Agrupaciones sincronizadas completas ({apiData.Count} registros)");
                }
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
        await _cacheService.UpsertAgrupacionAsync(agrupacion);
        NotifyStatus("Sincronizando agrupación con servidor en la nube...");

        try
        {
            agrupacion.Fupdate = null;
            bool patchOk = await _apiClient.PatchAsync($"/agrupaciones?id_agrupacion=eq.{Uri.EscapeDataString(agrupacion.IdAgrupacion)}", agrupacion, CurrentToken);
            if (!patchOk)
            {
                await _apiClient.PostAsync<List<Agrupacion>>("/agrupaciones", agrupacion, CurrentToken, upsert: true);
            }
            IsOnline = true;
            NotifyStatus($"Agrupación '{agrupacion.Nombre}' sincronizada con éxito en la nube.");
            return true;
        }
        catch (Exception ex)
        {
            IsOnline = false;
            NotifyStatus($"Guardado localmente. Error al sincronizar con el servidor: {ex.Message}");
            return true;
        }
    }

    public async Task<bool> DeleteAgrupacionAsync(string idAgrupacion)
    {
        await _cacheService.DeleteAgrupacionAsync(idAgrupacion);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/agrupaciones?id_agrupacion=eq.{Uri.EscapeDataString(idAgrupacion)}", CurrentToken);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    // --- INVITACIONES ---
    public async Task<List<InvitacionRuta>> GetInvitacionesAsync(bool forceRefresh = false)
    {
        try
        {
            var lastSync = await _cacheService.GetLastSyncTimeAsync("invitaciones");
            string endpoint = "/invitaciones_ruta?order=id_invitacion.desc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/invitaciones_ruta?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=id_invitacion.desc";
            }

            var apiData = await _apiClient.GetAsync<List<InvitacionRuta>>(endpoint, CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManyInvitacionesAsync(apiData);
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceInvitacionesAsync(apiData);
                }
            }
        }
        catch { IsOnline = false; }

        return await _cacheService.GetInvitacionesAsync();
    }

    public async Task<bool> SaveInvitacionAsync(InvitacionRuta invitacion)
    {
        await _cacheService.UpsertInvitacionAsync(invitacion);
        _ = Task.Run(async () =>
        {
            try
            {
                invitacion.Fupdate = null;
                await _apiClient.PostAsync<List<InvitacionRuta>>("/invitaciones_ruta", invitacion, CurrentToken, upsert: true);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    public async Task<bool> DeleteInvitacionAsync(string idInvitacion)
    {
        await _cacheService.DeleteInvitacionAsync(idInvitacion);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/invitaciones_ruta?id_invitacion=eq.{Uri.EscapeDataString(idInvitacion)}", CurrentToken);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    // --- PARTICIPANTES ---
    public async Task<List<ParticipanteInvitacion>> GetParticipantesAsync(bool forceRefresh = false)
    {
        try
        {
            var lastSync = await _cacheService.GetLastSyncTimeAsync("participantes");
            string endpoint = "/participantes_invitacion?order=fecha_registro.desc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/participantes_invitacion?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=fecha_registro.desc";
            }

            var apiData = await _apiClient.GetAsync<List<ParticipanteInvitacion>>(endpoint, CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManyParticipantesAsync(apiData);
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceParticipantesAsync(apiData);
                }
            }
        }
        catch { IsOnline = false; }

        return await _cacheService.GetParticipantesAsync();
    }

    public async Task<bool> SaveParticipanteAsync(ParticipanteInvitacion participante)
    {
        await _cacheService.UpsertParticipanteAsync(participante);
        _ = Task.Run(async () =>
        {
            try
            {
                participante.Fupdate = null;
                await _apiClient.PostAsync<List<ParticipanteInvitacion>>("/participantes_invitacion", participante, CurrentToken, upsert: true);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    public async Task<bool> DeleteParticipanteAsync(string idInvitacion, string emailSenderista)
    {
        await _cacheService.DeleteParticipanteAsync(idInvitacion, emailSenderista);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/participantes_invitacion?id_invitacion=eq.{Uri.EscapeDataString(idInvitacion)}&email_senderista=eq.{Uri.EscapeDataString(emailSenderista)}", CurrentToken);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    // --- FRASES DIFICULTAD ---
    public async Task<List<FraseDificultad>> GetFrasesDificultadAsync(bool forceRefresh = false)
    {
        try
        {
            var lastSync = await _cacheService.GetLastSyncTimeAsync("frases_dificultad");
            string endpoint = "/frases_dificultad?order=id.asc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/frases_dificultad?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=id.asc";
            }

            var apiData = await _apiClient.GetAsync<List<FraseDificultad>>(endpoint, CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManyFrasesDificultadAsync(apiData);
                    NotifyStatus($"Frases de dificultad actualizadas incrementalmente ({apiData.Count} registros)");
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceFrasesDificultadAsync(apiData);
                    NotifyStatus($"Frases de dificultad sincronizadas completas ({apiData.Count} registros)");
                }
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
        await _cacheService.UpsertFraseDificultadAsync(frase);
        _ = Task.Run(async () =>
        {
            try
            {
                frase.Fupdate = null;
                await _apiClient.PostAsync<List<FraseDificultad>>("/frases_dificultad", frase, CurrentToken, upsert: true);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    public async Task<bool> DeleteFraseDificultadAsync(string id)
    {
        await _cacheService.DeleteFraseDificultadAsync(id);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/frases_dificultad?id=eq.{Uri.EscapeDataString(id)}", CurrentToken);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    // --- REQUISITOS SENDERISMO ---
    public async Task<List<RequisitoSenderismo>> GetRequisitosSenderismoAsync(bool forceRefresh = false)
    {
        try
        {
            var lastSync = await _cacheService.GetLastSyncTimeAsync("requisitos_senderismo");
            string endpoint = "/requisitos_senderismo?order=id.asc";
            if (lastSync.HasValue && !forceRefresh)
            {
                endpoint = $"/requisitos_senderismo?fupdate=gt.{lastSync.Value.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ")}&order=id.asc";
            }

            var apiData = await _apiClient.GetAsync<List<RequisitoSenderismo>>(endpoint, CurrentToken);

            if (apiData != null)
            {
                IsOnline = true;
                if (lastSync.HasValue && !forceRefresh && apiData.Count > 0)
                {
                    await _cacheService.UpsertManyRequisitosSenderismoAsync(apiData);
                    NotifyStatus($"Requisitos actualizados incrementalmente ({apiData.Count} registros)");
                }
                else if (apiData.Count > 0 || !lastSync.HasValue || forceRefresh)
                {
                    await _cacheService.ReplaceRequisitosSenderismoAsync(apiData);
                    NotifyStatus($"Requisitos de senderismo sincronizados ({apiData.Count} registros completos)");
                }
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
        await _cacheService.UpsertRequisitoSenderismoAsync(requisito);
        _ = Task.Run(async () =>
        {
            try
            {
                requisito.Fupdate = null;
                await _apiClient.PostAsync<List<RequisitoSenderismo>>("/requisitos_senderismo", requisito, CurrentToken, upsert: true);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    public async Task<bool> DeleteRequisitoSenderismoAsync(string id)
    {
        await _cacheService.DeleteRequisitoSenderismoAsync(id);
        _ = Task.Run(async () =>
        {
            try
            {
                await _apiClient.DeleteAsync($"/requisitos_senderismo?id=eq.{Uri.EscapeDataString(id)}", CurrentToken);
                IsOnline = true;
            }
            catch { IsOnline = false; }
        });
        return true;
    }

    // --- SINCRONIZACIÓN GENERAL ---
    public async Task<(bool Success, string Message)> SyncAllTablesAsync()
    {
        try
        {
            NotifyStatus("Iniciando sincronización con PostgREST (Incremental si es posible)...");

            var s = await GetSenderistasAsync(false);
            var r = await GetRutasAsync(false);
            var a = await GetAgrupacionesAsync(false);
            var i = await GetInvitacionesAsync(false);
            var p = await GetParticipantesAsync(false);
            var f = await GetFrasesDificultadAsync(false);
            var req = await GetRequisitosSenderismoAsync(false);

            string summary = $"Sincronización exitosa. Total en caché local: {s.Count} senderistas, {r.Count} rutas, {a.Count} agrupaciones, {i.Count} invitaciones, {p.Count} participantes, {f.Count} frases, {req.Count} requisitos.";
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
