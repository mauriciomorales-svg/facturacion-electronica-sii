-- Script para verificar que el CAF se limpió correctamente
-- Ejecuta este script para confirmar que el CAF ya no tiene HTML corrupto

SELECT 
    ID,
    TD,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 200) as Primeros200Caracteres,
    RIGHT(CAFContenido, 100) as Ultimos100Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' THEN '✅ TIENE <?xml>'
        ELSE '❌ NO TIENE <?xml>'
    END as TieneXML,
    CASE 
        WHEN CAFContenido LIKE '%</AUTORIZACION>%' THEN '✅ TIENE </AUTORIZACION>'
        ELSE '❌ NO TIENE </AUTORIZACION>'
    END as TieneCierre,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN '❌ AÚN TIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ AÚN TIENE <hr>'
        WHEN CAFContenido LIKE '%<html%' THEN '❌ AÚN TIENE <html>'
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
        THEN '✅ XML VÁLIDO'
        ELSE '⚠️ REVISAR MANUALMENTE'
    END as EstadoFinal
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
