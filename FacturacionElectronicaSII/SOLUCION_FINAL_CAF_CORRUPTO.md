# Solución Final - CAF Corrupto con Múltiples Elementos Raíz

## 🔴 Problema Actual

El error ha evolucionado de:
- ❌ `The 'hr' start tag on line 22 position 8 does not match the end tag of 'td'` 
- ✅ **RESUELTO**: La sanitización ahora elimina tags HTML correctamente

A:
- ❌ `There are multiple root elements. Line 1, position 432`
- ⚠️ **PENDIENTE**: El CAF en la base de datos tiene múltiples bloques XML completos mezclados

---

## 📊 Diagnóstico

El XML del CAF en la base de datos contiene:
- ✅ Tags HTML eliminados correctamente por la sanitización
- ❌ **Múltiples bloques XML completos** (múltiples `<?xml>` o múltiples `<AUTORIZACION>`)

Esto indica que el CAF fue copiado/pegado incorrectamente o tiene contenido duplicado.

---

## ✅ Solución Recomendada: Limpieza Manual

Dado que el CAF tiene múltiples bloques XML completos, la **mejor solución es limpiarlo manualmente** en la base de datos.

### Paso 1: Ver el Contenido Completo

Ejecuta en MySQL:

```sql
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

### Paso 2: Identificar el Bloque XML Válido

En el resultado, busca:
1. **El PRIMER** `<?xml version="1.0"?>` o `<AUTORIZACION>`
2. **El PRIMER** `</AUTORIZACION>` que cierra ese bloque
3. **Copia SOLO ese bloque** (desde `<?xml` hasta `</AUTORIZACION>`)

### Paso 3: Validar el XML Limpio

Pega el XML en un validador online (https://www.xmlvalidation.com/) para asegurarte de que es válido.

### Paso 4: Actualizar la Base de Datos

```sql
UPDATE CAF 
SET CAFContenido = 'XML_LIMPIO_AQUI'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

**⚠️ IMPORTANTE**: 
- Escapa comillas simples (`'`) duplicándolas (`''`)
- Asegúrate de que el XML tenga SOLO UN elemento raíz (`<AUTORIZACION>`)

### Paso 5: Verificar

```sql
SELECT 
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND (LENGTH(CAFContenido) - LENGTH(REPLACE(CAFContenido, '<?xml', ''))) / 5 = 1
             AND (LENGTH(CAFContenido) - LENGTH(REPLACE(CAFContenido, '<AUTORIZACION', ''))) / 13 = 1
        THEN '✅ XML VÁLIDO CON UN SOLO ELEMENTO RAÍZ'
        ELSE '❌ REVISAR - Múltiples elementos raíz detectados'
    END as Estado
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

### Paso 6: Reiniciar y Probar

1. Reinicia el servicio: `dotnet run`
2. Prueba emitir un DTE

---

## 🔄 Alternativa: Obtener CAF Nuevo desde el SII

Si la limpieza manual es complicada, obtén un CAF nuevo:

1. Ve al portal SII (www.sii.cl)
2. Inicia sesión con Clave Tributaria
3. Solicita un nuevo CAF para TD=33
4. **Descarga el archivo XML directamente** (NO copies de la página web)
5. Inserta en la BD:

```sql
UPDATE CAF 
SET CAFContenido = 'NUEVO_XML_AQUI'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

---

## 📝 Estado del Código

El código de sanitización está **mejorado y funcionando**:
- ✅ Elimina tags HTML correctamente
- ✅ Extrae el bloque XML válido
- ✅ Detecta múltiples elementos raíz
- ⚠️ **Pero no puede resolver automáticamente** si hay múltiples bloques XML completos mezclados

**Recomendación**: Limpia el CAF manualmente en la base de datos siguiendo los pasos arriba.

---

## 🆘 Si Necesitas Más Ayuda

Comparte:
1. Los primeros 500 caracteres del `CAFContenido` (sin claves privadas)
2. Los últimos 500 caracteres del `CAFContenido`
3. El resultado de la consulta de verificación (Paso 5)

Con esa información puedo ayudarte a identificar exactamente dónde está el problema.

---

**Última actualización**: 2026-01-19
**Estado**: Código mejorado, requiere limpieza manual del CAF en BD
