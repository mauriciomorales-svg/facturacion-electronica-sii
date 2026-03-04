# Solución Manual para Limpiar el CAF

## Problema Confirmado

El script automático no funcionó. El CAF aún contiene etiquetas HTML (`<td>`). Necesitamos limpiarlo manualmente.

## Solución Paso a Paso

### Opción 1: Usar el Script Robusto

Ejecuta el script `LIMPIAR_CAF_ROBUSTO.sql` que es más robusto y maneja diferentes casos.

### Opción 2: Limpieza Manual Completa (Recomendado si el script no funciona)

1. **Ver el contenido completo del CAF:**
```sql
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

2. **En el resultado, busca:**
   - El inicio del XML válido: `<?xml version="1.0"?>` o `<?xml version="1.0" encoding="..."?>`
   - El final del XML válido: `</AUTORIZACION>`

3. **Copia SOLO la parte entre esos dos puntos:**
   - Desde `<?xml` (incluido)
   - Hasta `</AUTORIZACION>` (incluido)
   - **NO copies nada antes de `<?xml`**
   - **NO copies nada después de `</AUTORIZACION>`**

4. **Actualiza la base de datos:**
```sql
UPDATE CAF 
SET CAFContenido = 'AQUÍ_PEGA_SOLO_EL_XML_VÁLIDO_COMPLETO'
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

5. **Verifica que se limpió:**
```sql
SELECT 
    ID,
    TD,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ LIMPIO'
        ELSE '❌ AÚN TIENE PROBLEMAS'
    END as Estado
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

## Estructura Esperada del XML Válido

El XML del CAF debe verse así:

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

1. **Reinicia el servicio** (detén y vuelve a iniciar)
2. **Prueba la emisión de DTE**
3. **El error debería desaparecer**
