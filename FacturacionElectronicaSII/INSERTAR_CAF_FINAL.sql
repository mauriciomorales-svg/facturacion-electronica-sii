-- Script FINAL para insertar el CAF limpiamente
-- Este script está listo para ejecutar con el XML proporcionado

-- PASO 1: Backup (por seguridad)
CREATE TABLE IF NOT EXISTS CAF_backup_antes_insertar AS SELECT * FROM CAF WHERE TD = 33;

-- PASO 2: Definir variable con el XML del CAF (limpio y válido)
SET @xml_caf = '<?xml version="1.0"?>
<AUTORIZACION>
<CAF version="1.0">
<DA>
<RE>8451335-0</RE>
<RS>MARTA INALBIA URRA ESCOBAR</RS>
<TD>33</TD>
<RNG><D>1</D><H>60</H></RNG>
<FA>2018-09-22</FA>
<RSAPK><M>k+e6qyIYl4EF9fH1hEFk9H6F5LZmBplwq+sKpP0osX/lNoqEzPoUicyTWXJQpZIlDjnGXGbY7u7X7jfgG71TwQ==</M><E>Aw==</E></RSAPK>
<IDK>100</IDK>
</DA>
<FRMA algoritmo="SHA1withRSA">oAK8TyCOJgpo6G9hc4jbQ+RXMLiB3csxjCjxU8wl1QRi/ZKqYxAeWEqtXUN3fYGxkyabjB6VM3BL3Jb5wAPvaA==</FRMA>
</CAF>
<RSASK>-----BEGIN RSA PRIVATE KEY-----
MIIBOgIBAAJBAJPnuqsiGJeBBfXx9YRBZPR+heS2ZgaZcKvrCqT9KLF/5TaKhMz6
FInMk1lyUKWSJQ45xlxm2O7u1+434Bu9U8ECAQMCQGKafHIWuw+rWU6hTlgrmKL/
A+3O7q8Q9cfyBxioxcuplVql6QiYnMaa1NsVg5GK6oSv7BN7zDHiopWnKTraRuMC
IQDD+SNu4QOBS3SwLxypz5zoL0eJJNhcLKRm6XGecqfRGwIhAME1bjhfEagUb6Ph
tWF7pN0X6lsaVMp3dn0kS4PQzhhTAiEAgqYXn0CtANz4dXS9xopomsovsMM66B3C
70ZLvvcai2cCIQCAzkl66gvFYvUX685A/RiTZUbnZuMxpPmowt0CizQQNwIhAKK4
DwPCaGW3+IXLms4z5zA4DJbX5TYlu9d3ZsBOBrxO
-----END RSA PRIVATE KEY-----
</RSASK>

<RSAPUBK>-----BEGIN PUBLIC KEY-----
MFowDQYJKoZIhvcNAQEBBQADSQAwRgJBAJPnuqsiGJeBBfXx9YRBZPR+heS2ZgaZ
cKvrCqT9KLF/5TaKhMz6FInMk1lyUKWSJQ45xlxm2O7u1+434Bu9U8ECAQM=
-----END PUBLIC KEY-----
</RSAPUBK>
</AUTORIZACION>';

-- PASO 3: Validar el XML antes de insertar
SELECT 
    'VALIDACIÓN DEL XML' as Paso,
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
        THEN '✅ XML VÁLIDO Y LIMPIO'
        ELSE '⚠️ REVISAR'
    END as EstadoXML,
    LENGTH(@xml_caf) as Longitud;

-- PASO 4: Actualizar el CAF existente o insertar uno nuevo
-- Primero intentar actualizar
UPDATE CAF 
SET 
    CAFContenido = @xml_caf,
    RangoInicio = 1,  -- Extraído del XML: <D>1</D>
    RangoFin = 60,    -- Extraído del XML: <H>60</H>
    FechaCarga = NOW(),
    Estado = 'Activo'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- Si no se actualizó ninguna fila (no existe el registro), insertarlo
INSERT INTO CAF (TD, RangoInicio, RangoFin, FechaCarga, Estado, CAFContenido, FRMA)
SELECT 
    33,
    1,  -- Rango inicial desde <D>1</D>
    60, -- Rango final desde <H>60</H>
    NOW(),
    'Activo',
    @xml_caf,
    ''  -- FRMA se puede extraer del XML si es necesario
WHERE NOT EXISTS (
    SELECT 1 FROM CAF WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
);

-- PASO 5: Verificar el resultado final
SELECT 
    'RESULTADO FINAL' as Paso,
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 150) as Primeros150Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
             AND CAFContenido NOT LIKE '%<html%'
        THEN '✅ XML VÁLIDO Y LIMPIO - LISTO PARA USAR'
        WHEN CAFContenido LIKE '%<td%' THEN '❌ AÚN TIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ AÚN TIENE <hr>'
        ELSE '⚠️ REVISAR MANUALMENTE'
    END as EstadoFinal
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
