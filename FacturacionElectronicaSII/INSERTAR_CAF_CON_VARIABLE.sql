-- Script alternativo usando variable MySQL (más fácil de manejar)
-- Este script es más fácil porque puedes pegar el XML en una variable

-- PASO 1: Backup
CREATE TABLE IF NOT EXISTS CAF_backup_antes_insertar AS SELECT * FROM CAF WHERE TD = 33;

-- PASO 2: Definir variable con el XML del CAF
-- Reemplaza el contenido entre las comillas con el XML completo del CAF
SET @xml_caf = 'XML_DEL_CAF_AQUI';

-- PASO 3: Validar que el XML sea válido antes de insertar
SELECT 
    CASE 
        WHEN @xml_caf LIKE '%<?xml%' THEN '✅ TIENE <?xml>'
        ELSE '❌ NO TIENE <?xml>'
    END as TieneXML,
    CASE 
        WHEN @xml_caf LIKE '%</AUTORIZACION>%' THEN '✅ TIENE </AUTORIZACION>'
        ELSE '❌ NO TIENE </AUTORIZACION>'
    END as TieneCierre,
    CASE 
        WHEN @xml_caf LIKE '%<td%' THEN '❌ TIENE <td> - CORRUPTO'
        WHEN @xml_caf LIKE '%<hr%' THEN '❌ TIENE <hr> - CORRUPTO'
        WHEN @xml_caf LIKE '%<?xml%' AND @xml_caf LIKE '%</AUTORIZACION>%'
        THEN '✅ XML VÁLIDO'
        ELSE '⚠️ REVISAR'
    END as EstadoXML,
    LENGTH(@xml_caf) as Longitud;

-- PASO 4: Actualizar o insertar el CAF
-- Si ya existe un CAF para TD=33, actualizarlo
UPDATE CAF 
SET 
    CAFContenido = @xml_caf,
    FechaCarga = NOW(),
    Estado = 'Activo'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- Si no se actualizó ninguna fila, insertar uno nuevo
INSERT INTO CAF (TD, RangoInicio, RangoFin, FechaCarga, Estado, CAFContenido, FRMA)
SELECT 
    33,
    -- Intentar extraer rangos desde el XML (puede fallar si el formato es diferente)
    NULLIF(CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(@xml_caf, '<D>', -1), '</D>', 1) AS UNSIGNED), 0),
    NULLIF(CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(@xml_caf, '<H>', -1), '</H>', 1) AS UNSIGNED), 0),
    NOW(),
    'Activo',
    @xml_caf,
    ''
WHERE NOT EXISTS (
    SELECT 1 FROM CAF WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
);

-- PASO 5: Verificar el resultado final
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
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ XML VÁLIDO Y LIMPIO'
        ELSE '❌ AÚN TIENE PROBLEMAS'
    END as EstadoFinal
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
