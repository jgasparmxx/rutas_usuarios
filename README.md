# 🏔️ Administración Senderistas - Panel de Control BD & API REST

Aplicación de escritorio moderna y multiplataforma desarrollada en **.NET 9** con **Avalonia UI** y el patrón de arquitectura limpia **MVVM**. Permite la administración total de las tablas de la base de datos PostgreSQL (alojada en Neon Cloud) a través del API REST **PostgREST** (alojado en Render Cloud), con una capa de **caché local offline en SQLite** (`Microsoft.Data.Sqlite`).

---

## 🚀 Características Principales

1. **Patrón MVVM Limpio**:
   - Desarrollado con `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`, `ObservableProperty`).
   - Inyección de dependencias (`Microsoft.Extensions.DependencyInjection`).
   - Separación estricta de responsabilidades entre Vistas, ViewModels, Repositorios, Servicios y Modelos.

2. **Caché Local Offline con SQLite (`Microsoft.Data.Sqlite`)**:
   - Almacena localmente una copia completa de las tablas (`cache_senderistas`, `cache_rutas`, `cache_agrupaciones`, `cache_invitaciones`, `cache_participantes`).
   - Soporta funcionamiento sin conexión a internet y sincronización bidireccional automática al restablecerse el enlace con PostgREST.

3. **Autenticación y Seguridad Estricta**:
   - **Senderistas Administradores con Rol `'O'`**:
     - **Usuario**: Correo electrónico registrado en la tabla `senderistas`.
     - **Contraseña**: Número de teléfono registrado del senderista (sin alterar el esquema de BD ni interferir con la app móvil).
     - Validación en la BD/API: únicamente usuarios que posean el rol `'O'` y cuya clave coincida con su teléfono registrado pueden ingresar al panel de administración.
   - **Acceso de Emergencia (Super Usuario Maestro)**:
     - Llave de rescate reservada para casos extremos (`JGRBalam` / `Yomero`). No se expone ni sugiere en la interfaz de Login.
   - **Generación y Firma de Tokens JWT**:
     - Firma con algoritmo `HS256` y clave secreta `mi_clave_secreta_super_segura_rutas_sendero_2026`.
     - Claims validados por PostgREST (`role: "senderista"`, `email`, `rol: "O"`, `telefono`, `id_agrupacion`).

4. **Interfaz de Usuario (Avalonia UI 11.2)**:
   - **Menú a la derecha** con acceso directo y conteo dinámico de registros para cada tabla:
     - 👥 **Senderistas**
     - 🗺 **Rutas**
     - 🏛 **Agrupaciones**
     - ✉ **Invitaciones a Rutas**
     - 🎒 **Participantes**
     - 💬 **Frases Dificultad** (mantenimiento de textos motivacionales y por nivel de reto)
     - 📋 **Requisitos Senderismo** (equipo obligatorio, vestimenta, condición física y recomendaciones)
   - **Área principal a la izquierda dividida en 2 Pestañas**:
     - **Pestaña 1 (Grid)**: `DataGrid` con ordenamiento por columnas, buscador en tiempo real, filtros por rol/categoría, paginación/conteo, y exportación instantánea a CSV.
     - **Pestaña 2 (Formulario)**: Vista de detalle para ver, editar y crear registros tanto en el API REST como en la base de datos local SQLite.

5. **Empaquetado Multiplataforma**:
   - Compatible nativamente con **Windows 10/11** (`win-x64`) y **Linux** (`linux-x64`).

---

## 📂 Estructura de la Solución

```text
c:\GASPAR\baches\rutas_usuarios\
├── AdministracionSenderistas.sln
├── global.json
├── publish/
│   ├── win-x64/
│   └── linux-x64/
├── scripts/
│   ├── build_windows.bat
│   └── build_linux.sh
└── src/
    └── AdministracionSenderistas/
        ├── AdministracionSenderistas.csproj
        ├── App.axaml / App.axaml.cs
        ├── Program.cs
        ├── app.manifest
        ├── Models/
        │   ├── Senderista.cs
        │   ├── Ruta.cs
        │   ├── Agrupacion.cs
        │   ├── InvitacionRuta.cs
        │   ├── ParticipanteInvitacion.cs
        │   └── UserSession.cs
        ├── Services/
        │   ├── IJwtService.cs / JwtService.cs
        │   ├── ISqliteCacheService.cs / SqliteCacheService.cs
        │   ├── IPostgrestApiClient.cs / PostgrestApiClient.cs
        │   ├── IAuthService.cs / AuthService.cs
        │   └── IDataRepository.cs / DataRepository.cs
        ├── ViewModels/
        │   ├── ViewModelBase.cs
        │   ├── MainWindowViewModel.cs
        │   ├── LoginViewModel.cs
        │   └── DashboardViewModel.cs
        └── Views/
            ├── MainWindow.axaml / MainWindow.axaml.cs
            ├── LoginView.axaml / LoginView.axaml.cs
            └── DashboardView.axaml / DashboardView.axaml.cs
```

---

## 🛠️ Requisitos de Compilación y Ejecución

* **SDK de .NET**: .NET 9 (o .NET 8).
* **Sistema Operativo**: Windows 10/11 o distribución Linux (Ubuntu 20.04+, Debian, Fedora, Arch).

---

## 🏃 Cómo Ejecutar en Modo Desarrollo

```powershell
# Desde la raíz del repositorio
dotnet run --project src/AdministracionSenderistas/AdministracionSenderistas.csproj
```

---

## 📦 Cómo Empaquetar para Windows y Linux

### 1. Empaquetado para Windows (Ejecutable Independiente `.exe`):
Ejecuta el script:
```cmd
scripts\build_windows.bat
```
O manualmente:
```powershell
dotnet publish src/AdministracionSenderistas/AdministracionSenderistas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64-standalone
```

### 2. Empaquetado para Linux (Binario ejecutable):
Ejecuta el script:
```bash
bash scripts/build_linux.sh
```
O manualmente:
```bash
dotnet publish src/AdministracionSenderistas/AdministracionSenderistas.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64-standalone
```

---

## 🔑 Credenciales de Acceso
 
| Tipo de Usuario | Usuario (Login) | Contraseña | Rol Requerido | Alcance |
| :--- | :--- | :--- | :--- | :--- |
| **Administrador** | *Correo del Senderista* | *Número de teléfono registrado* | `'O'` (Dueño / Superadmin) | Acceso administrativo completo |
| **Rescate / Emergencia** | `JGRBalam` *(No sugerido en UI)* | `Yomero` | `'O'` | Llave maestra para casos extremos |

*(Cualquier usuario con rol `'A'`, `'G'` o `'S'`, o cuyo teléfono no coincida con el registrado, será rechazado por la política de seguridad implementada en `AuthService.cs`).*
