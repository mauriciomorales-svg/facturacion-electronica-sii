-- Script ROBUSTO para limpiar el CAF - Extrae SOLO el XML válido
-- ⚠️ IMPORTANTE: Haz un BACKUP antes de ejecutar

-- PASO 1: Crear backup
CREATE TABLE IF NOT EXISTS CAF_backup_antes_limpieza AS SELECT * FROM CAF WHERE TD = 33;

-- PASO 2: Ver el contenido actual (para diagnóstico)
SELECT 
    ID,
    TD,
    LENGTH(CAFContenido) as LongitudOriginal,
    LOCATE('<?xml', CAFContenido) as PosicionInicioXML,
    LOCATE('</AUTORIZACION>', CAFContenido) as PosicionFinXML,
    LEFT(CAFContenido, 300) as Primeros300Caracteres
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- PASO 3: Limpiar el CAF - Extraer SOLO desde <?xml hasta </AUTORIZACION>
-- Este script es más robusto y maneja diferentes casos
UPDATE CAF 
SET CAFContenido = TRIM(
    SUBSTRING(
        CAFContenido,
        GREATEST(1, IFNULL(LOCATE('<?xml', CAFContenido), LOCATE('<AUTORIZACION>', CAFContenido))),
        IFNULL(
            LOCATE('</AUTORIZACION>', CAFContenido) - LOCATE('<?xml', CAFContenido) + LENGTH('</AUTORIZACION>'),
            LOCATE('</AUTORIZACION>', CAFContenido) - LOCATE('<AUTORIZACION>', CAFContenido) + LENGTH('</AUTORIZACION>')
        )
    )
)
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
    AND (
        CAFContenido LIKE '%<?xml%' OR CAFContenido LIKE '%<AUTORIZACION>%'
    )
    AND CAFContenido LIKE '%</AUTORIZACION>%';

-- PASO 4: Verificar el resultado
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
             AND CAFContenido NOT LIKE '%<html%'
        THEN '✅ XML VÁLIDO Y LIMPIO'
        WHEN CAFContenido LIKE '%<td%' THEN '❌ AÚN TIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ AÚN TIENE <hr>'
        ELSE '⚠️ REVISAR MANUALMENTE'
    END as EstadoFinal
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
