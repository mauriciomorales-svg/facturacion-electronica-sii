# Instrucciones para Agregar CAF Nuevo Limpiamente

## Pasos

### 1. Descargar el CAF desde el SII

- Descarga el archivo XML del CAF desde el portal del SII
- Asegúrate de que sea el CAF correcto para tipo DTE 33 (Factura Electrónica)

### 2. Abrir el archivo XML

- Abre el archivo XML descargado en un editor de texto (Notepad++, Visual Studio Code, etc.)
- **Copia TODO el contenido** del archivo XML
- El XML debe comenzar con `<?xml version="1.0"?>` o similar
- El XML debe terminar con `</AUTORIZACION>`

### 3. Preparar el Script SQL

1. Abre el archivo `INSERTAR_CAF_LIMPIO.sql`
2. Busca la línea que dice: `'XML_DEL_CAF_AQUI'`
3. Reemplaza `'XML_DEL_CAF_AQUI'` con el XML completo que copiaste
4. **IMPORTANTE**: 
   - El XML debe estar entre comillas simples: `'...'`
   - Si el XML contiene comillas simples dentro, escápalas con `\'`
   - O mejor aún, usa comillas dobles si el XML las tiene

### 4. Ejecutar el Script

Ejecuta el script SQL modificado en tu cliente MySQL.

### 5. Verificar

El script mostrará una tabla de verificación. Debe mostrar:
- ✅ TIENE <?xml>
- ✅ TIENE </AUTORIZACION>
- ✅ XML VÁLIDO Y LIMPIO

## Ejemplo de Cómo Debería Verse el Script

```sql
UPDATE CAF 
SET 
    CAFContenido = '<?xml version="1.0"?>
<AUTORIZACION>
  <CAF version="1.0">
    <DA>
      <RE>8451335-0</RE>
      ...
    </DA>
    <FRMA algoritmo="SHA1withRSA">...</FRMA>
  </CAF>
  <RSASK>-----BEGIN RSA PRIVATE KEY-----...-----END RSA PRIVATE KEY-----</RSASK>
</AUTORIZACION>',
    FechaCarga = NOW(),
    Estado = 'Activo'
WHERE TD = 33;
```

## Nota sobre Comillas

Si el XML contiene comillas simples, puedes:
1. Escaparlas: `\'`
2. O usar comillas dobles en MySQL: `"XML_AQUI"` (pero mejor usar comillas simples y escapar)

## Después de Insertar

1. **Reinicia el servicio**
2. **Prueba la emisión de DTE**
3. **El error debería desaparecer**

## Alternativa: Script con Variable

Si prefieres, puedo crear un script que use una variable para el XML, lo cual es más fácil de manejar.
