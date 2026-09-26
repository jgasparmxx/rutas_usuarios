using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Services;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly ISqliteCacheService _cacheService;
    private readonly IPostgrestApiClient _apiClient;

    public UserSession? CurrentSession { get; private set; }

    public AuthService(
        IJwtService jwtService,
        ISqliteCacheService cacheService,
        IPostgrestApiClient apiClient)
    {
        _jwtService = jwtService;
        _cacheService = cacheService;
        _apiClient = apiClient;
    }

    public async Task<(bool Success, string Message, UserSession? Session)> LoginAsync(string identifier, string password)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Por favor ingrese usuario/correo y los últimos 4 dígitos de su teléfono.", null);
        }

        identifier = identifier.Trim();
        password = password.Trim();

        // 1. Super Usuario Maestro
        if (string.Equals(identifier, "JGRBalam", StringComparison.OrdinalIgnoreCase) &&
            (password == "Yomero" || password == "3835" || password == "9616603835"))
        {
            const string superEmail = "jgasparmxx@gmail.com";
            const string superTel = "9616603835";

            try
            {
                var localList = await _cacheService.GetSenderistasAsync();
                var match = localList.FirstOrDefault(s => string.Equals(s.Email, superEmail, StringComparison.OrdinalIgnoreCase));
                if (match != null && match.Bloqueado)
                {
                    return (false, "La cuenta se encuentra bloqueada. Contacte a soporte.", null);
                }
            }
            catch { }

            string token = _jwtService.GenerateToken(superEmail, "O", superTel, null);

            CurrentSession = new UserSession
            {
                Username = "JGRBalam (Super Usuario)",
                Email = superEmail,
                Telefono = superTel,
                Role = "O",
                IdAgrupacion = null,
                Token = token,
                IsAuthenticated = true
            };

            return (true, "Acceso concedido como Super Usuario (Rol 'O').", CurrentSession);
        }

        // 2. Senderistas con Rol 'O'
        // Intentar buscar en API primero, o fallback a SQLite Caché
        Senderista? senderista = null;
        try
        {
            // Usamos un token temporal de lectura para consultar
            string tempToken = _jwtService.GenerateToken(identifier, "O", "", null);
            var results = await _apiClient.GetAsync<List<Senderista>>($"/senderistas?email=eq.{Uri.EscapeDataString(identifier)}", tempToken);
            senderista = results?.FirstOrDefault();
        }
        catch
        {
            // Si la API falla o está offline, buscar en SQLite Cache
            var localList = await _cacheService.GetSenderistasAsync();
            senderista = localList.FirstOrDefault(s => string.Equals(s.Email, identifier, StringComparison.OrdinalIgnoreCase));
        }

        if (senderista == null)
        {
            return (false, "Usuario o credenciales no encontrados.", null);
        }

        if (senderista.Bloqueado)
        {
            return (false, "La cuenta se encuentra bloqueada. Contacte a soporte.", null);
        }

        // 2. Validación estricta: Únicamente senderistas con Rol 'O' (Dueño/Superadmin) pueden acceder a esta app
        if (!string.Equals(senderista.Rol, "O", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"Acceso denegado: El usuario tiene rol '{senderista.Rol}' ({senderista.RolDescripcion}). Únicamente los usuarios con Rol 'O' (Dueño / Super Administrador) tienen autorización para este panel.", null);
        }

        // Validación de Clave: Se solicitan los últimos 4 dígitos del número de teléfono registrado del senderista
        string cleanInputPassword = new string(password.Where(char.IsDigit).ToArray());
        string cleanStoredPhone = new string((senderista.Telefono ?? string.Empty).Where(char.IsDigit).ToArray());

        bool passwordMatches = false;
        if (!string.IsNullOrEmpty(cleanStoredPhone))
        {
            string last4StoredPhone = cleanStoredPhone.Length >= 4
                ? cleanStoredPhone.Substring(cleanStoredPhone.Length - 4)
                : cleanStoredPhone;

            if (!string.IsNullOrEmpty(cleanInputPassword))
            {
                // Acepta los últimos 4 dígitos requeridos, o el teléfono completo en caso de ser ingresado
                passwordMatches = cleanInputPassword == last4StoredPhone || cleanInputPassword == cleanStoredPhone;
            }
            else
            {
                string rawPhone = senderista.Telefono?.Trim() ?? string.Empty;
                string last4Raw = rawPhone.Length >= 4 ? rawPhone.Substring(rawPhone.Length - 4) : rawPhone;
                passwordMatches = string.Equals(password, last4Raw, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(password, rawPhone, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            passwordMatches = string.Equals(password, senderista.Telefono?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        if (!passwordMatches)
        {
            return (false, "Contraseña incorrecta (ingrese los últimos 4 dígitos de su teléfono).", null);
        }

        // Generar token JWT con claims del senderista
        string userToken = _jwtService.GenerateToken(
            senderista.Email,
            senderista.Rol,
            senderista.Telefono ?? string.Empty,
            senderista.IdAgrupacion);

        CurrentSession = new UserSession
        {
            Username = string.IsNullOrWhiteSpace(senderista.Nombre) ? senderista.Email : senderista.Nombre,
            Email = senderista.Email,
            Telefono = senderista.Telefono ?? string.Empty,
            Role = senderista.Rol,
            IdAgrupacion = senderista.IdAgrupacion,
            Token = userToken,
            IsAuthenticated = true
        };

        return (true, $"Bienvenido, {CurrentSession.Username} (Dueño / Superadmin).", CurrentSession);
    }

    public void Logout()
    {
        CurrentSession = null;
    }
}
