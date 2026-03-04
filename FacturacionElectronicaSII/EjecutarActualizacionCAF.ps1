# Script PowerShell para actualizar el CAF en MySQL
# Requiere: MySqlConnector instalado en el proyecto

Write-Host "=== ACTUALIZANDO CAF EN MYSQL ===" -ForegroundColor Cyan
Write-Host ""

# Cargar el proyecto .NET para usar MySqlConnector
$projectPath = Join-Path $PSScriptRoot "FacturacionElectronicaSII"
$dllPath = Join-Path $projectPath "bin\Debug\net8.0\MySqlConnector.dll"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: No se encontró MySqlConnector.dll" -ForegroundColor Red
    Write-Host "Compila el proyecto primero con: dotnet build" -ForegroundColor Yellow
    exit 1
}

# XML limpio del CAF
$xmlLimpio = @'
<?xml version="1.0"?>
<AUTORIZACION>
<CAF version="1.0">
<DA>
<RE>8451335-0</RE>
<RS>MARTA INALBIA URRA ESCOBAR</RS>
<TD>33</TD>
<RNG><D>1</D><H>60</H></RNG>
<FA>2018-09-22</FA>
<RSAPK><M>k+e6qyIYl4EF9fH1hEFk9H6F5LZmBplwq+sKpP0osX/lNoqEzPoUicyTWXJQpZIlDjnGXGbY7u7X7jfgG71TwQ==</M><E>Aw==</E></RSAPK>
<IDK>100</IDK>
</DA>
<FRMA algoritmo="SHA1withRSA">oAK8TyCOJgpo6G9hc4jbQ+RXMLiB3csxjCjxU8wl1QRi/ZKqYxAeWEqtXUN3fYGxkyabjB6VM3BL3Jb5wAPvaA==</FRMA>
</CAF>
<RSASK>-----BEGIN RSA PRIVATE KEY-----
MIIBOgIBAAJBAJPnuqsiGJeBBfXx9YRBZPR+heS2ZgaZcKvrCqT9KLF/5TaKhMz6
FInMk1lyUKWSJQ45xlxm2O7u1+434Bu9U8ECAQMCQGKafHIWuw+rWU6hTlgrmKL/
A+3O7q8Q9cfyBxioxcuplVql6QiYnMaa1NsVg5GK6oSv7BN7zDHiopWnKTraRuMC
IQDD+SNu4QOBS3SwLxypz5zoL0eJJNhcLKRm6XGecqfRGwIhAME1bjhfEagUb6Ph
tWF7pN0X6lsaVMp3dn0kS4PQzhhTAiEAgqYXn0CtANz4dXS9xopomsovsMM66B3C
70ZLvvcai2cCIQCAzkl66gvFYvUX685A/RiTZUbnZuMxpPmowt0CizQQNwIhAKK4
DwPCaGW3+IXLms4z5zA4DJbX5TYlu9d3ZsBOBrxO
-----END RSA PRIVATE KEY-----
</RSASK>

<RSAPUBK>-----BEGIN PUBLIC KEY-----
MFowDQYJKoZIhvcNAQEBBQADSQAwRgJBAJPnuqsiGJeBBfXx9YRBZPR+heS2ZgaZ
cKvrCqT9KLF/5TaKhMz6FInMk1lyUKWSJQ45xlxm2O7u1+434Bu9U8ECAQM=
-----END PUBLIC KEY-----
</RSAPUBK>
</AUTORIZACION>
'@

# Escapar comillas simples para SQL
$xmlParaSQL = $xmlLimpio.Replace("'", "''")

# Crear script SQL temporal
$sqlScript = @"
USE dbisabel2;

-- Verificar antes
SELECT 
    ID,
    TD,
    LENGTH(CAFContenido) as LongitudAntes,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN 'CONTIENE <td>'
        WHEN CAFContenido LIKE '%<html%' THEN 'CONTIENE <html>'
        ELSE 'NO CONTIENE HTML'
    END as EstadoAntes
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- Actualizar
UPDATE CAF 
SET CAFContenido = '$xmlParaSQL'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- Verificar después
SELECT 
    ID,
    TD,
    LENGTH(CAFContenido) as LongitudDespues,
    CASE 
        WHEN CAFContenido LIKE '%<AUTORIZACION%' THEN 'OK - TIENE AUTORIZACION'
        ELSE 'ERROR - NO TIENE AUTORIZACION'
    END as TieneAutorizacion,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'ERROR - CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN 'ERROR - CONTIENE <td>'
        WHEN CAFContenido LIKE '%<html%' THEN 'ERROR - CONTIENE <html>'
        ELSE 'OK - NO CONTIENE HTML'
    END as EstadoDespues
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
"@

$sqlFile = Join-Path $PSScriptRoot "UPDATE_CAF_TEMPORAL.sql"
$sqlScript | Out-File $sqlFile -Encoding UTF8

Write-Host "Script SQL generado en: $sqlFile" -ForegroundColor Yellow
Write-Host ""
Write-Host "OPCIÓN 1: Ejecutar manualmente en MySQL Workbench" -ForegroundColor Cyan
Write-Host "  1. Abre MySQL Workbench" -ForegroundColor White
Write-Host "  2. Conéctate a: 127.0.0.1:3306, usuario: root, BD: dbisabel2" -ForegroundColor White
Write-Host "  3. Abre el archivo: $sqlFile" -ForegroundColor White
Write-Host "  4. Ejecuta el script completo" -ForegroundColor White
Write-Host ""
Write-Host "OPCIÓN 2: Si tienes mysql.exe en el PATH:" -ForegroundColor Cyan
Write-Host "  mysql -h 127.0.0.1 -u root dbisabel2 < $sqlFile" -ForegroundColor White
Write-Host ""
Write-Host "Después de ejecutar, reinicia el servicio y prueba la emisión." -ForegroundColor Green
