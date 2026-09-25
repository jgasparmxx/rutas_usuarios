using System.Threading.Tasks;
using AdministracionSenderistas.Models;

namespace AdministracionSenderistas.Services;

public interface IAuthService
{
    UserSession? CurrentSession { get; }
    Task<(bool Success, string Message, UserSession? Session)> LoginAsync(string identifier, string password);
    void Logout();
}
