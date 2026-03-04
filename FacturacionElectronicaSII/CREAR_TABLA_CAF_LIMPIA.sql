-- ============================================================================
-- SCRIPT PARA CREAR TABLA CAF LIMPIA E INSERTAR NUEVO CAF
-- ============================================================================
-- Este script:
-- 1. Elimina la tabla CAF existente (si existe)
-- 2. Crea una nueva tabla CAF limpia
-- 3. Inserta el nuevo CAF proporcionado
-- ============================================================================

-- PASO 1: Eliminar la tabla CAF existente (si existe)
DROP TABLE IF EXISTS CAF;

-- PASO 2: Crear nueva tabla CAF limpia
CREATE TABLE CAF (
    ID INT AUTO_INCREMENT PRIMARY KEY,
    TD INT NOT NULL COMMENT 'Tipo de DTE (33=Factura, 39=Boleta, 61=Nota Crédito, 56=Nota Débito)',
    RangoInicio INT NOT NULL COMMENT 'Folio inicial del rango autorizado',
    RangoFin INT NOT NULL COMMENT 'Folio final del rango autorizado',
    FechaCarga DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Fecha de carga del CAF en el sistema',
    Estado VARCHAR(20) NOT NULL DEFAULT 'Activo' COMMENT 'Estado del CAF (Activo, Inactivo, Agotado)',
    CAFContenido TEXT NOT NULL COMMENT 'XML completo del CAF',
    FRMA VARCHAR(500) NULL COMMENT 'Firma del CAF (opcional)',
    INDEX idx_TD_Estado (TD, Estado),
    INDEX idx_Estado (Estado)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Tabla de Códigos de Autorización de Folios (CAF)';

-- PASO 3: Insertar el nuevo CAF
-- CAF para Factura Electrónica (TD=33)
-- Rango: 62-90
-- Fecha Autorización: 2026-01-19
INSERT INTO CAF (
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    CAFContenido,
    FRMA
) VALUES (
    33,  -- Tipo DTE: Factura Electrónica
    62,  -- Folio inicial del rango
    90,  -- Folio final del rango
    '2026-01-19 00:00:00',  -- Fecha de autorización del CAF
    'Activo',  -- Estado activo
    '<?xml version="1.0"?>
<AUTORIZACION>
<CAF version="1.0">
<DA>
<RE>8451335-0</RE>
<RS>MARTA INALBIA URRA ESCOBAR</RS>
<TD>33</TD>
<RNG><D>62</D><H>90</H></RNG>
<FA>2026-01-19</FA>
<RSAPK><M>vVPbpja9b8r1kFn2SqI6A4xCyk823Hw3kTY9hUsJfB+WvP86G8egx9SvsGzxgC4KXG1mzhrD5sYzxMEFd1hTtQ==</M><E>Aw==</E></RSAPK>
<IDK>100</IDK>
</DA>
<FRMA algoritmo="SHA1withRSA">FuOX7aAU8JgmW9k7ul8+ExK9klxqYJjgfbsyNs27zTt2+8yc+ZasL83tQ/oChLbrlv1QrZsN7EfsOn9VDp26vQ==</FRMA>
</CAF>
<RSASK>-----BEGIN RSA PRIVATE KEY-----
MIIBOgIBAAJBAL1T26Y2vW/K9ZBZ9kqiOgOMQspPNtx8N5E2PYVLCXwflrz/OhvH
oMfUr7Bs8YAuClxtZs4aw+bGM8TBBXdYU7UCAQMCQH4358QkfkqHTmA7+YcW0Vey
1zGKJJL9emDO064yBlK+ksiHHJPigW3cHirehsc5FMDw7FzoISW6lYeIfkVmu7MC
IQDzl1GqkXMYsh99LsxKi82yRAER7ojz8RaK4MJx6N35FwIhAMb44uSsgMXw6wVB
UtzJirj3AvJUNZ49F8iYsdYmYEETAiEAomThHGD3ZcwU/h8y3F0zzC1WC/RbTUtk
XJXW9ps+pg8CIQCEpeyYcwCD9fIDgOHohlx7T1dMOCO+02UwZcvkGZWAtwIhAMvr
u+jb1f+G6YxJqosgt/2lhFVVDOqWz8zz3ARFztvV
-----END RSA PRIVATE KEY-----
</RSASK>
<RSAPUBK>-----BEGIN PUBLIC KEY-----
MFowDQYJKoZIhvcNAQEBBQADSQAwRgJBAL1T26Y2vW/K9ZBZ9kqiOgOMQspPNtx8
N5E2PYVLCXwflrz/OhvHoMfUr7Bs8YAuClxtZs4aw+bGM8TBBXdYU7UCAQM=
-----END PUBLIC KEY-----
</RSAPUBK>
</AUTORIZACION>',
    'FuOX7aAU8JgmW9k7ul8+ExK9klxqYJjgfbsyNs27zTt2+8yc+ZasL83tQ/oChLbrlv1QrZsN7EfsOn9VDp26vQ=='  -- FRMA extraído del XML
);

-- PASO 4: Verificar que se insertó correctamente
SELECT 
    'VERIFICACIÓN' as Paso,
    ID,
    TD as TipoDTE,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' THEN '✅ TIENE <?xml>'
        ELSE '❌ NO TIENE <?xml>'
    END as TieneXML,
    CASE 
        WHEN CAFContenido LIKE '%</AUTORIZACION>%' THEN '✅ TIENE </AUTORIZACION>'
        ELSE '❌ NO TIENE </AUTORIZACION>'
    END as TieneCierre,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN '❌ TIENE <td> - CORRUPTO'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ TIENE <hr> - CORRUPTO'
        WHEN CAFContenido LIKE '%<?xml%' AND CAFContenido LIKE '%</AUTORIZACION>%'
        THEN '✅ XML VÁLIDO Y LIMPIO'
        ELSE '⚠️ REVISAR'
    END as EstadoXML
FROM CAF
WHERE TD = 33;

-- PASO 5: Mostrar información del CAF insertado
SELECT 
    'INFORMACIÓN DEL CAF' as Tipo,
    TD as TipoDTE,
    CONCAT(RangoInicio, ' - ', RangoFin) as RangoFolios,
    RangoFin - RangoInicio + 1 as TotalFolios,
    FechaCarga as FechaAutorizacion,
    Estado
FROM CAF
WHERE TD = 33;

-- ============================================================================
-- FIN DEL SCRIPT
-- ============================================================================
-- El CAF ha sido insertado correctamente con:
-- - Tipo DTE: 33 (Factura Electrónica)
-- - Rango: 62-90 (29 folios disponibles)
-- - Fecha: 2026-01-19
-- - Estado: Activo
-- ============================================================================
