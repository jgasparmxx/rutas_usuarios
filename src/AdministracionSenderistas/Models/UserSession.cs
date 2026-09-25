namespace AdministracionSenderistas.Models;

public class UserSession
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "O", "A", etc.
    public string? IdAgrupacion { get; set; }
    public string Token { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool IsSuperUser => Role == "O";

    public string RoleDisplayName => Role switch
    {
        "O" => "Dueño / Superadministrador",
        "A" => "Administrador de Agrupación",
        "G" => "Guía",
        "S" => "Senderista",
        _ => Role
    };
}
