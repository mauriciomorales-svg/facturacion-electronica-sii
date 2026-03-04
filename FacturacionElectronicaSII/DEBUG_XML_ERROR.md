# Debug del Error de Parseo XML

## Error Actual

```
The 'hr' start tag on line 22 position 8 does not match the end tag of 'td'. Line 23, position 7.
```

Este error es muy extraño porque menciona etiquetas HTML (`hr` y `td`) que no deberían estar en un XML del CAF.

## Posibles Causas

1. **XML del CAF corrupto en la base de datos**: El campo `CAFContenido` podría tener contenido HTML o XML mal formado.

2. **Problema al insertar el CAF en el DD**: Cuando se hace `{cafNode.OuterXml}` dentro del string del DD, podría haber problemas de encoding o caracteres especiales.

3. **Problema con el encoding**: El XML podría tener problemas de encoding (ISO-8859-1 vs UTF-8).

## Cómo Diagnosticar

1. **Revisar los logs** cuando ejecutes el servicio. Deberías ver:
   - "XML del CAF validado correctamente"
   - "Nodo DD completo validado correctamente"
   - "TED validado correctamente antes de insertar"

2. **Si falla en alguna validación**, el log mostrará:
   - El error exacto con línea y posición
   - Los primeros 1000 caracteres del XML problemático

3. **Verificar el XML del CAF en la base de datos**:
   ```sql
   SELECT CAFContenido 
   FROM CAF 
   WHERE TD = 33 AND Estado = 'Activo'
   ```

4. **Verificar que el XML del CAF sea válido** ejecutando:
   ```sql
   SELECT CAFContenido 
   FROM CAF 
   WHERE TD = 33 
   LIMIT 1
   ```
   Y luego intentar parsearlo manualmente.

## Solución Temporal

Si el XML del CAF tiene problemas, puedes:
1. Extraer el XML del CAF desde la base de datos
2. Validarlo manualmente con un parser XML
3. Corregir cualquier problema de formato
4. Actualizar el registro en la base de datos

## Próximos Pasos

1. Reinicia el servicio
2. Intenta emitir un DTE
3. Revisa los logs detallados que ahora se generan
4. Comparte el error exacto con línea y posición para diagnosticar mejor
