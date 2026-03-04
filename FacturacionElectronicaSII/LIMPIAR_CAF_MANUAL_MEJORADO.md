# Limpieza Manual del CAF - Guía Paso a Paso

## 📋 Objetivo

Limpiar el XML del CAF eliminando HTML corrupto que está causando errores de parseo.

---

## 🔍 Paso 1: Diagnosticar el Problema

Ejecuta el script `DIAGNOSTICO_CAF_MEJORADO.sql` en MySQL para ver:
- Dónde está el HTML corrupto
- Cuántas etiquetas HTML hay
- Posición del inicio y fin del XML válido

---

## 🛠️ Paso 2: Extraer el Contenido Completo

Ejecuta esta consulta y copia TODO el resultado:

```sql
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

**Importante**: Copia el resultado completo a un editor de texto (Notepad++, VS Code, etc.)

---

## ✂️ Paso 3: Limpiar Manualmente

### 3.1 Identificar el Bloque XML Válido

Busca en el texto copiado:

1. **Inicio del XML**: 
   - `<?xml version="1.0" encoding="ISO-8859-1"?>` O
   - `<?xml version="1.0"?>` O
   - `<AUTORIZACION>` O
   - `<CAF`

2. **Fin del XML**:
   - `</AUTORIZACION>`

### 3.2 Eliminar Todo lo que NO sea XML Válido

**Elimina**:
- ❌ Todo lo que esté ANTES de `<?xml` o `<AUTORIZACION>`
- ❌ Todo lo que esté DESPUÉS de `</AUTORIZACION>`
- ❌ Etiquetas HTML: `<html>`, `<head>`, `<body>`, `<table>`, `<tr>`, `<td>`, `<th>`, `<hr>`, `<div>`, `<span>`, `<p>`, `<br>`, `<a>`, `<img>`, `<font>`, `<style>`
- ❌ Texto junk como "Descargado desde SII", "Página 1 de 1", etc.

**Preserva**:
- ✅ `<?xml version="1.0"?>` (o con encoding)
- ✅ Todo el bloque desde `<AUTORIZACION>` hasta `</AUTORIZACION>`
- ✅ El contenido dentro de las etiquetas XML (números, texto, claves)

### 3.3 Ejemplo de Limpieza

**ANTES (corrupto)**:
```
<html><body><table><tr><td>
<?xml version="1.0"?>
<AUTORIZACION>
<CAF version="1.0">
...
</CAF>
</AUTORIZACION>
</td></tr></table>
</body></html>
```

**DESPUÉS (limpio)**:
```
<?xml version="1.0"?>
<AUTORIZACION>
<CAF version="1.0">
...
</CAF>
</AUTORIZACION>
```

---

## ✅ Paso 4: Validar el XML Limpio

### Opción A: Validador Online
1. Ve a https://www.xmlvalidation.com/
2. Pega tu XML limpio
3. Verifica que no haya errores

### Opción B: Validar con C# (código simple)
```csharp
var doc = new XmlDocument();
doc.LoadXml(tuXmlLimpio);  // Si no falla, está OK.
```

---

## 💾 Paso 5: Actualizar la Base de Datos

### Opción A: UPDATE Directo (si el XML es corto)

```sql
UPDATE CAF 
SET CAFContenido = 'AQUÍ_PEGA_EL_XML_LIMPIO_COMPLETO'
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

**⚠️ IMPORTANTE**: 
- Escapa comillas simples (`'`) duplicándolas (`''`)
- Si el XML tiene comillas dobles, no necesitas escaparlas en MySQL

### Opción B: Usar Variable (recomendado para XML largo)

```sql
SET @xml_limpio = 'AQUÍ_PEGA_EL_XML_LIMPIO_COMPLETO';

UPDATE CAF 
SET CAFContenido = @xml_limpio
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

### Opción C: MySQL Workbench
1. Abre MySQL Workbench
2. Conecta a `dbisabel2`
3. Ejecuta la consulta UPDATE
4. O usa la interfaz gráfica para editar el campo directamente

---

## 🔄 Paso 6: Verificar la Actualización

Ejecuta esta consulta para verificar:

```sql
SELECT 
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN '❌ AÚN TIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN '❌ AÚN TIENE <hr>'
        WHEN CAFContenido LIKE '%<?xml%' AND CAFContenido LIKE '%</AUTORIZACION>%' 
             AND CAFContenido NOT LIKE '%<td%' AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ XML VÁLIDO Y LIMPIO'
        ELSE '⚠️ REVISAR'
    END as Estado
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

Deberías ver: `✅ XML VÁLIDO Y LIMPIO`

---

## 🚀 Paso 7: Reiniciar el Servicio

1. Detén el servicio (Ctrl+C)
2. Reinicia: `dotnet run`
3. Prueba emitir un DTE

---

## 📝 Notas Importantes

1. **Encoding**: Asegúrate de que el XML quede en UTF-8 o ISO-8859-1 (como requiere SII)
2. **Preservar Contenido**: NO elimines el contenido dentro de las etiquetas XML, solo las etiquetas HTML
3. **Backup**: Considera hacer un backup de la tabla CAF antes de actualizar:
   ```sql
   CREATE TABLE CAF_backup AS SELECT * FROM CAF;
   ```

---

## 🆘 Si Algo Sale Mal

Si después de actualizar el servicio sigue fallando:

1. Verifica que el UPDATE se aplicó correctamente (Paso 6)
2. Revisa los logs del servicio para ver el error exacto
3. Compara el XML limpio con el ejemplo de XML válido en `INSERTAR_CAF_FINAL.sql`
4. Considera obtener un CAF nuevo desde el SII

---

**Última actualización**: 2026-01-19
