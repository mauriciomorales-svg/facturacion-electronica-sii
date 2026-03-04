# 📚 Documentación Completa - Sistema DTE SII Chile

## 🎯 Resumen Ejecutivo

Esta documentación contiene **TODO** lo que necesitas saber para implementar correctamente un sistema de Facturación Electrónica que sea aceptado por el SII de Chile.

Basado en código real, probado y funcional, con todas las lecciones aprendidas y mejores prácticas.

---

## 📂 Archivos de Documentación

### 1. **GUIA_COMPLETA_DTE_SII_CHILE.md** ⭐
**El archivo más importante - Empieza aquí**

Contiene:
- ✅ Principios fundamentales (PreserveWhitespace, Encoding, etc.)
- ✅ Estructura completa del proceso paso a paso
- ✅ Reglas críticas del XML
- ✅ Generación del TED (Timbre Electrónico)
- ✅ Firma digital del DTE
- ✅ Creación del EnvioDTE
- ✅ Firma del EnvioDTE
- ✅ Validaciones pre-envío
- ✅ Errores comunes y soluciones
- ✅ Código de referencia completo

**Cuándo usarlo:** Como referencia principal y guía de implementación.

---

### 2. **EJEMPLOS_CODIGO_COMPLETOS.md**
**Código funcional listo para copiar y pegar**

Contiene:
- ✅ FuncionesComunes.cs completa
- ✅ UploaderSII.cs completa
- ✅ EnvioDTENormalizer.cs completa
- ✅ Todas las funciones auxiliares necesarias
- ✅ Ejemplos comentados y explicados
- ✅ Manejo de errores robusto

**Cuándo usarlo:** Para implementar las clases faltantes en tu proyecto.

---

### 3. **DIAGRAMAS_Y_FLUJOS.md**
**Visualización del proceso completo**

Contiene:
- ✅ Diagrama de flujo completo (ASCII art)
- ✅ Arquitectura de clases
- ✅ Flujo de firmas digitales
- ✅ Puntos críticos de fallo
- ✅ Checklist de validación
- ✅ Métricas de éxito
- ✅ Orden de depuración
- ✅ Plan de implementación de 10 días

**Cuándo usarlo:** Para entender el panorama general y planificar tu implementación.

---

## 🚀 Guía de Inicio Rápido

### Para Cursor / LLM

Si vas a usar Cursor o cualquier LLM para ayudarte con el código, dale este prompt:

```
Necesito implementar un sistema de facturación electrónica para el SII de Chile.
Tengo 3 archivos de documentación que explican TODO el proceso:

1. GUIA_COMPLETA_DTE_SII_CHILE.md - Guía principal
2. EJEMPLOS_CODIGO_COMPLETOS.md - Código de referencia
3. DIAGRAMAS_Y_FLUJOS.md - Diagramas y flujos

Las reglas MÁS IMPORTANTES son:
- PreserveWhitespace = true SIEMPRE
- Encoding ISO-8859-1 sin BOM
- Formatear ANTES de firmar
- Signature como HERMANA de Documento en DTE
- Limpiar nodo DD antes de firmar el TED
- SetDTE ID="SetDoc" en EnvioDTE

Por favor ayúdame a [describe tu tarea específica]
```

### Para Desarrollo Manual

1. **Día 1:** Lee GUIA_COMPLETA_DTE_SII_CHILE.md completa
2. **Día 2:** Estudia DIAGRAMAS_Y_FLUJOS.md para entender el flujo
3. **Día 3+:** Implementa usando EJEMPLOS_CODIGO_COMPLETOS.md como referencia

---

## 🎓 Conceptos Clave que DEBES Entender

### 1. PreserveWhitespace = true
```csharp
XmlDocument doc = new XmlDocument();
doc.PreserveWhitespace = true;  // ⚠️ CRÍTICO para firmas digitales
```
**Por qué:** La firma digital se calcula sobre cada byte del documento. Si .NET elimina espacios, la firma se invalida.

### 2. Encoding ISO-8859-1 sin BOM
```csharp
Encoding iso88591 = Encoding.GetEncoding("ISO-8859-1");
byte[] bytes = iso88591.GetBytes(xmlString);
File.WriteAllBytes(path, bytes);  // ISO-8859-1 NO tiene BOM
```
**Por qué:** El SII rechaza archivos con BOM. UTF-8 agrega BOM, ISO-8859-1 no.

### 3. Formatear ANTES de Firmar
```csharp
// ✅ CORRECTO
string xmlFormateado = GenerarXMLConFormato();
string xmlFirmado = FirmarDocumento(xmlFormateado);

// ❌ INCORRECTO
string xmlFirmado = FirmarDocumento(xml);
xmlFirmado = xmlFirmado.Replace(...);  // Esto ROMPE la firma
```
**Por qué:** Modificar el XML después de firmarlo invalida la firma.

### 4. Estructura Correcta de Signature
```xml
<!-- ✅ CORRECTO -->
<DTE>
  <Documento ID="F1T33">...</Documento>
  <Signature>...</Signature>  <!-- Hermana de Documento -->
</DTE>

<!-- ❌ INCORRECTO -->
<DTE>
  <Documento ID="F1T33">
    <Signature>...</Signature>  <!-- Hija de Documento -->
  </Documento>
</DTE>
```
**Por qué:** El esquema XSD del SII exige esta estructura.

### 5. Limpieza del Nodo DD
```csharp
string ddLimpio = nodoDD
    .Replace("\r\n", "")
    .Replace("\n", "")
    .Replace("  ", "")
    .Replace("> <", "><")
    .Trim();
```
**Por qué:** El TED se firma sobre el DD EXACTO sin espacios.

---

## 🛠️ Herramientas Recomendadas

### Software Necesario
- ✅ Visual Studio 2019+ o VS Code
- ✅ .NET Framework 4.7.2+
- ✅ Certificado digital instalado en Windows
- ✅ Archivos XSD del SII (descarga de sii.cl)

### Archivos XSD Requeridos
Descargar de: https://www.sii.cl/factura_electronica/

- `DTE_v10.xsd`
- `EnvioDTE_v10.xsd`
- `SiiTypes_v10.xsd`
- `xmldsignature_v10.xsd`

Ubicación recomendada: `C:\SchemasSII\`

### Para Pruebas
- Ambiente de Certificación: https://maullin.sii.cl
- CAF de prueba (solicitar en SII)
- Certificado digital de prueba

---

## 📋 Checklist de Validación Rápida

Antes de enviar CUALQUIER DTE al SII:

```
☐ Encoding ISO-8859-1 (sin BOM)
☐ PreserveWhitespace = true usado
☐ Valida contra XSD sin errores
☐ Signature en ubicación correcta
☐ TED incluido y válido
☐ Firmas verifican correctamente
☐ Ninguna línea > 4090 caracteres
☐ SetDTE ID="SetDoc"
☐ schemaLocation correcto
☐ Token válido obtenido
```

---

## 🚨 Los 10 Errores Más Comunes

1. **PreserveWhitespace = false** → Firma inválida
2. **UTF-8 con BOM** → CHR-00001 o rechazo
3. **Modificar XML después de firmar** → Firma inválida
4. **Signature dentro de Documento** → SCH-00001
5. **Espacios en DD del TED** → TED inválido
6. **Líneas > 4090 caracteres** → CHR-00002
7. **schemaLocation incorrecto** → SCH-00001
8. **SetDTE sin ID="SetDoc"** → Firma falla
9. **Timestamp sin zona horaria Chile** → Desfase temporal
10. **Canonicalización incorrecta** → DigestValue no coincide

---

## 📞 Recursos Adicionales

### Documentación Oficial SII
- Portal SII: https://www.sii.cl
- Factura Electrónica: https://www.sii.cl/servicios_online/1039-.html
- Documentación Técnica: https://www.sii.cl/factura_electronica/

### URLs del Sistema

**Certificación (Maullin):**
- Semilla: `https://maullin.sii.cl/DTEWS/CrSeed.jws`
- Token: `https://maullin.sii.cl/DTEWS/GetTokenFromSeed.jws`
- Upload: `https://maullin.sii.cl/cgi_dte/UPL/DTEUpload`

**Producción (Palena):**
- Semilla: `https://palena.sii.cl/DTEWS/CrSeed.jws`
- Token: `https://palena.sii.cl/DTEWS/GetTokenFromSeed.jws`
- Upload: `https://palena.sii.cl/cgi_dte/UPL/DTEUpload`

---

## 💡 Tips Pro

### 1. Logging Exhaustivo
```csharp
// Guarda TODOS los XMLs generados para debugging
File.WriteAllText(@"C:\FacturasElectronicas\DTE.xml", dteXml);
File.WriteAllText(@"C:\FacturasElectronicas\TED.xml", tedXml);
File.WriteAllText(@"C:\FacturasElectronicas\DTE_Firmado.xml", dteFirmado);
File.WriteAllText(@"C:\FacturasElectronicas\EnvioDTE.xml", envioXml);
File.WriteAllText(@"C:\FacturasElectronicas\EnvioDTE_Firmado.xml", envioFirmado);
```

### 2. Validación Progresiva
```csharp
// Valida en cada paso, no al final
var errores1 = ValidarDTE(dteXml);
var errores2 = ValidarTED(tedXml);
var errores3 = ValidarDTEFirmado(dteFirmado);
var errores4 = ValidarEnvioDTE(envioXml);
var errores5 = ValidarEnvioDTEFirmado(envioFirmado);
```

### 3. Comparación con Ejemplos Válidos
```csharp
// Compara tu XML con uno que YA funcionó
string xmlValido = File.ReadAllText("DTE_que_funciono.xml");
string xmlNuevo = File.ReadAllText("DTE_nuevo.xml");
// Usa un diff tool para comparar
```

### 4. Pruebas Incrementales
```csharp
// Empieza con lo más simple posible
// 1. DTE con 1 detalle, montos mínimos
// 2. Agregar más detalles
// 3. Agregar descuentos/recargos
// 4. Casos especiales
```

---

## 🎯 Métricas de Éxito

Sabrás que todo está funcionando cuando:

✅ Validación XSD pasa sin errores
✅ Todas las firmas digitales verifican correctamente
✅ SII retorna: "DOCUMENTO TRIBUTARIO ELECTRONICO RECIBIDO"
✅ Recibes Track ID en la respuesta HTML
✅ NO hay errores CHR-XXXXX
✅ NO hay errores SCH-XXXXX
✅ Recibes email de aceptación del SII
✅ El DTE aparece en el portal del SII

---

## 📚 Estructura de Carpetas Recomendada

```
C:\
├── SchemasSII\                          # Esquemas XSD
│   ├── DTE_v10.xsd
│   ├── EnvioDTE_v10.xsd
│   ├── SiiTypes_v10.xsd
│   └── xmldsignature_v10.xsd
│
└── FacturasElectronicas\
    ├── CAF\                             # Códigos de Autorización
    │   └── CAF_33_1-60.xml
    │
    ├── DTE\                             # DTEs generados
    │   ├── DTE.xml
    │   ├── TED.xml
    │   └── DTE_Firmado.xml
    │
    ├── EnvioDTE\                        # Sobres generados
    │   ├── EnvioDTE.xml
    │   └── EnvioDTE_Firmado.xml
    │
    ├── Logs\                            # Logs de depuración
    │   ├── depuracion_log.txt
    │   ├── ValidacionDTE.log
    │   └── ValidacionEnvioDTE.log
    │
    ├── Respuestas\                      # Respuestas del SII
    │   └── respuesta_sii.html
    │
    └── Certificados\                    # Certificados y claves
        └── certificado.pfx
```

---

## 🔄 Flujo Completo en Pseudocódigo

```
FUNCIÓN GenerarYEnviarDTE(datosFactura):
    
    // 1. AUTENTICACIÓN
    token = ObtenerTokenSII(certificado)
    
    // 2. GENERAR DTE
    dteXml = GenerarDTE(datosFactura)  // Con formato
    ValidarContraXSD(dteXml, "DTE_v10.xsd")
    
    // 3. GENERAR TED
    dd = GenerarNodoDD(datosFactura, caf)
    ddLimpio = LimpiarNodoDD(dd)  // Sin espacios
    firmaTED = FirmarConClaveCAF(ddLimpio, caf)
    tedXml = GenerarTEDCompleto(ddLimpio, firmaTED)
    
    // 4. INSERTAR TED EN DTE
    dteConTED = InsertarTED(dteXml, tedXml)
    
    // 5. FIRMAR DTE
    dteFirmado = FirmarDTE(dteConTED, certificado)
    ValidarFirma(dteFirmado, certificado)
    
    // 6. CREAR ENVIODTE
    envioXml = GenerarEnvioDTE(dteFirmado, datosSobre)
    ValidarContraXSD(envioXml, "EnvioDTE_v10.xsd")
    
    // 7. FIRMAR ENVIODTE
    envioFirmado = FirmarEnvioDTE(envioXml, certificado)
    ValidarFirma(envioFirmado, certificado)
    
    // 8. NORMALIZAR
    envioNormalizado = Normalizar(envioFirmado)
    
    // 9. VALIDACIÓN FINAL
    errores = ValidacionCompleta(envioNormalizado)
    SI errores.Count > 0:
        RETORNAR errores
    
    // 10. ENVIAR
    respuesta = EnviarAlSII(envioNormalizado, token)
    trackID = ExtraerTrackID(respuesta)
    
    RETORNAR { exito: true, trackID: trackID }
```

---

## 🎓 Resumen de Lecciones Aprendidas

### Lo que SÍ funciona ✅
1. Generar XML con formato desde el inicio
2. Usar PreserveWhitespace = true siempre
3. ISO-8859-1 sin BOM
4. Signature como hermana de Documento
5. Limpiar DD completamente antes de firmar
6. C14N Inclusivo para canonicalización
7. Validar contra XSD en cada paso
8. Logging exhaustivo de todo el proceso
9. Zona horaria de Chile para timestamps
10. Probar en Maullin antes de producción

### Lo que NO funciona ❌
1. Modificar XML después de firmar
2. UTF-8 con BOM
3. PreserveWhitespace = false
4. Signature dentro de Documento
5. Espacios en el nodo DD
6. Canonicalización Exclusive
7. Namespaces incorrectos
8. SetDTE sin ID
9. Timestamps sin zona horaria
10. Enviar directo a producción sin probar

---

## 🚀 ¡Estás Listo!

Con esta documentación tienes TODO lo necesario para implementar un sistema de facturación electrónica que funcione correctamente con el SII de Chile.

**Recuerda los 3 archivos principales:**
1. `GUIA_COMPLETA_DTE_SII_CHILE.md` - Tu biblia técnica
2. `EJEMPLOS_CODIGO_COMPLETOS.md` - Código funcional
3. `DIAGRAMAS_Y_FLUJOS.md` - Visualización y planificación

**Siguiente paso:** Empieza leyendo la guía completa, luego implementa paso a paso siguiendo los diagramas de flujo.

---

## 📝 Notas Finales

- Esta documentación está basada en código REAL y FUNCIONAL
- Todos los errores mencionados fueron encontrados y resueltos en producción
- Las soluciones han sido probadas y validadas con el SII
- El código está optimizado para certificación (Maullin) pero funciona igual en producción (Palena)

**Última actualización:** Enero 2026

**Autor:** Basado en el sistema de Comercial Isabel

**Licencia:** Úsalo libremente, mejóralo, compártelo

---

¡Mucha suerte con tu implementación! 🚀

Si tienes dudas, revisa:
1. Los logs de depuración
2. La sección de errores comunes
3. Los diagramas de flujo
4. Los ejemplos de código

Y recuerda: **PreserveWhitespace = true** y **ISO-8859-1 sin BOM** son las dos reglas más importantes.
