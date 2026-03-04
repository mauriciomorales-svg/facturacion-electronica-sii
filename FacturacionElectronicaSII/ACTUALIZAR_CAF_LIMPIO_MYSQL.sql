-- =====================================================
-- SCRIPT SQL PARA MYSQL: Actualizar CAF con XML Limpio
-- =====================================================
-- Fecha: 2025-01-19
-- Fuente: Dump guardado automáticamente por el servicio
-- Archivo: C:\FacturasElectronicas\Logs\Dumps\CAF_HTML_DETECTADO_20260119_052214.html
-- =====================================================
-- Este script actualiza el CAF tipo 33 con el XML limpio
-- extraído del dump guardado por el servicio.
-- =====================================================

-- PASO 1: Verificar el CAF actual (antes de actualizar)
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudActual,
    LEFT(CAFContenido, 200) as Primeros200Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN 'CONTIENE <td>'
        WHEN CAFContenido LIKE '%<table%' THEN 'CONTIENE <table>'
        WHEN CAFContenido LIKE '%<html%' THEN 'CONTIENE <html>'
        ELSE 'NO CONTIENE HTML'
    END as ContieneHTML
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
ORDER BY FechaCarga DESC
LIMIT 1;

-- =====================================================
-- PASO 2: ACTUALIZAR EL CAF CON XML LIMPIO
-- =====================================================
-- ⚠️ IMPORTANTE: Revisa el resultado del PASO 1 antes de ejecutar esto
-- =====================================================

UPDATE CAF 
SET CAFContenido = '<?xml version="1.0"?>
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
</AUTORIZACION>'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- =====================================================
-- PASO 3: VERIFICAR QUE SE ACTUALIZÓ CORRECTAMENTE
-- =====================================================

SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudNueva,
    LEFT(CAFContenido, 200) as Primeros200Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<AUTORIZACION%' THEN '✓ TIENE AUTORIZACION'
        ELSE '✗ NO TIENE AUTORIZACION'
    END as TieneAutorizacion,
    CASE 
        WHEN CAFContenido LIKE '%<CAF%' THEN '✓ TIENE CAF'
        ELSE '✗ NO TIENE CAF'
    END as TieneCAF,
    CASE 
        WHEN CAFContenido LIKE '%<RSASK%' THEN '✓ TIENE RSASK'
        ELSE '✗ NO TIENE RSASK'
    END as TieneRSASK,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN '✗ CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN '✗ CONTIENE <td>'
        WHEN CAFContenido LIKE '%<table%' THEN '✗ CONTIENE <table>'
        WHEN CAFContenido LIKE '%<html%' THEN '✗ CONTIENE <html>'
        ELSE '✓ NO CONTIENE HTML'
    END as EstadoHTML
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
ORDER BY FechaCarga DESC
LIMIT 1;

-- =====================================================
-- INSTRUCCIONES:
-- =====================================================
-- 1. Ejecuta el PASO 1 para ver el estado actual
-- 2. Si confirmas que el CAF está corrupto, ejecuta el PASO 2
-- 3. Ejecuta el PASO 3 para verificar que se actualizó correctamente
-- 4. Reinicia el servicio de facturación electrónica
-- 5. Prueba la emisión de un DTE nuevamente
-- =====================================================
