using System.Threading.Tasks;

namespace AdministracionSenderistas.Services;

public interface IPostgrestApiClient
{
    string BaseUrl { get; }
    Task<T?> GetAsync<T>(string endpointAndQuery, string token);
    Task<T?> PostAsync<T>(string endpoint, object data, string token, bool upsert = true);
    Task<bool> PatchAsync(string endpointAndQuery, object data, string token);
    Task<bool> DeleteAsync(string endpointAndQuery, string token);
    Task<bool> PingAsync();
}
