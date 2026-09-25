using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AdministracionSenderistas.Services;

public class JwtService : IJwtService
{
    public const string SecretKey = "mi_clave_secreta_super_segura_rutas_sendero_2026";

    public string GenerateToken(string email, string rol = "O", string telefono = "", string? idAgrupacion = null, int daysValid = 365)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(SecretKey);

        var claims = new[]
        {
            new Claim("role", "senderista"), // PostgreSQL DB role
            new Claim("email", email ?? string.Empty),
            new Claim("telefono", telefono ?? string.Empty),
            new Claim("rol", rol ?? "O"),     // App permission role: 'O', 'A', etc.
            new Claim("id_agrupacion", idAgrupacion ?? string.Empty)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(daysValid),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public bool ValidateToken(string token, out string? email, out string? rol, out string? idAgrupacion)
    {
        email = null;
        rol = null;
        idAgrupacion = null;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            email = principal.FindFirst("email")?.Value ?? principal.FindFirst(ClaimTypes.Email)?.Value;
            rol = principal.FindFirst("rol")?.Value;
            idAgrupacion = principal.FindFirst("id_agrupacion")?.Value;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
