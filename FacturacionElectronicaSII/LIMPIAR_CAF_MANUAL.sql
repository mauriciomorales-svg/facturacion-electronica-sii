-- Script MANUAL para limpiar el CAF
-- Si el script automático no funciona, usa este método manual

-- PASO 1: Ver el contenido completo del CAF
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;

-- PASO 2: Identificar manualmente:
--   1. Dónde comienza el XML válido (busca "<?xml" o "<AUTORIZACION>")
--   2. Dónde termina el XML válido (busca "</AUTORIZACION>")

-- PASO 3: Copia SOLO el XML válido (desde <?xml hasta </AUTORIZACION>)
--    NO incluyas nada antes de <?xml
--    NO incluyas nada después de </AUTORIZACION>

-- PASO 4: Actualiza manualmente (reemplaza 'XML_VALIDO_AQUI' con el XML completo)
UPDATE CAF 
SET CAFContenido = 'XML_VALIDO_AQUI'
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';

-- Ejemplo de cómo debería verse el XML válido:
-- <?xml version="1.0"?>
-- <AUTORIZACION>
--   <CAF version="1.0">
--     <DA>...</DA>
--     <FRMA>...</FRMA>
--   </CAF>
--   <RSASK>-----BEGIN RSA PRIVATE KEY-----...-----END RSA PRIVATE KEY-----</RSASK>
-- </AUTORIZACION>
