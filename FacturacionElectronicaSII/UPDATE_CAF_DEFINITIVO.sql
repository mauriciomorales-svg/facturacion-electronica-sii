-- =====================================================
-- UPDATE DEFINITIVO DEL CAF - Usando variable para evitar problemas de escape
-- =====================================================

USE dbisabel2;

-- Verificar estado ANTES
SELECT 
    ID, 
    TD, 
    Estado,
    LENGTH(CAFContenido) as LongitudAntes,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'TIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN 'TIENE <td>'
        WHEN CAFContenido LIKE '%<AUTORIZACION%' THEN 'OK'
        ELSE 'REVISAR'
    END as EstadoAntes
FROM CAF 
WHERE TD = 33;

-- ACTUALIZAR usando variable SET
SET @xml_limpio = '<?xml version=\"1.0\"?>\n<AUTORIZACION>\n<CAF version=\"1.0\">\n<DA>\n<RE>8451335-0</RE>\n<RS>MARTA INALBIA URRA ESCOBAR</RS>\n<TD>33</TD>\n<RNG><D>1</D><H>60</H></RNG>\n<FA>2018-09-22</FA>\n<RSAPK><M>k+e6qyIYl4EF9fH1hEFk9H6F5LZmBplwq+sKpP0osX/lNoqEzPoUicyTWXJQpZIlDjnGXGbY7u7X7jfgG71TwQ==</M><E>Aw==</E></RSAPK>\n<IDK>100</IDK>\n</DA>\n<FRMA algoritmo=\"SHA1withRSA\">oAK8TyCOJgpo6G9hc4jbQ+RXMLiB3csxjCjxU8wl1QRi/ZKqYxAeWEqtXUN3fYGxkyabjB6VM3BL3Jb5wAPvaA==</FRMA>\n</CAF>\n<RSASK>-----BEGIN RSA PRIVATE KEY-----\nMIIBOgIBAAJBAJPnuqsiGJeBBfXx9YRBZPR+heS2ZgaZcKvrCqT9KLF/5TaKhMz6\nFInMk1lyUKWSJQ45xlxm2O7u1+434Bu9U8ECAQMCQGKafHIWuw+rWU6hTlgrmKL/\nA+3O7q8Q9cfyBxioxcuplVql6QiYnMaa1NsVg5GK6oSv7BN7zDHiopWnKTraRuMC\nIQDD+SNu4QOBS3SwLxypz5zoL0eJJNhcLKRm6XGecqfRGwIhAME1bjhfEagUb6Ph\ntWF7pN0X6lsaVMp3dn0kS4PQzhhTAiEAgqYXn0CtANz4dXS9xopomsovsMM66B3C\n70ZLvvcai2cCIQCAzkl66gvFYvUX685A/RiTZUbnZuMxpPmowt0CizQQNwIhAKK4\nDwPCaGW3+IXLms4z5zA4DJbX5TYlu9d3ZsBOBrxO\n-----END RSA PRIVATE KEY-----\n</RSASK>\n\n<RSAPUBK>-----BEGIN PUBLIC KEY-----\nMFowDQYJKoZIhvcNAQEBBQADSQAwRgJBAJPnuqsiGJeBBfXx9YRBZPR+heS2ZgaZ\ncKvrCqT9KLF/5TaKhMz6FInMk1lyUKWSJQ45xlxm2O7u1+434Bu9U8ECAQM=\n-----END PUBLIC KEY-----\n</RSAPUBK>\n</AUTORIZACION>';

-- Reemplazar \n por saltos de línea reales
SET @xml_limpio = REPLACE(@xml_limpio, '\\n', CHAR(10));

-- ACTUALIZAR TODOS los registros con TD=33
UPDATE CAF 
SET CAFContenido = @xml_limpio
WHERE TD = 33;

-- Verificar estado DESPUÉS
SELECT 
    ID, 
    TD, 
    Estado,
    LENGTH(CAFContenido) as LongitudDespues,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'TIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN 'TIENE <td>'
        WHEN CAFContenido LIKE '%<AUTORIZACION%' THEN 'OK - TIENE AUTORIZACION'
        ELSE 'REVISAR'
    END as EstadoDespues,
    LEFT(CAFContenido, 200) as Primeros200Chars
FROM CAF 
WHERE TD = 33;
