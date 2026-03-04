# Verificar CAF en Base de Datos

## Problema

El error indica que el XML del CAF almacenado en la base de datos tiene problemas de formato:
```
The 'hr' start tag on line 22 position 8 does not match the end tag of 'td'. Line 23, position 7.
```

Este error sugiere que el XML del CAF podría tener contenido HTML o estar corrupto.

## Verificación

Ejecuta esta consulta SQL en tu base de datos MySQL para ver el contenido del CAF:

```sql
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    FechaCarga,
    Estado,
    LENGTH(CAFContenido) as LongitudXML,
    LEFT(CAFContenido, 500) as Primeros500Caracteres
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
ORDER BY FechaCarga DESC
LIMIT 1;
```

## Verificar que el XML sea válido

Si el XML parece válido, intenta parsearlo manualmente:

```sql
-- Ver el XML completo (cuidado: puede ser muy largo)
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

## Posibles Problemas

1. **XML corrupto**: El CAF podría tener caracteres especiales o estar mal formado
2. **Encoding incorrecto**: El XML podría tener problemas de codificación
3. **Contenido HTML**: El error menciona etiquetas HTML (`hr`, `td`), lo cual es muy extraño

## Solución Temporal

Si el XML está corrupto, necesitarás:
1. Obtener el CAF original desde el SII
2. Guardarlo correctamente en la base de datos
3. Asegurarte de que el encoding sea correcto (UTF-8 o ISO-8859-1)

## Logs del Servicio

Revisa los logs del servicio cuando intentes emitir un DTE. Ahora deberías ver:
- "XML del CAF validado correctamente"
- "OuterXml del nodo CAF validado correctamente"
- O el error exacto con línea y posición
