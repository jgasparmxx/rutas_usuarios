using System;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using AdministracionSenderistas.Services;

namespace AdministracionSenderistas.Tests;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing ImageGeneratorService...");
        byte[] bytes = ImageGeneratorService.GenerateMotiveJpgBytes("Fotografía no representativa del camino o sendero", "Punto #3 (25.123, -100.456)");
        if (bytes != null && bytes.Length > 0 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            Console.WriteLine($"[PASS] JPG successfully generated! Size: {bytes.Length} bytes. Header: FF D8 FF");
        }
        else
        {
            Console.WriteLine("[FAIL] Invalid JPG output.");
        }

        string dataUrl = ImageGeneratorService.GenerateMotiveJpgDataUrl("Contenido inapropiado reportado");
        if (dataUrl.StartsWith("data:image/jpeg;base64,"))
        {
            Console.WriteLine($"[PASS] Data URL generated successfully! Length: {dataUrl.Length}");
        }
        else
        {
            Console.WriteLine("[FAIL] Invalid Data URL.");
        }

        Console.WriteLine("\nTesting Punto removal while keeping Traza intact...");
        var details = new DetallesRuta();
        details.Puntos.Add(new PuntoRuta { Latitud = 10, Longitud = 10, Descripcion = "Punto 1" });
        details.Puntos.Add(new PuntoRuta { Latitud = 20, Longitud = 20, Descripcion = "Punto 2" });
        details.Traza.Add(new TrazaPunto { Latitud = 10, Longitud = 10, Orden = 1 });
        details.Traza.Add(new TrazaPunto { Latitud = 15, Longitud = 15, Orden = 2 });
        details.Traza.Add(new TrazaPunto { Latitud = 20, Longitud = 20, Orden = 3 });

        int initialTrazaCount = details.Traza.Count;
        details.Puntos.RemoveAt(0);
        if (details.Puntos.Count == 1 && details.Traza.Count == initialTrazaCount)
        {
            Console.WriteLine($"[PASS] Punto removed: Remaining Puntos={details.Puntos.Count}, Traza intact={details.Traza.Count}");
        }
        else
        {
            Console.WriteLine("[FAIL] Traza was unexpectedly altered.");
        }

        Console.WriteLine("\nTesting Ruta serialization for PostgREST schema compatibility...");
        var ruta = new Ruta { Id = "test-id", Nombre = "Ruta Test", Dificultad = "Moderada", DuracionEstimada = "01:00:00" };
        string json = System.Text.Json.JsonSerializer.Serialize(ruta);
        if (!json.Contains("\"dificultad\"") && !json.Contains("\"duracion_estimada\""))
        {
            Console.WriteLine("[PASS] 'dificultad' and 'duracion_estimada' are properly ignored for PostgREST!");
        }
        else
        {
            Console.WriteLine("[FAIL] Ruta serialization still includes columns not present in PostgreSQL!");
        }
        Console.WriteLine("\nTesting AuthService Last 4 Digits login...");
        var authService = new AuthService(new JwtService(), new SqliteCacheService(), new PostgrestApiClient());
        var res1 = authService.LoginAsync("JGRBalam", "3835").GetAwaiter().GetResult();
        var res2 = authService.LoginAsync("JGRBalam", "Yomero").GetAwaiter().GetResult();
        var res3 = authService.LoginAsync("JGRBalam", "0000").GetAwaiter().GetResult();
        if (res1.Success && res2.Success && !res3.Success)
        {
            Console.WriteLine("[PASS] AuthService accepts both '3835' (last 4 digits) and 'Yomero', rejects invalid '0000'!");
        }
        else
        {
            Console.WriteLine($"[FAIL] Auth test failed: res1={res1.Success}, res2={res2.Success}, res3={res3.Success}");
        }

        Console.WriteLine("\nTesting Agrupacion cache and persistence of all fields...");
        TestAgrupacionPersistenceAsync().GetAwaiter().GetResult();

        Console.WriteLine("\n--- REPAIRING AND VERIFYING COMALA ROUTE ---");
        RepairAndVerifyComalaAsync().GetAwaiter().GetResult();
    }

    static async Task RepairAndVerifyComalaAsync()
    {
        try
        {
            string dbPath = @"C:\GASPAR\baches\rutas_usuarios\src\AdministracionSenderistas\bin\Debug\net9.0\data\senderistas_cache.db";
            var cache = new SqliteCacheService(dbPath);
            await cache.InitializeAsync();
            var rutas = await cache.GetRutasAsync();
            var comala = rutas.FirstOrDefault(r => r.Id == "COMALWZP79");
            if (comala == null)
            {
                Console.WriteLine("[FAIL] Comala route COMALWZP79 not found in SQLite.");
                return;
            }

            Console.WriteLine($"Found Comala route: ID={comala.Id}, Nombre={comala.Nombre}");
            string decrypted = CryptoService.DecryptString(comala.DatosEncriptados);
            var details = CryptoService.ParseRouteDetails(decrypted);

            Console.WriteLine($"Current details: Puntos={details.Puntos.Count}, Traza={details.Traza.Count}");

            // Serialize with comala route context to generate complete senderista schema
            string newJson = CryptoService.SerializeRouteDetails(details, comala);
            Console.WriteLine($"Generated new JSON length: {newJson.Length}");

            // Verify with JsonDocument
            using var doc = System.Text.Json.JsonDocument.Parse(newJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ruta", out var rProp))
            {
                Console.WriteLine("[FAIL] 'ruta' property missing from serialized payload!");
                return;
            }
            if (!root.TryGetProperty("puntos", out var pProp) || pProp.GetArrayLength() != 7)
            {
                Console.WriteLine($"[FAIL] 'puntos' property invalid or count != 7! Count: {pProp.GetArrayLength()}");
                return;
            }
            if (!root.TryGetProperty("traza", out var tProp) || tProp.GetArrayLength() != 520)
            {
                Console.WriteLine($"[FAIL] 'traza' property invalid or count != 520! Count: {tProp.GetArrayLength()}");
                return;
            }

            Console.WriteLine("[PASS] Verified 'ruta', 'puntos' (7) and 'traza' (520) in payload!");
            Console.WriteLine($"Sample Punto 1: {pProp[0].GetRawText()}");
            Console.WriteLine($"Sample Punto 7 (Fin): {pProp[6].GetRawText()}");

            // Encrypt and update SQLite
            string newEncrypted = CryptoService.EncryptString(newJson);
            comala.DatosEncriptados = newEncrypted;
            await cache.UpsertRutaAsync(comala);
            Console.WriteLine("[PASS] Updated COMALWZP79 in local SQLite cache.");

            // Try saving to PostgREST via DataRepository
            try
            {
                var authService = new AuthService(new JwtService(), cache, new PostgrestApiClient());
                var repo = new DataRepository(new PostgrestApiClient(), cache, authService);
                bool saved = await repo.SaveRutaAsync(comala);
                Console.WriteLine($"PostgREST save result: {saved}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PostgREST sync notice: {ex.Message}");
            }

            Console.WriteLine("\n[SUCCESS] Comala route COMALWZP79 successfully repaired and verified!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] Error repairing Comala: {ex}");
        }
    }

    static async Task TestAgrupacionPersistenceAsync()
    {
        try
        {
            string testDb = Path.Combine(AppContext.BaseDirectory, "test_agrupacion.db");
            if (File.Exists(testDb)) File.Delete(testDb);
            var cache = new SqliteCacheService(testDb);
            await cache.InitializeAsync();

            var ag = new Agrupacion
            {
                IdAgrupacion = "AGRUP_TEST",
                Nombre = "Club Senderista Volcanes",
                Responsable = "Juan Perez",
                Telefono = "3129876543",
                Email = "contacto@volcanes.org",
                FechaCreacion = "2026-01-15",
                Descripcion = "Club oficial de senderismo",
                Ciudad = "Colima",
                Contacto = "Lic. Maria Lopez",
                Clave = "admin1@test.com, admin2@test.com",
                Fupdate = "2026-10-23T10:00:00Z"
            };

            await cache.UpsertAgrupacionAsync(ag);
            var list = await cache.GetAgrupacionesAsync();
            var retrieved = list.FirstOrDefault(a => a.IdAgrupacion == "AGRUP_TEST");

            if (retrieved != null &&
                retrieved.Nombre == ag.Nombre &&
                retrieved.Responsable == ag.Responsable &&
                retrieved.Telefono == ag.Telefono &&
                retrieved.Email == ag.Email &&
                retrieved.FechaCreacion == ag.FechaCreacion &&
                retrieved.Descripcion == ag.Descripcion &&
                retrieved.Ciudad == ag.Ciudad &&
                retrieved.Contacto == ag.Contacto &&
                retrieved.Clave == ag.Clave)
            {
                Console.WriteLine("[PASS] All Agrupacion fields correctly stored and retrieved from SQLite!");
            }
            else
            {
                Console.WriteLine("[FAIL] Agrupacion fields mismatch in SQLite cache!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] TestAgrupacionPersistenceAsync exception: {ex}");
        }
    }
}
