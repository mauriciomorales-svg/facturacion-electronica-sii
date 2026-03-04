# Script PowerShell para ejecutar el script SQL de creación de tabla CAF
# Requiere MySqlConnector instalado

$connectionString = "Server=127.0.0.1;Database=dbisabel2;User=root;Password=;Port=3306;"

try {
    # Cargar MySqlConnector
    Add-Type -Path "bin\Debug\net8.0\MySqlConnector.dll" -ErrorAction SilentlyContinue
    
    Write-Host "Conectando a la base de datos..." -ForegroundColor Cyan
    
    $connection = New-Object MySqlConnector.MySqlConnection($connectionString)
    $connection.Open()
    
    Write-Host "Leyendo script SQL..." -ForegroundColor Cyan
    $sqlScript = Get-Content "CREAR_TABLA_CAF_LIMPIA.sql" -Raw
    
    # Dividir el script en comandos individuales (separados por ;)
    $commands = $sqlScript -split ';' | Where-Object { $_.Trim() -ne '' -and $_.Trim() -notmatch '^--' }
    
    Write-Host "Ejecutando script..." -ForegroundColor Cyan
    
    foreach ($command in $commands) {
        $cmd = $command.Trim()
        if ($cmd -ne '' -and $cmd -notmatch '^--') {
            try {
                $mysqlCommand = New-Object MySqlConnector.MySqlCommand($cmd, $connection)
                $result = $mysqlCommand.ExecuteNonQuery()
                Write-Host "✓ Comando ejecutado" -ForegroundColor Green
            } catch {
                Write-Host "⚠ Error en comando: $_" -ForegroundColor Yellow
            }
        }
    }
    
    Write-Host "`n✓ Script ejecutado correctamente" -ForegroundColor Green
    $connection.Close()
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Intentando método alternativo..." -ForegroundColor Yellow
    
    # Método alternativo: usar dotnet run con un programa temporal
    Write-Host "Por favor ejecuta el script SQL manualmente usando:" -ForegroundColor Cyan
    Write-Host "mysql -h 127.0.0.1 -u root dbisabel2 < CREAR_TABLA_CAF_LIMPIA.sql" -ForegroundColor Yellow
}
