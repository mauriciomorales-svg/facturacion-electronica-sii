# Configuración de Base de Datos - CAFs

## Estructura Esperada

El servicio `CAFService` espera las siguientes tablas en la base de datos `dbisabel2`:

### Tabla de CAFs
Nombre sugerido: `CAFs` (ajustar en `CAFService.cs` línea ~66)

Columnas esperadas:
- `TipoDTE` (int) - Tipo de documento (33, 39, 61, 56)
- `CAF_XML` o `XMLData` (nvarchar/text) - XML completo del CAF
- `FechaAutorizacion` (datetime) - Fecha de autorización del CAF
- `FolioInicial` (int) - Folio inicial del rango (opcional, se puede extraer del XML)
- `FolioFinal` (int) - Folio final del rango (opcional, se puede extraer del XML)
- `RutEmisor` (varchar) - RUT del emisor (opcional, se puede extraer del XML)
- `RazonSocial` (varchar) - Razón social (opcional, se puede extraer del XML)
- `Activo` (bit) - Indica si el CAF está activo

### Tabla de Folios Usados
Nombre sugerido: `FoliosUsados` (ajustar en `CAFService.cs` líneas ~120 y ~150)

Columnas esperadas:
- `TipoDTE` (int) - Tipo de documento
- `Folio` (int) - Número de folio usado
- `FechaUso` (datetime) - Fecha en que se usó el folio

## Ajustar Consultas SQL

Si tus tablas tienen nombres diferentes, edita el archivo `Services/CAFService.cs`:

1. **Línea ~66**: Cambiar nombre de tabla y columnas en la consulta SELECT
2. **Línea ~120**: Cambiar nombre de tabla en INSERT de folios usados
3. **Línea ~150**: Cambiar nombre de tabla en SELECT de folios usados

## Ejemplo de Consulta Ajustada

Si tu tabla se llama `DTE_CAFs` y la columna del XML es `XML_CAF`:

```csharp
var query = @"
    SELECT TOP 1 
        XML_CAF as XMLData,
        FechaAutorizacion,
        FolioInicial,
        FolioFinal,
        RutEmisor,
        RazonSocial
    FROM DTE_CAFs 
    WHERE TipoDTE = @TipoDTE 
        AND Activo = 1
    ORDER BY FechaAutorizacion DESC";
```

## Cadena de Conexión

La cadena de conexión está configurada en `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Ajusta según tu servidor SQL Server:
- Si es instancia nombrada: `Server=localhost\\SQLEXPRESS;...`
- Si requiere autenticación SQL: `Server=localhost;Database=dbisabel2;User Id=usuario;Password=contraseña;`
