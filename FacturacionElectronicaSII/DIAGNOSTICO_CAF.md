# Diagnóstico de CAF - Retorna 0 Folios Disponibles

## Posibles Causas

Si el endpoint `/api/CAF/folios-disponibles/33` retorna `0`, puede ser por:

### 1. No se encuentra el CAF en la base de datos

**Verificar:**
- Que exista un registro en la tabla `CAF` con:
  - `TD = 33`
  - `Estado = 'Activo'` (exactamente así, con mayúscula A)

**Consulta de verificación:**
```sql
SELECT ID, TD, RangoInicio, RangoFin, Estado, FechaCarga
FROM CAF
WHERE TD = 33 AND Estado = 'Activo'
```

### 2. La consulta SQL no está funcionando

**Revisar logs** en la consola del servicio. Deberías ver mensajes como:
- "Obteniendo CAF para tipo DTE 33 desde base de datos"
- "CAF encontrado en base de datos..." (si lo encuentra)
- "No se encontró CAF activo..." (si no lo encuentra)

### 3. Problema con la cadena de conexión

**Verificar** en `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=dbisabel2;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Ajustar si tu servidor SQL tiene otro nombre o requiere autenticación diferente.

### 4. Tabla de Folios Usados no existe

Si no tienes la tabla `FoliosUsados`, el servicio asumirá que **todos los folios están disponibles**. 

Si retorna 0, es porque **no está encontrando el CAF**, no por los folios usados.

## Cómo Diagnosticar

1. **Revisa los logs** en la consola donde ejecutaste `dotnet run`
2. **Prueba el endpoint** `/api/CAF/33` para ver si encuentra el CAF
3. **Ejecuta la consulta SQL** directamente en tu base de datos para verificar que existe el registro

## Solución Rápida

Si el CAF existe pero no lo encuentra, puede ser:
- El nombre de la tabla es diferente (no es `CAF`)
- El nombre de la columna `Estado` tiene espacios o diferente formato
- La columna `CAFContenido` tiene otro nombre

Revisa los logs para ver el error exacto.
