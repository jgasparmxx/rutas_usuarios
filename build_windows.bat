@echo off
echo ========================================================
echo   Empaquetando AdministracionSenderistas para Windows
echo ========================================================

dotnet publish src\AdministracionSenderistas\AdministracionSenderistas.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64-standalone

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo   Compilacion exitosa.
    echo   Ejecutable generado en: publish\win-x64-standalone\AdministracionSenderistas.exe
    echo ========================================================
) else (
    echo.
    echo [ERROR] Ocurrio un error al compilar para Windows.
)
pause
