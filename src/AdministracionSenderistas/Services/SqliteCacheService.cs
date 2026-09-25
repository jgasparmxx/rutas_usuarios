using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AdministracionSenderistas.Models;
using Microsoft.Data.Sqlite;

namespace AdministracionSenderistas.Services;

public class SqliteCacheService : ISqliteCacheService
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public SqliteCacheService(string? customDbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            DatabasePath = customDbPath;
        }
        else
        {
            string baseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            try
            {
                if (!Directory.Exists(baseFolder))
                    Directory.CreateDirectory(baseFolder);
            }
            catch
            {
                baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            }

            DatabasePath = Path.Combine(baseFolder, "senderistas_cache.db");
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = @"
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS cache_senderistas (
                email TEXT PRIMARY KEY,
                nombre TEXT NOT NULL,
                telefono TEXT,
                rol TEXT NOT NULL,
                id_agrupacion TEXT,
                sexo TEXT,
                fecha_nacimiento TEXT,
                a_quienes_avisar TEXT,
                a_quien_avisar_telefono1 TEXT,
                tiene_enfermedad_cronica TEXT,
                bloqueado INTEGER NOT NULL DEFAULT 0,
                nivel_senderista TEXT,
                minimo_longitud REAL DEFAULT 0,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS cache_rutas (
                id TEXT PRIMARY KEY,
                nombre TEXT NOT NULL,
                fecha TEXT,
                distancia_gps REAL,
                email_creador TEXT,
                telefono_creador TEXT,
                publico INTEGER NOT NULL DEFAULT 1,
                agrupacion TEXT,
                dificultad TEXT,
                duracion_estimada TEXT,
                descripcion TEXT,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS cache_agrupaciones (
                id_agrupacion TEXT PRIMARY KEY,
                nombre TEXT NOT NULL,
                descripcion TEXT,
                ciudad TEXT,
                contacto TEXT,
                clave TEXT,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS cache_invitaciones (
                id_invitacion TEXT PRIMARY KEY,
                titulo TEXT NOT NULL,
                id_ruta TEXT,
                fecha_salida TEXT,
                punto_encuentro TEXT,
                id_agrupacion TEXT,
                email_organizador TEXT,
                cupo_maximo INTEGER,
                estado TEXT,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS cache_participantes (
                id_invitacion TEXT NOT NULL,
                email_senderista TEXT NOT NULL,
                estado TEXT,
                fecha_registro TEXT,
                updated_at TEXT,
                PRIMARY KEY (id_invitacion, email_senderista)
            );

            CREATE TABLE IF NOT EXISTS cache_frases_dificultad (
                id TEXT PRIMARY KEY,
                dificultad TEXT NOT NULL,
                frase TEXT NOT NULL,
                autor TEXT,
                activa INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS cache_requisitos_senderismo (
                id TEXT PRIMARY KEY,
                categoria TEXT NOT NULL,
                requisito TEXT NOT NULL,
                descripcion TEXT,
                es_obligatorio INTEGER NOT NULL DEFAULT 1,
                activo INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS cache_sync_meta (
                table_name TEXT PRIMARY KEY,
                last_sync_utc TEXT
            );
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        // Migración segura para bases de datos existentes
        try
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE cache_senderistas ADD COLUMN minimo_longitud REAL DEFAULT 0;";
            await alterCmd.ExecuteNonQueryAsync();
        }
        catch { }

        try
        {
            using var alterCmd2 = conn.CreateCommand();
            alterCmd2.CommandText = "ALTER TABLE cache_agrupaciones ADD COLUMN clave TEXT;";
            await alterCmd2.ExecuteNonQueryAsync();
        }
        catch { }
    }

    public async Task SetLastSyncTimeAsync(string tableName)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_sync_meta (table_name, last_sync_utc)
            VALUES (@table, @time)
            ON CONFLICT(table_name) DO UPDATE SET last_sync_utc = @time;
        ";
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@time", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(string tableName)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_sync_utc FROM cache_sync_meta WHERE table_name = @table;";
        cmd.Parameters.AddWithValue("@table", tableName);

        var result = await cmd.ExecuteScalarAsync();
        if (result is string s && DateTime.TryParse(s, out var dt))
            return dt;

        return null;
    }

    // --- Senderistas ---
    public async Task<List<Senderista>> GetSenderistasAsync()
    {
        var list = new List<Senderista>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT email, nombre, telefono, rol, id_agrupacion, sexo,
                   fecha_nacimiento, a_quienes_avisar, a_quien_avisar_telefono1,
                   tiene_enfermedad_cronica, bloqueado, nivel_senderista, minimo_longitud
            FROM cache_senderistas
            ORDER BY nombre ASC;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Senderista
            {
                Email = reader.GetString(0),
                Nombre = reader.GetString(1),
                Telefono = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Rol = reader.IsDBNull(3) ? "S" : reader.GetString(3),
                IdAgrupacion = reader.IsDBNull(4) ? null : reader.GetString(4),
                Sexo = reader.IsDBNull(5) ? null : reader.GetString(5),
                FechaNacimiento = reader.IsDBNull(6) ? null : reader.GetString(6),
                AQuienesAvisar = reader.IsDBNull(7) ? null : reader.GetString(7),
                AQuienAvisarTelefono1 = reader.IsDBNull(8) ? null : reader.GetString(8),
                TieneEnfermedadCronica = reader.IsDBNull(9) ? null : reader.GetString(9),
                Bloqueado = reader.GetInt32(10) != 0,
                NivelSenderista = reader.IsDBNull(11) ? null : reader.GetString(11),
                MinimoLongitud = reader.IsDBNull(12) ? 0 : reader.GetDouble(12)
            });
        }
        return list;
    }

    public async Task UpsertSenderistaAsync(Senderista item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_senderistas
                (email, nombre, telefono, rol, id_agrupacion, sexo, fecha_nacimiento,
                 a_quienes_avisar, a_quien_avisar_telefono1, tiene_enfermedad_cronica, bloqueado, nivel_senderista, minimo_longitud, updated_at)
            VALUES
                (@email, @nombre, @telefono, @rol, @id_agrupacion, @sexo, @fecha_nacimiento,
                 @a_quienes_avisar, @a_quien_avisar_telefono1, @tiene_enfermedad_cronica, @bloqueado, @nivel_senderista, @minimo_longitud, @updated_at)
            ON CONFLICT(email) DO UPDATE SET
                nombre = excluded.nombre,
                telefono = excluded.telefono,
                rol = excluded.rol,
                id_agrupacion = excluded.id_agrupacion,
                sexo = excluded.sexo,
                fecha_nacimiento = excluded.fecha_nacimiento,
                a_quienes_avisar = excluded.a_quienes_avisar,
                a_quien_avisar_telefono1 = excluded.a_quien_avisar_telefono1,
                tiene_enfermedad_cronica = excluded.tiene_enfermedad_cronica,
                bloqueado = excluded.bloqueado,
                nivel_senderista = excluded.nivel_senderista,
                minimo_longitud = excluded.minimo_longitud,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@email", item.Email);
        cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@telefono", (object?)item.Telefono ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rol", item.Rol ?? "S");
        cmd.Parameters.AddWithValue("@id_agrupacion", (object?)item.IdAgrupacion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sexo", (object?)item.Sexo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fecha_nacimiento", (object?)item.FechaNacimiento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@a_quienes_avisar", (object?)item.AQuienesAvisar ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@a_quien_avisar_telefono1", (object?)item.AQuienAvisarTelefono1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tiene_enfermedad_cronica", (object?)item.TieneEnfermedadCronica ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@bloqueado", item.Bloqueado ? 1 : 0);
        cmd.Parameters.AddWithValue("@nivel_senderista", (object?)item.NivelSenderista ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@minimo_longitud", item.MinimoLongitud ?? 0);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSenderistaAsync(string email)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_senderistas WHERE email = @email;";
        cmd.Parameters.AddWithValue("@email", email);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceSenderistasAsync(IEnumerable<Senderista> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_senderistas;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_senderistas
                (email, nombre, telefono, rol, id_agrupacion, sexo, fecha_nacimiento,
                 a_quienes_avisar, a_quien_avisar_telefono1, tiene_enfermedad_cronica, bloqueado, nivel_senderista, minimo_longitud, updated_at)
            VALUES
                (@email, @nombre, @telefono, @rol, @id_agrupacion, @sexo, @fecha_nacimiento,
                 @a_quienes_avisar, @a_quien_avisar_telefono1, @tiene_enfermedad_cronica, @bloqueado, @nivel_senderista, @minimo_longitud, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@email", item.Email);
            cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? string.Empty);
            cmd.Parameters.AddWithValue("@telefono", (object?)item.Telefono ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rol", item.Rol ?? "S");
            cmd.Parameters.AddWithValue("@id_agrupacion", (object?)item.IdAgrupacion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sexo", (object?)item.Sexo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha_nacimiento", (object?)item.FechaNacimiento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a_quienes_avisar", (object?)item.AQuienesAvisar ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a_quien_avisar_telefono1", (object?)item.AQuienAvisarTelefono1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tiene_enfermedad_cronica", (object?)item.TieneEnfermedadCronica ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bloqueado", item.Bloqueado ? 1 : 0);
            cmd.Parameters.AddWithValue("@nivel_senderista", (object?)item.NivelSenderista ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@minimo_longitud", item.MinimoLongitud ?? 0);
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("senderistas");
    }

    // --- Rutas ---
    public async Task<List<Ruta>> GetRutasAsync()
    {
        var list = new List<Ruta>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, nombre, fecha, distancia_gps, email_creador, telefono_creador,
                   publico, agrupacion, dificultad, duracion_estimada, descripcion
            FROM cache_rutas
            ORDER BY nombre ASC;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Ruta
            {
                Id = reader.GetString(0),
                Nombre = reader.GetString(1),
                Fecha = reader.IsDBNull(2) ? null : reader.GetString(2),
                DistanciaGps = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                EmailCreador = reader.IsDBNull(4) ? null : reader.GetString(4),
                TelefonoCreador = reader.IsDBNull(5) ? null : reader.GetString(5),
                Publico = reader.GetInt32(6) != 0,
                Agrupacion = reader.IsDBNull(7) ? null : reader.GetString(7),
                Dificultad = reader.IsDBNull(8) ? null : reader.GetString(8),
                DuracionEstimada = reader.IsDBNull(9) ? null : reader.GetString(9),
                Descripcion = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }
        return list;
    }

    public async Task UpsertRutaAsync(Ruta item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_rutas
                (id, nombre, fecha, distancia_gps, email_creador, telefono_creador,
                 publico, agrupacion, dificultad, duracion_estimada, descripcion, updated_at)
            VALUES
                (@id, @nombre, @fecha, @distancia_gps, @email_creador, @telefono_creador,
                 @publico, @agrupacion, @dificultad, @duracion_estimada, @descripcion, @updated_at)
            ON CONFLICT(id) DO UPDATE SET
                nombre = excluded.nombre,
                fecha = excluded.fecha,
                distancia_gps = excluded.distancia_gps,
                email_creador = excluded.email_creador,
                telefono_creador = excluded.telefono_creador,
                publico = excluded.publico,
                agrupacion = excluded.agrupacion,
                dificultad = excluded.dificultad,
                duracion_estimada = excluded.duracion_estimada,
                descripcion = excluded.descripcion,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@fecha", (object?)item.Fecha ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@distancia_gps", (object?)item.DistanciaGps ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email_creador", (object?)item.EmailCreador ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@telefono_creador", (object?)item.TelefonoCreador ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@publico", item.Publico ? 1 : 0);
        cmd.Parameters.AddWithValue("@agrupacion", (object?)item.Agrupacion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dificultad", (object?)item.Dificultad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@duracion_estimada", (object?)item.DuracionEstimada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@descripcion", (object?)item.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRutaAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_rutas WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceRutasAsync(IEnumerable<Ruta> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_rutas;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_rutas
                (id, nombre, fecha, distancia_gps, email_creador, telefono_creador,
                 publico, agrupacion, dificultad, duracion_estimada, descripcion, updated_at)
            VALUES
                (@id, @nombre, @fecha, @distancia_gps, @email_creador, @telefono_creador,
                 @publico, @agrupacion, @dificultad, @duracion_estimada, @descripcion, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? string.Empty);
            cmd.Parameters.AddWithValue("@fecha", (object?)item.Fecha ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@distancia_gps", (object?)item.DistanciaGps ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email_creador", (object?)item.EmailCreador ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@telefono_creador", (object?)item.TelefonoCreador ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@publico", item.Publico ? 1 : 0);
            cmd.Parameters.AddWithValue("@agrupacion", (object?)item.Agrupacion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dificultad", (object?)item.Dificultad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@duracion_estimada", (object?)item.DuracionEstimada ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@descripcion", (object?)item.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("rutas");
    }

    // --- Agrupaciones ---
    public async Task<List<Agrupacion>> GetAgrupacionesAsync()
    {
        var list = new List<Agrupacion>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id_agrupacion, nombre, descripcion, ciudad, contacto, clave
            FROM cache_agrupaciones
            ORDER BY nombre ASC;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Agrupacion
            {
                IdAgrupacion = reader.GetString(0),
                Nombre = reader.GetString(1),
                Descripcion = reader.IsDBNull(2) ? null : reader.GetString(2),
                Ciudad = reader.IsDBNull(3) ? null : reader.GetString(3),
                Contacto = reader.IsDBNull(4) ? null : reader.GetString(4),
                Clave = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return list;
    }

    public async Task UpsertAgrupacionAsync(Agrupacion item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_agrupaciones
                (id_agrupacion, nombre, descripcion, ciudad, contacto, clave, updated_at)
            VALUES
                (@id_agrupacion, @nombre, @descripcion, @ciudad, @contacto, @clave, @updated_at)
            ON CONFLICT(id_agrupacion) DO UPDATE SET
                nombre = excluded.nombre,
                descripcion = excluded.descripcion,
                ciudad = excluded.ciudad,
                contacto = excluded.contacto,
                clave = excluded.clave,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@id_agrupacion", item.IdAgrupacion);
        cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@descripcion", (object?)item.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ciudad", (object?)item.Ciudad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@contacto", (object?)item.Contacto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@clave", (object?)item.Clave ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAgrupacionAsync(string idAgrupacion)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_agrupaciones WHERE id_agrupacion = @id;";
        cmd.Parameters.AddWithValue("@id", idAgrupacion);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceAgrupacionesAsync(IEnumerable<Agrupacion> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_agrupaciones;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_agrupaciones
                (id_agrupacion, nombre, descripcion, ciudad, contacto, clave, updated_at)
            VALUES
                (@id_agrupacion, @nombre, @descripcion, @ciudad, @contacto, @clave, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@id_agrupacion", item.IdAgrupacion);
            cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? string.Empty);
            cmd.Parameters.AddWithValue("@descripcion", (object?)item.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ciudad", (object?)item.Ciudad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@contacto", (object?)item.Contacto ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@clave", (object?)item.Clave ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("agrupaciones");
    }

    // --- Invitaciones ---
    public async Task<List<InvitacionRuta>> GetInvitacionesAsync()
    {
        var list = new List<InvitacionRuta>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id_invitacion, titulo, id_ruta, fecha_salida, punto_encuentro,
                   id_agrupacion, email_organizador, cupo_maximo, estado
            FROM cache_invitaciones
            ORDER BY fecha_salida ASC;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new InvitacionRuta
            {
                IdInvitacion = reader.GetString(0),
                Titulo = reader.GetString(1),
                IdRuta = reader.IsDBNull(2) ? null : reader.GetString(2),
                FechaSalida = reader.IsDBNull(3) ? null : reader.GetString(3),
                PuntoEncuentro = reader.IsDBNull(4) ? null : reader.GetString(4),
                IdAgrupacion = reader.IsDBNull(5) ? null : reader.GetString(5),
                EmailOrganizador = reader.IsDBNull(6) ? null : reader.GetString(6),
                CupoMaximo = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                Estado = reader.IsDBNull(8) ? "Activa" : reader.GetString(8)
            });
        }
        return list;
    }

    public async Task UpsertInvitacionAsync(InvitacionRuta item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_invitaciones
                (id_invitacion, titulo, id_ruta, fecha_salida, punto_encuentro,
                 id_agrupacion, email_organizador, cupo_maximo, estado, updated_at)
            VALUES
                (@id_invitacion, @titulo, @id_ruta, @fecha_salida, @punto_encuentro,
                 @id_agrupacion, @email_organizador, @cupo_maximo, @estado, @updated_at)
            ON CONFLICT(id_invitacion) DO UPDATE SET
                titulo = excluded.titulo,
                id_ruta = excluded.id_ruta,
                fecha_salida = excluded.fecha_salida,
                punto_encuentro = excluded.punto_encuentro,
                id_agrupacion = excluded.id_agrupacion,
                email_organizador = excluded.email_organizador,
                cupo_maximo = excluded.cupo_maximo,
                estado = excluded.estado,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@id_invitacion", item.IdInvitacion);
        cmd.Parameters.AddWithValue("@titulo", item.Titulo ?? string.Empty);
        cmd.Parameters.AddWithValue("@id_ruta", (object?)item.IdRuta ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fecha_salida", (object?)item.FechaSalida ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@punto_encuentro", (object?)item.PuntoEncuentro ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id_agrupacion", (object?)item.IdAgrupacion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email_organizador", (object?)item.EmailOrganizador ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cupo_maximo", (object?)item.CupoMaximo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@estado", (object?)item.Estado ?? "Activa");
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteInvitacionAsync(string idInvitacion)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_invitaciones WHERE id_invitacion = @id;";
        cmd.Parameters.AddWithValue("@id", idInvitacion);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceInvitacionesAsync(IEnumerable<InvitacionRuta> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_invitaciones;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_invitaciones
                (id_invitacion, titulo, id_ruta, fecha_salida, punto_encuentro,
                 id_agrupacion, email_organizador, cupo_maximo, estado, updated_at)
            VALUES
                (@id_invitacion, @titulo, @id_ruta, @fecha_salida, @punto_encuentro,
                 @id_agrupacion, @email_organizador, @cupo_maximo, @estado, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@id_invitacion", item.IdInvitacion);
            cmd.Parameters.AddWithValue("@titulo", item.Titulo ?? string.Empty);
            cmd.Parameters.AddWithValue("@id_ruta", (object?)item.IdRuta ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha_salida", (object?)item.FechaSalida ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@punto_encuentro", (object?)item.PuntoEncuentro ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id_agrupacion", (object?)item.IdAgrupacion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email_organizador", (object?)item.EmailOrganizador ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cupo_maximo", (object?)item.CupoMaximo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@estado", (object?)item.Estado ?? "Activa");
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("invitaciones");
    }

    // --- Participantes ---
    public async Task<List<ParticipanteInvitacion>> GetParticipantesAsync()
    {
        var list = new List<ParticipanteInvitacion>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id_invitacion, email_senderista, estado, fecha_registro
            FROM cache_participantes
            ORDER BY id_invitacion, email_senderista;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ParticipanteInvitacion
            {
                IdInvitacion = reader.GetString(0),
                EmailSenderista = reader.GetString(1),
                Estado = reader.IsDBNull(2) ? "Inscrito" : reader.GetString(2),
                FechaRegistro = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return list;
    }

    public async Task UpsertParticipanteAsync(ParticipanteInvitacion item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_participantes
                (id_invitacion, email_senderista, estado, fecha_registro, updated_at)
            VALUES
                (@id_invitacion, @email_senderista, @estado, @fecha_registro, @updated_at)
            ON CONFLICT(id_invitacion, email_senderista) DO UPDATE SET
                estado = excluded.estado,
                fecha_registro = excluded.fecha_registro,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@id_invitacion", item.IdInvitacion);
        cmd.Parameters.AddWithValue("@email_senderista", item.EmailSenderista);
        cmd.Parameters.AddWithValue("@estado", (object?)item.Estado ?? "Inscrito");
        cmd.Parameters.AddWithValue("@fecha_registro", (object?)item.FechaRegistro ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteParticipanteAsync(string idInvitacion, string emailSenderista)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_participantes WHERE id_invitacion = @id AND email_senderista = @email;";
        cmd.Parameters.AddWithValue("@id", idInvitacion);
        cmd.Parameters.AddWithValue("@email", emailSenderista);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceParticipantesAsync(IEnumerable<ParticipanteInvitacion> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_participantes;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_participantes
                (id_invitacion, email_senderista, estado, fecha_registro, updated_at)
            VALUES
                (@id_invitacion, @email_senderista, @estado, @fecha_registro, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@id_invitacion", item.IdInvitacion);
            cmd.Parameters.AddWithValue("@email_senderista", item.EmailSenderista);
            cmd.Parameters.AddWithValue("@estado", (object?)item.Estado ?? "Inscrito");
            cmd.Parameters.AddWithValue("@fecha_registro", (object?)item.FechaRegistro ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("participantes");
    }

    // --- Frases Dificultad ---
    public async Task<List<FraseDificultad>> GetFrasesDificultadAsync()
    {
        var list = new List<FraseDificultad>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, dificultad, frase, autor, activa
            FROM cache_frases_dificultad
            ORDER BY dificultad, id;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new FraseDificultad
            {
                Id = reader.GetString(0),
                Dificultad = reader.GetString(1),
                Frase = reader.GetString(2),
                Autor = reader.IsDBNull(3) ? null : reader.GetString(3),
                Activa = reader.GetInt32(4) != 0
            });
        }
        return list;
    }

    public async Task UpsertFraseDificultadAsync(FraseDificultad item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_frases_dificultad
                (id, dificultad, frase, autor, activa, updated_at)
            VALUES
                (@id, @dificultad, @frase, @autor, @activa, @updated_at)
            ON CONFLICT(id) DO UPDATE SET
                dificultad = excluded.dificultad,
                frase = excluded.frase,
                autor = excluded.autor,
                activa = excluded.activa,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@dificultad", item.Dificultad ?? "Moderada");
        cmd.Parameters.AddWithValue("@frase", item.Frase ?? string.Empty);
        cmd.Parameters.AddWithValue("@autor", (object?)item.Autor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@activa", item.Activa ? 1 : 0);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteFraseDificultadAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_frases_dificultad WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceFrasesDificultadAsync(IEnumerable<FraseDificultad> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_frases_dificultad;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_frases_dificultad
                (id, dificultad, frase, autor, activa, updated_at)
            VALUES
                (@id, @dificultad, @frase, @autor, @activa, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@dificultad", item.Dificultad ?? "Moderada");
            cmd.Parameters.AddWithValue("@frase", item.Frase ?? string.Empty);
            cmd.Parameters.AddWithValue("@autor", (object?)item.Autor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@activa", item.Activa ? 1 : 0);
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("frases_dificultad");
    }

    // --- Requisitos Senderismo ---
    public async Task<List<RequisitoSenderismo>> GetRequisitosSenderismoAsync()
    {
        var list = new List<RequisitoSenderismo>();
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, categoria, requisito, descripcion, es_obligatorio, activo
            FROM cache_requisitos_senderismo
            ORDER BY categoria, id;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RequisitoSenderismo
            {
                Id = reader.GetString(0),
                Categoria = reader.GetString(1),
                Requisito = reader.GetString(2),
                Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                EsObligatorio = reader.GetInt32(4) != 0,
                Activo = reader.GetInt32(5) != 0
            });
        }
        return list;
    }

    public async Task UpsertRequisitoSenderismoAsync(RequisitoSenderismo item)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO cache_requisitos_senderismo
                (id, categoria, requisito, descripcion, es_obligatorio, activo, updated_at)
            VALUES
                (@id, @categoria, @requisito, @descripcion, @es_obligatorio, @activo, @updated_at)
            ON CONFLICT(id) DO UPDATE SET
                categoria = excluded.categoria,
                requisito = excluded.requisito,
                descripcion = excluded.descripcion,
                es_obligatorio = excluded.es_obligatorio,
                activo = excluded.activo,
                updated_at = excluded.updated_at;
        ";

        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@categoria", item.Categoria ?? "General");
        cmd.Parameters.AddWithValue("@requisito", item.Requisito ?? string.Empty);
        cmd.Parameters.AddWithValue("@descripcion", (object?)item.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@es_obligatorio", item.EsObligatorio ? 1 : 0);
        cmd.Parameters.AddWithValue("@activo", item.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRequisitoSenderismoAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM cache_requisitos_senderismo WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReplaceRequisitosSenderismoAsync(IEnumerable<RequisitoSenderismo> items)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        using var transaction = conn.BeginTransaction();

        using (var deleteCmd = conn.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM cache_requisitos_senderismo;";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        string insertSql = @"
            INSERT INTO cache_requisitos_senderismo
                (id, categoria, requisito, descripcion, es_obligatorio, activo, updated_at)
            VALUES
                (@id, @categoria, @requisito, @descripcion, @es_obligatorio, @activo, @updated_at);
        ";

        string now = DateTime.UtcNow.ToString("o");
        foreach (var item in items)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@categoria", item.Categoria ?? "General");
            cmd.Parameters.AddWithValue("@requisito", item.Requisito ?? string.Empty);
            cmd.Parameters.AddWithValue("@descripcion", (object?)item.Descripcion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@es_obligatorio", item.EsObligatorio ? 1 : 0);
            cmd.Parameters.AddWithValue("@activo", item.Activo ? 1 : 0);
            cmd.Parameters.AddWithValue("@updated_at", now);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        await SetLastSyncTimeAsync("requisitos_senderismo");
    }
}
