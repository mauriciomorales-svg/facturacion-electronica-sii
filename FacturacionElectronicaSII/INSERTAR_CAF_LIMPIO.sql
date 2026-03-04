-- Script para insertar/actualizar el CAF limpiamente en la base de datos
-- ⚠️ IMPORTANTE: Reemplaza 'XML_DEL_CAF_AQUI' con el XML completo del CAF descargado

-- PASO 1: Backup (por si acaso)
CREATE TABLE IF NOT EXISTS CAF_backup_antes_insertar AS SELECT * FROM CAF WHERE TD = 33;

-- PASO 2: Si ya existe un CAF para TD=33, actualizarlo
-- Si no existe, se insertará uno nuevo
UPDATE CAF 
SET 
    CAFContenido = 'XML_DEL_CAF_AQUI',
    FechaCarga = NOW(),
    Estado = 'Activo'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- Si no se actualizó ninguna fila (no existe el registro), insertarlo
INSERT INTO CAF (TD, RangoInicio, RangoFin, FechaCarga, Estado, CAFContenido, FRMA)
SELECT 
    33 as TD,
    -- Extraer RangoInicio desde el XML (si está disponible)
    CAST(SUBSTRING_INDEX(SUBSTRING_INDEX('XML_DEL_CAF_AQUI', '<D>', -1), '</D>', 1) AS UNSIGNED) as RangoInicio,
    -- Extraer RangoFin desde el XML (si está disponible)
    CAST(SUBSTRING_INDEX(SUBSTRING_INDEX('XML_DEL_CAF_AQUI', '<H>', -1), '</H>', 1) AS UNSIGNED) as RangoFin,
    NOW() as FechaCarga,
    'Activo' as Estado,
    'XML_DEL_CAF_AQUI' as CAFContenido,
    '' as FRMA
WHERE NOT EXISTS (
    SELECT 1 FROM CAF WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
);

-- PASO 3: Verificar que se insertó/actualizó correctamente
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 200) as Primeros200Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' THEN '✅ TIENE <?xml>'
        ELSE '❌ NO TIENE <?xml>'
    END as TieneXML,
    CASE 
        WHEN CAFContenido LIKE '%</AUTORIZACION>%' THEN '✅ TIENE </AUTORIZACION>'
        ELSE '❌ NO TIENE </AUTORIZACION>'
    END as TieneCierre,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN '❌ TIENE <td> - CORRUPTO'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ TIENE <hr> - CORRUPTO'
        WHEN CAFContenido LIKE '%<html%' THEN '❌ TIENE <html> - CORRUPTO'
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
        THEN '✅ XML VÁLIDO Y LIMPIO'
        ELSE '⚠️ REVISAR'
    END as EstadoFinal
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
