-- Script para limpiar el CAF directamente en la base de datos
-- ⚠️ IMPORTANTE: Haz un BACKUP antes de ejecutar este script

-- PASO 1: Crear backup
CREATE TABLE IF NOT EXISTS CAF_backup AS SELECT * FROM CAF WHERE TD = 33;

-- PASO 2: Ver el contenido actual (para identificar el problema)
SELECT 
    ID,
    TD,
    LENGTH(CAFContenido) as LongitudOriginal,
    LEFT(CAFContenido, 300) as Inicio,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN 'TIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN 'TIENE <hr>'
        ELSE 'SIN HTML'
    END as Problema
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- PASO 3: Extraer solo el XML válido
-- Esto busca desde <?xml hasta </AUTORIZACION> y extrae solo esa parte
UPDATE CAF 
SET CAFContenido = SUBSTRING(
    CAFContenido,
    LOCATE('<?xml', CAFContenido),
    LOCATE('</AUTORIZACION>', CAFContenido) - LOCATE('<?xml', CAFContenido) + LENGTH('</AUTORIZACION>')
)
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
    AND CAFContenido LIKE '%<?xml%'
    AND CAFContenido LIKE '%</AUTORIZACION>%'
    AND (CAFContenido LIKE '%<td%' OR CAFContenido LIKE '%<hr%');

-- PASO 4: Verificar que se limpió correctamente
SELECT 
    ID,
    TD,
    LENGTH(CAFContenido) as LongitudLimpia,
    LEFT(CAFContenido, 200) as InicioLimpio,
    RIGHT(CAFContenido, 100) as FinLimpio,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ LIMPIO CORRECTAMENTE'
        ELSE '❌ AÚN TIENE PROBLEMAS'
    END as Estado
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
