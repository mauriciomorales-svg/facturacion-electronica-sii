-- Script para limpiar el CAF y extraer solo el XML válido
-- EJECUTA ESTE SCRIPT EN TU CLIENTE MySQL

-- PASO 1: Ver el contenido completo del CAF (para identificar dónde está el HTML)
SELECT 
    ID,
    TD,
    CAFContenido as XMLCompleto
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- PASO 2: Extraer solo la parte XML válida (desde <?xml hasta </AUTORIZACION>)
-- NOTA: Ajusta los índices según tu estructura real
UPDATE CAF 
SET CAFContenido = SUBSTRING(
    CAFContenido,
    LOCATE('<?xml', CAFContenido),
    LOCATE('</AUTORIZACION>', CAFContenido) - LOCATE('<?xml', CAFContenido) + LENGTH('</AUTORIZACION>')
)
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
    AND CAFContenido LIKE '%<?xml%'
    AND CAFContenido LIKE '%</AUTORIZACION>%';

-- PASO 3: Verificar que el XML ahora sea válido
SELECT 
    ID,
    TD,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 200) as InicioXML,
    RIGHT(CAFContenido, 100) as FinXML,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'CONTIENE <hr> - CORRUPTO'
        WHEN CAFContenido LIKE '%<td%' THEN 'CONTIENE <td> - CORRUPTO'
        WHEN CAFContenido LIKE '%<html%' THEN 'CONTIENE <html> - CORRUPTO'
        WHEN CAFContenido LIKE '%<?xml%' AND CAFContenido LIKE '%</AUTORIZACION>%' THEN 'XML VÁLIDO'
        ELSE 'REVISAR MANUALMENTE'
    END as EstadoXML
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
