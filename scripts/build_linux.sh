#!/bin/bash
echo "========================================================"
echo "  Empaquetando AdministracionSenderistas para Linux"
echo "========================================================"

dotnet publish src/AdministracionSenderistas/AdministracionSenderistas.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/linux-x64-standalone

if [ $? -eq 0 ]; then
    chmod +x publish/linux-x64-standalone/AdministracionSenderistas
    echo ""
    echo "========================================================"
    echo "  Compilación para Linux exitosa."
    echo "  Binario generado en: publish/linux-x64-standalone/AdministracionSenderistas"
    echo "========================================================"
else
    echo ""
    echo "[ERROR] Falló la compilación para Linux."
fi
