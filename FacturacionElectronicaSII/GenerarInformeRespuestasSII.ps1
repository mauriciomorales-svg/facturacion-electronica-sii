# Script para generar informe de respuestas del SII
$directorioRespuestas = "RespuestasSII"
$archivoInforme = "INFORME_RESPUESTAS_SII.txt"

Write-Host "`n=== GENERANDO INFORME DE RESPUESTAS DEL SII ===" -ForegroundColor Cyan

if (-not (Test-Path $directorioRespuestas)) {
    Write-Host "`nDirectorio $directorioRespuestas no existe." -ForegroundColor Yellow
    Write-Host "Aun no se han recibido respuestas del SII." -ForegroundColor Yellow
    Write-Host "El directorio se creara automaticamente cuando el sistema reciba respuestas." -ForegroundColor Yellow
    exit
}

$archivos = Get-ChildItem $directorioRespuestas -Filter "*.txt" | Sort-Object LastWriteTime -Descending

if ($archivos.Count -eq 0) {
    Write-Host "`nNo hay archivos de respuestas en el directorio." -ForegroundColor Yellow
    exit
}

Write-Host "`nArchivos encontrados: $($archivos.Count)" -ForegroundColor Green

$informe = @()
$informe += "==================================================================="
$informe += "           INFORME DE RESPUESTAS DEL SII"
$informe += "==================================================================="
$informe += "Fecha de generacion: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$informe += "Total de archivos: $($archivos.Count)"
$informe += "Directorio: $((Get-Item $directorioRespuestas).FullName)"
$informe += ""

# Resumen por tipo
$informe += "==================================================================="
$informe += "RESUMEN POR TIPO DE OPERACION"
$informe += "==================================================================="
$porTipo = $archivos | Group-Object { ($_.Name -split '_')[0] }
foreach ($grupo in $porTipo | Sort-Object Name) {
    $informe += "$($grupo.Name.PadRight(20)): $($grupo.Count) archivo(s)"
    $ultimo = ($grupo.Group | Sort-Object LastWriteTime -Descending)[0]
    $informe += "  Ultimo archivo: $($ultimo.Name) - $($ultimo.LastWriteTime)"
}
$informe += ""

# Detalle de cada respuesta
$informe += "==================================================================="
$informe += "DETALLE DE RESPUESTAS"
$informe += "==================================================================="
$informe += ""

$contador = 1
foreach ($archivo in $archivos) {
    $contenido = Get-Content $archivo.FullName -Raw -Encoding UTF8
    $lineas = ($contenido -split "`n").Count
    
    $informe += "-------------------------------------------------------------------"
    $informe += "[$contador] $($archivo.Name)"
    $informe += "-------------------------------------------------------------------"
    $informe += "Fecha/Hora:     $($archivo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
    $informe += "Tamaño:         $([math]::Round($archivo.Length/1KB, 2)) KB"
    $informe += "Lineas:         $lineas"
    $informe += "Tipo:           $(($archivo.Name -split '_')[0])"
    
    # Extraer código HTTP si está en el nombre
    if ($archivo.Name -match 'HTTP_(\d+)') {
        $codigoHttp = $matches[1]
        $informe += "Codigo HTTP:    $codigoHttp"
    }
    
    $informe += ""
    $informe += "CONTENIDO COMPLETO:"
    $informe += "-------------------------------------------------------------------"
    $informe += $contenido
    $informe += ""
    $informe += "==================================================================="
    $informe += ""
    
    $contador++
}

# Análisis de respuestas
$informe += "==================================================================="
$informe += "ANALISIS DE RESPUESTAS"
$informe += "==================================================================="
$informe += ""

# Contar errores
$errores = $archivos | Where-Object { $_.Name -match 'Error|HTTP_5|HTTP_4' }
$exitosos = $archivos | Where-Object { $_.Name -match 'HTTP_200' -or ($_.Name -notmatch 'Error' -and $_.Name -notmatch 'HTTP_') }

$informe += "Respuestas exitosas: $($exitosos.Count)"
$informe += "Respuestas con error: $($errores.Count)"
$informe += ""

# Buscar mensajes de error comunes
$informe += "Mensajes de error encontrados:"
foreach ($archivo in $errores) {
    $contenido = Get-Content $archivo.FullName -Raw -Encoding UTF8
    if ($contenido -match 'NO ESTA AUTOR|NO ESTA AUTORIZADA|ERROR|Error') {
        $mensaje = ($contenido -split "`n" | Where-Object { $_ -match 'NO ESTA AUTOR|NO ESTA AUTORIZADA|ERROR' } | Select-Object -First 1).Trim()
        if ($mensaje) {
            $informe += "  - $($archivo.Name): $mensaje"
        }
    }
}
$informe += ""

# Guardar informe
$informe | Out-File $archivoInforme -Encoding UTF8

Write-Host "`nInforme generado exitosamente!" -ForegroundColor Green
Write-Host "Archivo: $archivoInforme" -ForegroundColor Cyan
Write-Host "Tamaño: $([math]::Round((Get-Item $archivoInforme).Length/1KB, 2)) KB" -ForegroundColor Cyan
Write-Host "Total de lineas: $($informe.Count)" -ForegroundColor Cyan
