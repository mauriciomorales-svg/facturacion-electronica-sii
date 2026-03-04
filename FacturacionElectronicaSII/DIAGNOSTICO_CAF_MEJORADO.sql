-- ============================================================
-- DIAGNÓSTICO MEJORADO DEL CAF - Detección de HTML Corrupto
-- ============================================================
-- Ejecuta estas consultas en MySQL para diagnosticar el problema

-- 1. RESUMEN Y DETECCIÓN DE PROBLEMAS
SELECT 
    ID,
    TD,
    Estado,
    LENGTH(CAFContenido) AS Longitud,
    LEFT(CAFContenido, 2000) AS Primeros2000Caracteres,
    RIGHT(CAFContenido, 500) AS Ultimos500Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN 'CONTIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN 'CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<table%' THEN 'CONTIENE <table>'
        WHEN CAFContenido LIKE '%<tr%' THEN 'CONTIENE <tr>'
        WHEN CAFContenido LIKE '%<html%' THEN 'CONTIENE <html>'
        WHEN CAFContenido LIKE '%<body%' THEN 'CONTIENE <body>'
        WHEN CAFContenido LIKE '%<?xml%' AND CAFContenido LIKE '%</AUTORIZACION>%' 
        THEN 'XML POSIBLEMENTE VÁLIDO'
        ELSE 'REVISAR MANUALMENTE'
    END AS EstadoXML
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- 2. CONTENIDO COMPLETO (para copiar y analizar)
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- 3. BUSCAR POSICIÓN DE INICIO Y FIN DEL XML
SELECT 
    LOCATE('<?xml', CAFContenido) AS PosicionInicioXML,
    LOCATE('<AUTORIZACION>', CAFContenido) AS PosicionInicioAUTORIZACION,
    LOCATE('</AUTORIZACION>', CAFContenido) AS PosicionFinAUTORIZACION,
    LOCATE('<td', CAFContenido) AS PosicionPrimerTD,
    LOCATE('<hr', CAFContenido) AS PosicionPrimerHR,
    LOCATE('<table', CAFContenido) AS PosicionPrimerTABLE
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- 4. CONTAR OCURRENCIAS DE TAGS HTML
SELECT 
    (LENGTH(CAFContenido) - LENGTH(REPLACE(CAFContenido, '<td', ''))) / 3 AS CantidadTD,
    (LENGTH(CAFContenido) - LENGTH(REPLACE(CAFContenido, '<hr', ''))) / 3 AS CantidadHR,
    (LENGTH(CAFContenido) - LENGTH(REPLACE(CAFContenido, '<table', ''))) / 6 AS CantidadTABLE,
    (LENGTH(CAFContenido) - LENGTH(REPLACE(CAFContenido, '<tr', ''))) / 3 AS CantidadTR
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
