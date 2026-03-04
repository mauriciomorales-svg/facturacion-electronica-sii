# Configurar Cadena de Conexión a Base de Datos

## Error Actual

El servicio no puede conectarse a SQL Server. El error indica:
> "The server was not found or was not accessible"

## Solución

Edita `appsettings.json` y ajusta la cadena de conexión según tu configuración de SQL Server.

### Opciones Comunes:

#### 1. SQL Server Express (instancia nombrada)
```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

#### 2. SQL Server Default Instance
```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

#### 3. SQL Server con nombre de servidor específico
```json
"DefaultConnection": "Server=NOMBRE_SERVIDOR;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

#### 4. SQL Server con autenticación SQL (usuario/contraseña)
```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;User Id=usuario;Password=contraseña;TrustServerCertificate=True;"
```

#### 5. SQL Server en puerto específico
```json
"DefaultConnection": "Server=localhost,1433;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

## Cómo Encontrar tu Configuración

1. **Abre SQL Server Management Studio (SSMS)**
2. **Conéctate a tu servidor** y mira el nombre del servidor en la barra de conexión
3. **Copia ese nombre** y úsalo en la cadena de conexión

Ejemplo: Si en SSMS ves `MI-PC\SQLEXPRESS`, usa:
```
Server=MI-PC\\SQLEXPRESS;Database=dbisabel2;...
```

## Probar la Conexión

Después de actualizar `appsettings.json`, reinicia el servicio y prueba nuevamente el endpoint `/api/CAF/33`.
