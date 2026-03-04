# Estructura Real de Base de Datos

## Tabla CAF

Según la estructura real de tu base de datos `dbisabel2`:

| Columna | Tipo | Descripción |
|---------|------|-------------|
| ID | int | Identificador único |
| TD | int | Tipo de DTE (33, 39, 61, 56) |
| RangoInicio | int | Folio inicial del rango |
| RangoFin | int | Folio final del rango |
| FechaCarga | datetime | Fecha de carga del CAF |
| Estado | varchar | Estado del CAF ('Activo', etc.) |
| CAFContenido | text/xml | XML completo del CAF |
| FRMA | varchar | Firma del CAF |

## Consulta Actualizada

El `CAFService` ahora usa:

```sql
SELECT TOP 1 
    CAFContenido as XMLData,
    FechaCarga as FechaAutorizacion,
    RangoInicio as FolioInicial,
    RangoFin as FolioFinal
FROM CAF 
WHERE TD = @TipoDTE 
    AND Estado = 'Activo'
ORDER BY FechaCarga DESC
```

## Tabla de Folios Usados

Si no tienes una tabla para rastrear folios usados, puedes:

1. **Crear la tabla**:
```sql
CREATE TABLE FoliosUsados (
    ID int IDENTITY(1,1) PRIMARY KEY,
    TipoDTE int NOT NULL,
    Folio int NOT NULL,
    FechaUso datetime NOT NULL DEFAULT GETDATE(),
    UNIQUE(TipoDTE, Folio)
)
```

2. **O usar otra lógica** según tu sistema existente

## Notas

- El XML del CAF está en la columna `CAFContenido`
- Los rangos de folios están en `RangoInicio` y `RangoFin`
- El estado se compara con el string `'Activo'` (no es bit)
