-- Script para verificar el contenido del CAF en la base de datos
-- Ejecuta este script en tu cliente MySQL (MySQL Workbench, phpMyAdmin, etc.)

-- 1. Ver información básica del CAF
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 200) as Primeros200Caracteres
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
ORDER BY FechaCarga DESC
LIMIT 1;

-- 2. Ver si contiene etiquetas HTML problemáticas
SELECT 
    ID,
    TD,
    CASE 
        WHEN CAFContenido LIKE '%<hr%' THEN 'CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<td%' THEN 'CONTIENE <td>'
        WHEN CAFContenido LIKE '%<table%' THEN 'CONTIENE <table>'
        WHEN CAFContenido LIKE '%<html%' THEN 'CONTIENE <html>'
        ELSE 'NO CONTIENE HTML'
    END as ContieneHTML,
    LENGTH(CAFContenido) as Longitud
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- 3. Ver el XML completo (CUIDADO: puede ser muy largo)
-- Descomenta la siguiente línea solo si necesitas ver el XML completo
-- SELECT CAFContenido FROM CAF WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO' LIMIT 1;

-- 4. Verificar que el XML tenga la estructura correcta
SELECT 
    ID,
    TD,
    CASE 
        WHEN CAFContenido LIKE '%<CAF%' THEN 'TIENE <CAF>'
        ELSE 'NO TIENE <CAF>'
    END as TieneCAF,
    CASE 
        WHEN CAFContenido LIKE '%<DA%' THEN 'TIENE <DA>'
        ELSE 'NO TIENE <DA>'
    END as TieneDA,
    CASE 
        WHEN CAFContenido LIKE '%<RSASK%' THEN 'TIENE <RSASK>'
        ELSE 'NO TIENE <RSASK>'
    END as TieneRSASK
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
