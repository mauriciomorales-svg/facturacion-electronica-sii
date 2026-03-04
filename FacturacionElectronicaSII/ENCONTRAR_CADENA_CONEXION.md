# Cómo Encontrar la Cadena de Conexión Correcta

## El Problema

El servicio no puede conectarse a SQL Server. Necesitas encontrar la cadena de conexión correcta.

## Método 1: Desde SQL Server Management Studio (SSMS)

1. Abre **SQL Server Management Studio**
2. Conéctate a tu servidor `dbisabel2`
3. Haz clic derecho en el servidor → **Properties**
4. En la pestaña **Connection**, verás el nombre del servidor
5. Copia ese nombre exacto

## Método 2: Desde Visual Studio

1. Abre **Server Explorer** (View → Server Explorer)
2. Haz clic derecho en **Data Connections** → **Add Connection**
3. Selecciona **Microsoft SQL Server**
4. En **Server name**, verás las instancias disponibles
5. Selecciona la que contiene `dbisabel2`
6. Haz clic en **Advanced** para ver la cadena de conexión completa

## Método 3: Probar Variaciones Comunes

Prueba estas cadenas de conexión en `appsettings.json`:

### Opción 1: Instancia por defecto
```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Opción 2: SQL Server Express
```json
"DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Opción 3: Nombre de PC
```json
"DefaultConnection": "Server=TU-PC\\SQLEXPRESS;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```
(Reemplaza `TU-PC` con el nombre de tu computadora)

### Opción 4: Con puerto específico
```json
"DefaultConnection": "Server=localhost,1433;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Opción 5: Con autenticación SQL
```json
"DefaultConnection": "Server=localhost;Database=dbisabel2;User Id=usuario;Password=contraseña;TrustServerCertificate=True;"
```

## Método 4: Desde otro programa que funcione

Si tienes otro programa que se conecta a `dbisabel2`, copia su cadena de conexión.

## Después de Encontrar la Cadena Correcta

1. Actualiza `appsettings.json` con la cadena correcta
2. Reinicia el servicio
3. Prueba nuevamente el endpoint

## Verificar que SQL Server está Corriendo

1. Abre **Services** (servicios.msc)
2. Busca **SQL Server (MSSQLSERVER)** o **SQL Server (SQLEXPRESS)**
3. Verifica que esté **Running**
