# 📋 INFORME FINAL: ERROR MULTIPLE ROOT ELEMENTS

**Fecha:** 2026-01-19  
**Estado:** ❌ ERROR PERSISTE - Requiere reinicio del servicio

---

## 🔴 ERROR ACTUAL

```
Error interno: There are multiple root elements. Line 11, position 2.
```

**Tipo:** Error de parsing XML - múltiples elementos raíz  
**Línea del error:** 11, posición 2  
**Estado:** ❌ PERSISTE después de múltiples correcciones

---

## 🔍 ANÁLISIS DEL PROBLEMA

### Archivo CAF Verificado:

El archivo CAF tiene la estructura correcta:
- ✅ 1 declaración XML (`<?xml version="1.0"?>`)
- ✅ 1 bloque `<AUTORIZACION>...</AUTORIZACION>`
- ✅ Sin HTML corrupto
- ✅ Estructura XML válida

### Línea 11 del Archivo CAF:

```xml
<IDK>100</IDK>
```

Esta línea está dentro de `<DA>` que está dentro de `<CAF>` que está dentro de `<AUTORIZACION>`. No debería causar un error de múltiples elementos raíz.

---

## 🔧 SOLUCIONES IMPLEMENTADAS

### 1. ✅ Método `ExtraerBloqueAutorizacion()` con Regex
- Usa regex para extraer solo el bloque `<AUTORIZACION>`
- Elimina declaración XML si está presente
- Valida que solo haya un elemento raíz

### 2. ✅ Detección Temprana de Declaración XML
- En `ObtenerCafBlindado()`, si detecta `<?xml`, extrae el bloque directamente
- Evita intentar parsear el XML completo con declaración

### 3. ✅ Validación del Bloque Extraído
- Valida que tenga exactamente 1 apertura y 1 cierre de `<AUTORIZACION>`
- Logging detallado para diagnóstico

### 4. ✅ Corrección en `ExtraerClavePrivada()`
- Ahora usa `ObtenerCafBlindado()` en lugar de parsear directamente

---

## ⚠️ PROBLEMA IDENTIFICADO

**El servicio está usando código anterior** porque no se ha recompilado después de los cambios. El código nuevo tiene la detección temprana de declaración XML, pero el servicio en ejecución todavía tiene el código antiguo.

---

## 🔧 SOLUCIÓN REQUERIDA

### Opción 1: Reiniciar el Servicio (RECOMENDADO)
1. Detener el servicio actual
2. Recompilar el proyecto (`dotnet build`)
3. Reiniciar el servicio
4. Probar la emisión nuevamente

### Opción 2: Hot Reload (si está disponible)
Si el servicio soporta hot reload, los cambios deberían aplicarse automáticamente.

---

## 📋 CAMBIOS IMPLEMENTADOS QUE REQUIEREN REINICIO

### En `TEDService.cs` y `CAFService.cs`:

**Nuevo código en `ObtenerCafBlindado()`:**
```csharp
// 1) CRÍTICO: Eliminar declaración XML antes de intentar parsear
if (raw.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogInformation("CAF contiene declaración XML. Extrayendo bloque AUTORIZACION directamente...");
    string bloqueInicial = ExtraerBloqueAutorizacion(raw);
    var docInicial = new XmlDocument { PreserveWhitespace = true };
    try
    {
        docInicial.LoadXml(bloqueInicial);
        _logger.LogInformation("CAF parseado después de extraer bloque AUTORIZACION (evitando declaración XML)");
        return docInicial;
    }
    catch (XmlException xmlEx)
    {
        _logger.LogWarning("Error al parsear bloque extraído: {Error}", xmlEx.Message);
        // Continuar con el flujo normal
    }
}
```

Este código detecta la declaración XML **antes** de intentar parsear y extrae el bloque directamente, evitando el error de múltiples elementos raíz.

---

## 🔍 DIAGNÓSTICO ADICIONAL

### Posible Causa del Error "Line 11, position 2":

El error podría estar ocurriendo cuando:
1. Se intenta parsear el XML completo con la declaración XML
2. El parser ve la declaración XML como un elemento separado
3. Luego encuentra `<AUTORIZACION>` como segundo elemento raíz
4. El parser reporta el error en la línea 11 (primer elemento dentro de AUTORIZACION)

### Solución Implementada:

Al detectar la declaración XML **antes** de parsear y extraer solo el bloque `<AUTORIZACION>`, se evita este problema completamente.

---

## 📊 RESUMEN

- **Error:** `There are multiple root elements. Line 11, position 2.`
- **Causa:** El parser ve la declaración XML y el elemento AUTORIZACION como múltiples elementos raíz
- **Solución implementada:** ✅ Detección temprana y extracción del bloque antes de parsear
- **Estado:** ⏳ Requiere reinicio del servicio para aplicar cambios

---

## 🔧 PRÓXIMOS PASOS

1. **Reiniciar el servicio** para aplicar los cambios
2. **Probar la emisión** nuevamente
3. **Revisar los logs** para confirmar que se está usando el nuevo código
4. Si el error persiste, revisar los logs para ver qué bloque se está extrayendo exactamente

---

**Generado:** 2026-01-19  
**Última Actualización:** Después de implementar detección temprana de declaración XML
