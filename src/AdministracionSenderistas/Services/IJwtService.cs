namespace AdministracionSenderistas.Services;

public interface IJwtService
{
    string GenerateToken(string email, string rol = "O", string telefono = "", string? idAgrupacion = null, int daysValid = 365);
    bool ValidateToken(string token, out string? email, out string? rol, out string? idAgrupacion);
}
