using System;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using AdministracionSenderistas.Services;

namespace AdministracionSenderistas.Tests;

class Program
{
    static async Task Main()
    {
        var apiClient = new PostgrestApiClient();
        var cacheService = new SqliteCacheService(@"C:\GASPAR\baches\rutas_usuarios\src\AdministracionSenderistas\bin\Debug\net9.0\data\senderistas_cache.db");
        await cacheService.InitializeAsync();

        var jwtService = new JwtService();
        var authService = new AuthService(jwtService, cacheService, apiClient);
        await authService.LoginAsync("JGRBalam", "Yomero");

        var repo = new DataRepository(apiClient, cacheService, authService);

        Console.WriteLine("Refreshing and syncing Agrupaciones from PostgREST...");
        var agrupaciones = await repo.GetAgrupacionesAsync(forceRefresh: true);
        foreach (var a in agrupaciones)
        {
            Console.WriteLine($"Agrupacion: ID={a.IdAgrupacion}, Nombre={a.Nombre}, Clave={a.Clave}");
        }

        Console.WriteLine("\nRefreshing and syncing Senderistas from PostgREST...");
        var senderistas = await repo.GetSenderistasAsync(forceRefresh: true);
        var lourdes = senderistas.Find(s => s.Email == "loudi1502@hotmail.com");
        Console.WriteLine($"Lourdes: Email={lourdes?.Email}, Nombre={lourdes?.Nombre}, Rol={lourdes?.Rol}, Agrupacion={lourdes?.IdAgrupacion}");
    }
}
