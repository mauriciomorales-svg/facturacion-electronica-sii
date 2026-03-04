# Solución para CAF Corrupto

## Problema Confirmado

El XML del CAF en la base de datos contiene etiquetas HTML (`<td>`, posiblemente `<hr>`, etc.), lo que corrompe el XML y causa errores de parseo.

## Soluciones

### Opción 1: Limpiar el CAF Automáticamente (Recomendado)

Ejecuta el script `LIMPIAR_CAF.sql` que extrae solo la parte XML válida del CAF.

**⚠️ IMPORTANTE: Haz un backup antes de ejecutar el UPDATE**

```sql
-- Backup primero
CREATE TABLE CAF_backup AS SELECT * FROM CAF WHERE TD = 33;

-- Luego ejecuta el script LIMPIAR_CAF.sql
```

### Opción 2: Extraer Manualmente el XML Válido

1. **Ver el contenido completo:**
```sql
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

2. **Identificar dónde comienza y termina el XML válido:**
   - Debe comenzar con: `<?xml version="1.0"?>` o `<?xml version="1.0" encoding="..."?>`
   - Debe terminar con: `</AUTORIZACION>` o `</CAF>`

3. **Extraer solo esa parte y actualizar:**
```sql
UPDATE CAF 
SET CAFContenido = 'AQUÍ_PEGA_SOLO_EL_XML_VÁLIDO'
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

### Opción 3: Obtener el CAF Original del SII

Si tienes acceso al CAF original desde el SII:

1. Descarga el CAF original (archivo XML)
2. Abre el archivo XML en un editor de texto
3. Copia TODO el contenido XML (desde `<?xml` hasta `</AUTORIZACION>`)
4. Actualiza la base de datos:

```sql
UPDATE CAF 
SET CAFContenido = 'AQUÍ_PEGA_EL_XML_COMPLETO_DEL_ARCHIVO'
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

## Verificación

Después de limpiar, verifica que el XML sea válido:

```sql
SELECT 
    ID,
    TD,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
             AND CAFContenido NOT LIKE '%<html%'
        THEN '✅ XML VÁLIDO'
        ELSE '❌ AÚN TIENE PROBLEMAS'
    END as Estado
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

## Estructura Esperada del CAF

El XML del CAF debe tener esta estructura:

```xml
<?xml version="1.0"?>
<AUTORIZACION>
  <CAF version="1.0">
    <DA>
      <RE>8451335-0</RE>
      <RS>MARTA INALBIA URRA ESCOBAR</RS>
      <TD>33</TD>
      <RNG>
        <D>1</D>
        <H>60</H>
      </RNG>
      <FA>2018-09-22</FA>
      <RSAPK>
        <M>...</M>
        <E>...</E>
      </RSAPK>
      <IDK>100</IDK>
    </DA>
    <FRMA algoritmo="SHA1withRSA">...</FRMA>
  </CAF>
  <RSASK>-----BEGIN RSA PRIVATE KEY-----...-----END RSA PRIVATE KEY-----</RSASK>
</AUTORIZACION>
```

## Después de Limpiar

Una vez que el CAF esté limpio:

1. Reinicia el servicio
2. Prueba la emisión de DTE nuevamente
3. El error de parseo XML debería desaparecer
