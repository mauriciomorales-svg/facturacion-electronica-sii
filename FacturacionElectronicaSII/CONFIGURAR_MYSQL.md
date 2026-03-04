# Configuración de MySQL

## Cambios Realizados

El servicio ahora usa **MySQL** en lugar de SQL Server para conectarse a `dbisabel2`.

## Cadena de Conexión

La cadena de conexión en `appsettings.json` debe ajustarse según tu configuración de MySQL:

```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;User=root;Password=;Port=3306;"
```

### Parámetros Comunes:

- **Server**: `localhost` o la IP del servidor MySQL
- **Database**: `dbisabel2`
- **User**: Usuario de MySQL (ej: `root`)
- **Password**: Contraseña del usuario
- **Port**: Puerto de MySQL (por defecto `3306`)

### Ejemplos:

#### Conexión Local
```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;User=root;Password=tu_password;Port=3306;"
```

#### Conexión Remota
```json
"DefaultConnection": "Server=192.168.1.100;Database=dbisabel2;User=usuario;Password=contraseña;Port=3306;"
```

#### Sin Contraseña (solo desarrollo)
```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;User=root;Password=;Port=3306;"
```

## Cambios en el Código

- ✅ Cambiado de `Microsoft.Data.SqlClient` a `MySqlConnector`
- ✅ Cambiado `SqlConnection` → `MySqlConnection`
- ✅ Cambiado `SqlCommand` → `MySqlCommand`
- ✅ Cambiado sintaxis SQL:
  - `TOP 1` → `LIMIT 1`
  - `LTRIM(RTRIM(...))` → `TRIM(...)`
  - `GETDATE()` → `NOW()`
  - `IF NOT EXISTS ... BEGIN ... END` → `INSERT ... SELECT ... WHERE NOT EXISTS`

## Verificar Conexión

Después de actualizar la cadena de conexión, reinicia el servicio y prueba el endpoint:
```
GET /api/CAF/folios-disponibles/33
```
