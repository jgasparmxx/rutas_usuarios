using System.Threading.Tasks;

namespace AdministracionSenderistas.Services;

public interface ICloudinaryService
{
    Task<string?> UploadImageAsync(byte[] imageBytes, string publicId, string folder = "rutas_senderismo");
}
