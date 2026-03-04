-- UPDATE DIRECTO del CAF (más simple y directo)
-- Ejecuta este script si el anterior no funcionó

-- Backup
CREATE TABLE IF NOT EXISTS CAF_backup AS SELECT * FROM CAF WHERE TD = 33;

-- UPDATE directo
UPDATE CAF 
SET 
    CAFContenido = '<?xml version="1.0"?>
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
</AUTORIZACION>',
    RangoInicio = 1,
    RangoFin = 60,
    FechaCarga = NOW(),
    Estado = 'Activo'
WHERE TD = 33;

-- Verificar
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    LENGTH(CAFContenido) as Longitud,
    CASE 
        WHEN CAFContenido LIKE '%8451335-0%' 
             AND CAFContenido LIKE '%<?xml%'
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ ACTUALIZADO CORRECTAMENTE'
        ELSE '❌ NO SE ACTUALIZÓ'
    END as Estado
FROM CAF 
WHERE TD = 33;
