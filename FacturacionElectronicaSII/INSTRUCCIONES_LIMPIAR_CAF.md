# Instrucciones para Limpiar el CAF Corrupto

## Problema Confirmado

El XML del CAF en la base de datos contiene etiquetas HTML (`<td>`, `<hr>`) que corrompen el XML y causan errores de parseo.

## Solución Recomendada: Limpiar en la Base de Datos

### Opción 1: Script SQL Automático (Más Rápido)

1. **Abre tu cliente MySQL** (MySQL Workbench, phpMyAdmin, etc.)

2. **Ejecuta el script `LIMPIAR_CAF_DIRECTO.sql`**

   El script:
   - Crea un backup automático
   - Extrae solo la parte XML válida (desde `<?xml` hasta `</AUTORIZACION>`)
   - Verifica que se limpió correctamente

3. **Reinicia el servicio** y prueba nuevamente

### Opción 2: Limpieza Manual

1. **Ver el contenido completo:**
```sql
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

2. **Identificar el XML válido:**
   - Debe comenzar con: `<?xml version="1.0"?>`
   - Debe terminar con: `</AUTORIZACION>`

3. **Copiar solo el XML válido** (sin el HTML que está antes o después)

4. **Actualizar la base de datos:**
```sql
UPDATE CAF 
SET CAFContenido = 'AQUÍ_PEGA_SOLO_EL_XML_VÁLIDO_COMPLETO'
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

## Verificación

Después de limpiar, verifica:

```sql
SELECT 
    ID,
    TD,
    LENGTH(CAFContenido) as Longitud,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ VÁLIDO'
        ELSE '❌ AÚN CORRUPTO'
    END as Estado
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

## Después de Limpiar

1. **Reinicia el servicio**
2. **Prueba la emisión de DTE**
3. **El error debería desaparecer**

## Nota

El código del servicio también tiene una función de limpieza automática, pero es mejor limpiar el CAF directamente en la base de datos para evitar problemas futuros.
