# Solicitud de Ayuda - XML del CAF Corrupto con HTML

## 🔴 Problema Crítico

El servicio de facturación electrónica falla al intentar emitir DTEs debido a que el XML del CAF almacenado en la base de datos MySQL contiene **HTML corrupto mezclado con el XML válido**.

### Error Exacto

```
Error interno: The 'hr' start tag on line 22 position 8 does not match the end tag of 'td'. Line 23, position 7.
```

Este error ocurre cuando `XmlDocument.LoadXml()` intenta parsear el XML del CAF y encuentra etiquetas HTML (`<hr>`, `<td>`) que no deberían estar presentes.

---

## 📊 Contexto del Sistema

- **Base de datos**: MySQL `dbisabel2`
- **Tabla**: `CAF`
- **Campo**: `CAFContenido` (Tipo: TEXT o LONGTEXT)
- **Filtro**: `TD = 33` y `Estado = 'Activo'`
- **Servicio**: ASP.NET Core Web API (.NET 8.0)
- **Ubicación**: `C:\Users\ComercioIsabel\source\repos\FacturacionElectronicaSII`

---

## 🔄 Flujo del Proceso y Dónde Falla

1. **Cliente hace petición** → `POST /api/DTE/emitir`
2. **DTEService** obtiene folio disponible
3. **CAFService.ObtenerCAFAsync** lee el CAF desde MySQL
4. **CAFService.SanitizeCafXml** intenta limpiar el XML (❌ **NO FUNCIONA COMPLETAMENTE**)
5. **CAFService** intenta validar el XML parseándolo → **FALLA AQUÍ**
6. El error se propaga y se retorna al cliente

---

## 🛠️ Soluciones Intentadas

### 1. Limpieza Automática en Código (Implementada)

Se implementó la función `SanitizeCafXml` en `CAFService.cs` y `TEDService.cs` que:

- ✅ Decodifica entidades HTML (`WebUtility.HtmlDecode`)
- ✅ Busca el inicio del XML válido (`<?xml`, `<AUTORIZACION>`, `<CAF>`)
- ✅ Busca el final del XML válido (`</AUTORIZACION>`)
- ✅ Extrae el bloque XML usando índices de string
- ✅ Elimina etiquetas HTML con Regex:
  - `<td>`, `</td>`, `<hr>`, `<table>`, `<tr>`, `<html>`, `<body>`, `<div>`, `<br>`, `<p>`
- ✅ Limpia espacios múltiples
- ✅ Valida el XML antes de retornarlo

**Resultado**: ❌ El error persiste. El HTML corrupto aún está presente cuando se intenta parsear.

### 2. Scripts SQL de Limpieza (Intentados)

Se crearon múltiples scripts SQL:
- `LIMPIAR_CAF_DIRECTO.sql`
- `UPDATE_CAF_DIRECTO.sql`
- `INSERTAR_CAF_FINAL.sql`

**Resultado**: ❌ El usuario ejecutó los scripts pero el error persiste, sugiriendo que:
- Los scripts no se ejecutaron correctamente, O
- El UPDATE no afectó el registro correcto, O
- El HTML corrupto está en un formato que los scripts no detectan

---

## 📝 Código Actual de Sanitización

### Ubicación
- `FacturacionElectronicaSII/Services/CAFService.cs` (línea ~284)
- `FacturacionElectronicaSII/Services/TEDService.cs` (línea ~462)

### Función `SanitizeCafXml`

```csharp
private string SanitizeCafXml(string rawXml)
{
    // PASO 1: Decodificar entidades HTML
    string decoded = WebUtility.HtmlDecode(rawXml);
    
    // PASO 2: Buscar inicio del XML válido
    int inicioXML = decoded.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
    if (inicioXML < 0)
    {
        inicioXML = decoded.IndexOf("<AUTORIZACION>", StringComparison.OrdinalIgnoreCase);
        if (inicioXML < 0)
        {
            inicioXML = decoded.IndexOf("<CAF", StringComparison.OrdinalIgnoreCase);
        }
    }
    
    // Extraer desde el inicio
    decoded = decoded.Substring(inicioXML);
    
    // PASO 3: Buscar final del XML válido
    int finAUTORIZACION = decoded.LastIndexOf("</AUTORIZACION>", StringComparison.OrdinalIgnoreCase);
    
    // Extraer bloque XML
    finAUTORIZACION += "</AUTORIZACION>".Length;
    string xmlLimpio = decoded.Substring(0, finAUTORIZACION).Trim();
    
    // PASO 3.5: ELIMINAR HTML CORRUPTO
    xmlLimpio = Regex.Replace(xmlLimpio, @"<td[^>]*>.*?</td>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    xmlLimpio = Regex.Replace(xmlLimpio, @"<hr[^>]*/?>", "", RegexOptions.IgnoreCase);
    // ... más eliminaciones de HTML ...
    
    // Limpieza agresiva por líneas si aún queda HTML
    if (xmlLimpio.Contains("<td", StringComparison.OrdinalIgnoreCase) || 
        xmlLimpio.Contains("<hr", StringComparison.OrdinalIgnoreCase))
    {
        var lineas = xmlLimpio.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var lineasLimpias = lineas.Where(line => 
            !line.Contains("<td", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("</td>", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("<hr", StringComparison.OrdinalIgnoreCase)
        ).ToArray();
        xmlLimpio = string.Join("\n", lineasLimpias);
    }
    
    // Validar XML
    var testDoc = new XmlDocument();
    testDoc.PreserveWhitespace = true;
    testDoc.LoadXml(xmlLimpio); // ❌ FALLA AQUÍ
    
    return xmlLimpio;
}
```

---

## 🔍 Diagnóstico Necesario

### 1. Verificar el Contenido Real del CAF en la Base de Datos

Ejecutar esta consulta SQL para ver el contenido exacto:

```sql
SELECT 
    ID,
    TD,
    Estado,
    LENGTH(CAFContenido) as Longitud,
    LEFT(CAFContenido, 1000) as Primeros1000Caracteres,
    CASE 
        WHEN CAFContenido LIKE '%<td%' THEN 'CONTIENE <td>'
        WHEN CAFContenido LIKE '%<hr%' THEN 'CONTIENE <hr>'
        WHEN CAFContenido LIKE '%<?xml%' AND CAFContenido LIKE '%</AUTORIZACION>%' 
        THEN 'XML VÁLIDO'
        ELSE 'REVISAR'
    END as EstadoXML
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

### 2. Ver el XML Completo del CAF

```sql
SELECT CAFContenido 
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

**Importante**: Copiar el resultado completo y verificar:
- ¿Dónde está el HTML corrupto? (antes, después, o dentro del bloque XML)
- ¿Qué formato tiene el HTML? (¿está en una sola línea, múltiples líneas, con atributos?)
- ¿Hay caracteres especiales o encoding incorrecto?

### 3. Revisar los Logs del Servicio

Cuando se intenta emitir un DTE, los logs deberían mostrar:

```
[Information] XML del CAF leído de BD, longitud: XXXX caracteres
[Warning] El XML del CAF contiene HTML corrupto. Sanitizando...
[Information] Inicio del XML encontrado en posición: XX
[Information] XML extraído y limpiado de HTML, longitud: XXXX caracteres
[Error] El XML sanitizado aún no es válido. Error en línea 22, posición 8
```

**Ubicación de logs**: Consola donde se ejecuta `dotnet run`

---

## 💡 Posibles Soluciones Alternativas

### Opción 1: Limpieza Manual Directa en Base de Datos

1. **Extraer el XML válido manualmente**:
   - Abrir el resultado de la consulta SQL
   - Identificar dónde empieza `<?xml` o `<AUTORIZACION>`
   - Identificar dónde termina `</AUTORIZACION>`
   - Copiar SOLO esa parte

2. **Actualizar directamente**:
   ```sql
   UPDATE CAF 
   SET CAFContenido = 'XML_VALIDO_AQUI'
   WHERE TD = 33 
       AND TRIM(UPPER(Estado)) = 'ACTIVO';
   ```

### Opción 2: Mejorar la Sanitización para Manejar Casos Específicos

Si el HTML corrupto tiene un formato específico que no estamos detectando, necesitamos:

1. **Ver el XML corrupto completo** para entender su estructura
2. **Ajustar los patrones Regex** para ese formato específico
3. **Agregar validación más temprana** antes de intentar parsear

### Opción 3: Obtener un CAF Nuevo desde el SII

1. Descargar un CAF nuevo desde el portal del SII
2. Guardarlo directamente en la base de datos sin pasar por ningún proceso que pueda corromperlo
3. Verificar que el encoding sea correcto (UTF-8 o ISO-8859-1)

---

## 📋 Información para Pedir Ayuda

### Preguntas Clave a Responder

1. **¿Cuál es el contenido exacto del campo `CAFContenido` en la base de datos?**
   - Ejecutar: `SELECT CAFContenido FROM CAF WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO' LIMIT 1;`
   - Compartir los primeros 2000 caracteres y los últimos 500 caracteres

2. **¿Dónde está el HTML corrupto?**
   - ¿Antes del XML válido?
   - ¿Después del XML válido?
   - ¿Dentro del bloque `<AUTORIZACION>...</AUTORIZACION>`?

3. **¿Qué formato tiene el HTML corrupto?**
   - ¿Está en una sola línea?
   - ¿Tiene atributos?
   - ¿Está mezclado con el contenido XML?

4. **¿Cómo se insertó el CAF originalmente?**
   - ¿Se descargó desde el SII?
   - ¿Se copió desde otro sistema?
   - ¿Pasó por algún proceso de transformación?

### Archivos Relevantes para Compartir

1. **Código de sanitización**:
   - `FacturacionElectronicaSII/Services/CAFService.cs` (función `SanitizeCafXml`)
   - `FacturacionElectronicaSII/Services/TEDService.cs` (función `SanitizeCafXml`)

2. **Logs del servicio** cuando se intenta emitir un DTE

3. **Resultado de la consulta SQL** del contenido del CAF

---

## 🎯 Objetivo Final

Eliminar completamente el HTML corrupto del XML del CAF antes de que se intente parsear, de manera que:

1. ✅ El XML sea válido según el parser XML de .NET
2. ✅ Se pueda extraer el nodo `<CAF>` correctamente
3. ✅ Se pueda generar el TED sin errores
4. ✅ Se pueda emitir el DTE exitosamente

---

## 📞 Próximos Pasos Recomendados

1. **Ejecutar la consulta SQL** para ver el contenido exacto del CAF
2. **Compartir el resultado** (primeros 2000 y últimos 500 caracteres)
3. **Revisar los logs** del servicio cuando falla
4. **Intentar una de las soluciones alternativas** mencionadas arriba
5. **Si nada funciona**, considerar obtener un CAF nuevo desde el SII

---

**Última actualización**: 2026-01-19
**Estado**: Problema persistente - Se requiere análisis del contenido exacto del CAF en la base de datos
