-- Script para verificar que el CAF se actualizó correctamente
-- Ejecuta este script para confirmar que el CAF está limpio

SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
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
        WHEN CAFContenido LIKE '%8451335-0%' THEN '✅ TIENE RUT CORRECTO'
        ELSE '❌ NO TIENE RUT CORRECTO'
    END as TieneRutCorrecto,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN '❌ AÚN TIENE <td> - NO SE ACTUALIZÓ'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ AÚN TIENE <hr> - NO SE ACTUALIZÓ'
        WHEN CAFContenido LIKE '%<html%' THEN '❌ AÚN TIENE <html> - NO SE ACTUALIZÓ'
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido LIKE '%8451335-0%'
        THEN '✅ XML VÁLIDO Y ACTUALIZADO CORRECTAMENTE'
        ELSE '⚠️ REVISAR - Puede que no se haya actualizado'
    END as EstadoFinal
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
