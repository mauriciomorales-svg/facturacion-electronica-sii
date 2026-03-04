# Guía Completa: Cómo Generar DTE Correctamente para el SII de Chile

## 📋 Tabla de Contenidos
1. [Principios Fundamentales](#principios-fundamentales)
2. [Estructura del Proceso](#estructura-del-proceso)
3. [Reglas Críticas del XML](#reglas-críticas-del-xml)
4. [Generación del TED (Timbre Electrónico)](#generación-del-ted)
5. [Firma Digital del DTE](#firma-digital-del-dte)
6. [Creación del EnvioDTE](#creación-del-enviodte)
7. [Firma del EnvioDTE](#firma-del-enviodte)
8. [Validaciones Pre-Envío](#validaciones-pre-envío)
9. [Errores Comunes y Soluciones](#errores-comunes-y-soluciones)
10. [Código de Referencia](#código-de-referencia)

---

## 🎯 Principios Fundamentales

### 1. **PreserveWhitespace = true SIEMPRE**
```csharp
XmlDocument doc = new XmlDocument();
doc.PreserveWhitespace = true;  // ⚠️ CRÍTICO
doc.LoadXml(xmlString);
```

**¿Por qué?** La firma digital depende de cada byte del documento. Si .NET elimina espacios o saltos de línea, la firma se invalida.

### 2. **Encoding ISO-8859-1 sin BOM**
```csharp
Encoding iso88591 = Encoding.GetEncoding("ISO-8859-1");
// NUNCA usar UTF-8 con BOM
```

**¿Por qué?** El SII rechaza archivos con BOM (Byte Order Mark). ISO-8859-1 no tiene BOM.

### 3. **Formato ANTES de Firmar**
```csharp
// ✅ CORRECTO: Formatear primero, luego firmar
string xmlFormateado = GenerarXMLConFormato();
string xmlFirmado = FirmarDocumento(xmlFormateado);

// ❌ INCORRECTO: Firmar y luego formatear invalida la firma
```

### 4. **Canonicalización C14N Inclusiva**
```csharp
signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
// URL: http://www.w3.org/TR/2001/REC-xml-c14n-20010315
```

---

## 🏗️ Estructura del Proceso

```
1. Generar DTE (con formato)
   ↓
2. Generar TED (Timbre Electrónico)
   ↓
3. Insertar TED en el DTE
   ↓
4. Firmar DTE (Signature como HERMANA de Documento)
   ↓
5. Crear EnvioDTE (sobre con SetDTE)
   ↓
6. Insertar DTE firmado en EnvioDTE
   ↓
7. Firmar EnvioDTE (Signature referencia #SetDoc)
   ↓
8. Validar contra XSD
   ↓
9. Enviar al SII
```

---

## 📝 Reglas Críticas del XML

### Estructura del DTE

```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<DTE xmlns="http://www.sii.cl/SiiDte" version="1.0">
  <Documento ID="F1T33">
    <Encabezado>
      <IdDoc>
        <TipoDTE>33</TipoDTE>
        <Folio>1</Folio>
        <FchEmis>2026-01-18</FchEmis>
      </IdDoc>
      <Emisor>
        <RUTEmisor>8451335-0</RUTEmisor>
        <RznSoc>MARTA INALBIA URRA ESCOBAR</RznSoc>
        <!-- ... -->
      </Emisor>
      <Receptor>
        <RUTRecep>12345678-9</RUTRecep>
        <!-- ... -->
      </Receptor>
      <Totales>
        <MntNeto>100</MntNeto>
        <TasaIVA>19</TasaIVA>
        <IVA>19</IVA>
        <MntTotal>119</MntTotal>
      </Totales>
    </Encabezado>
    <Detalle>
      <NroLinDet>1</NroLinDet>
      <!-- ... -->
    </Detalle>
    <TED version="1.0">
      <DD>
        <!-- Datos del timbre -->
      </DD>
      <FRMT algoritmo="SHA1withRSA">...</FRMT>
    </TED>
    <TmstFirma>2026-01-18T23:28:50</TmstFirma>
  </Documento>
  <!-- ⚠️ CRÍTICO: Signature es HERMANA de Documento, NO hija -->
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    <!-- ... -->
  </Signature>
</DTE>
```

### ⚠️ Errores Comunes de Estructura

#### ❌ INCORRECTO: Firma dentro de Documento
```xml
<Documento ID="F1T33">
  <!-- contenido -->
  <Signature>...</Signature>  <!-- ❌ MAL -->
</Documento>
```

#### ✅ CORRECTO: Firma como hermana de Documento
```xml
<DTE>
  <Documento ID="F1T33">
    <!-- contenido -->
  </Documento>
  <Signature>...</Signature>  <!-- ✅ BIEN -->
</DTE>
```

---

## 🎫 Generación del TED (Timbre Electrónico)

### Paso 1: Construir el nodo DD

```csharp
public static string GenerarNodoDD(
    string rutEmisor, int tipoDTE, int folio, string fechaEmision,
    string rutReceptor, string razonSocialReceptor, int montoTotal,
    string itemPrincipal, string cafXml, string fechaTimbre)
{
    StringBuilder sb = new StringBuilder();
    
    // ⚠️ CRÍTICO: Sin espacios, sin saltos de línea (se limpiará después)
    sb.Append("<DD>");
    sb.Append($"<RE>{rutEmisor}</RE>");
    sb.Append($"<TD>{tipoDTE}</TD>");
    sb.Append($"<F>{folio}</F>");
    sb.Append($"<FE>{fechaEmision}</FE>");
    sb.Append($"<RR>{rutReceptor}</RR>");
    sb.Append($"<RSR>{razonSocialReceptor}</RSR>");
    sb.Append($"<MNT>{montoTotal}</MNT>");
    sb.Append($"<IT1>{itemPrincipal}</IT1>");
    
    // Extraer CAF del XML (sin la declaración XML)
    sb.Append(ExtraerCAFSinDeclaracion(cafXml));
    
    sb.Append($"<TSTED>{fechaTimbre}</TSTED>");
    sb.Append("</DD>");
    
    return sb.ToString();
}
```

### Paso 2: Limpiar el nodo DD

```csharp
public static string LimpiarNodoDD(string nodoDD)
{
    // ⚠️ CRÍTICO: Eliminar TODOS los espacios y saltos de línea
    // La firma RSA se calcula sobre el texto EXACTO
    
    return nodoDD
        .Replace("\r\n", "")
        .Replace("\n", "")
        .Replace("\r", "")
        .Replace("  ", "")
        .Replace("> <", "><")  // Eliminar espacios entre tags
        .Trim();
}
```

### Paso 3: Firmar el DD con la clave privada del CAF

```csharp
public static string FirmarNodoDD(string ddLimpio, string cafXml)
{
    // 1. Extraer clave privada del CAF
    string clavePrivadaPEM = ExtraerClavePrivada(cafXml);
    
    // 2. Importar clave RSA
    RSACryptoServiceProvider rsa = ImportarClavePrivada(clavePrivadaPEM);
    
    // 3. Calcular hash SHA1 del DD
    byte[] ddBytes = Encoding.UTF8.GetBytes(ddLimpio);
    byte[] hash = SHA1.Create().ComputeHash(ddBytes);
    
    // 4. Firmar el hash
    byte[] firma = rsa.SignHash(hash, CryptoConfig.MapNameToOID("SHA1"));
    
    // 5. Convertir a Base64
    return Convert.ToBase64String(firma);
}
```

### Paso 4: Construir TED completo

```csharp
public static string GenerarTEDCompleto(string ddLimpio, string firmaBase64)
{
    // ⚠️ Ahora SÍ aplicamos formato legible
    return $@"
<TED version=""1.0"">
    <DD>{ddLimpio}</DD>
    <FRMT algoritmo=""SHA1withRSA"">{firmaBase64}</FRMT>
</TED>".Trim();
}
```

---

## 🔐 Firma Digital del DTE

### Configuración Correcta de SignedXml

```csharp
public string FirmarDocumento(string xmlConFormato, X509Certificate2 certificado, string idDocumento)
{
    // 1. Cargar XML preservando espacios
    XmlDocument doc = new XmlDocument();
    doc.PreserveWhitespace = true;  // ⚠️ CRÍTICO
    doc.LoadXml(xmlConFormato);
    
    // 2. Encontrar nodo DTE y Documento
    XmlElement dteNode = doc.GetElementsByTagName("DTE")[0] as XmlElement;
    XmlNode documentoNode = doc.SelectNodes($"//*[@ID='{idDocumento}']")[0];
    
    // 3. Eliminar firmas previas (si existen)
    EliminarFirmasPrevias(dteNode, documentoNode);
    
    // 4. Construir SignedXml
    SignedXml signedXml = new SignedXml(doc)
    {
        SigningKey = certificado.GetRSAPrivateKey()
    };
    
    // ⚠️ CRÍTICO: C14N Inclusivo
    signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
    signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
    
    // 5. Reference al Documento (ej: "#F1T33")
    Reference reference = new Reference
    {
        Uri = $"#{idDocumento}",
        DigestMethod = SignedXml.XmlDsigSHA1Url
    };
    reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
    signedXml.AddReference(reference);
    
    // 6. KeyInfo con clave pública y certificado
    KeyInfo keyInfo = new KeyInfo();
    keyInfo.AddClause(new RSAKeyValue(certificado.GetRSAPublicKey()));
    keyInfo.AddClause(new KeyInfoX509Data(certificado));
    signedXml.KeyInfo = keyInfo;
    
    // 7. Computar firma
    signedXml.ComputeSignature();
    XmlElement xmlSignature = signedXml.GetXml();
    
    // 8. ⚠️ CRÍTICO: Insertar Signature como HERMANA de Documento
    XmlNode signatureNode = doc.ImportNode(xmlSignature, true);
    dteNode.InsertAfter(signatureNode, documentoNode);
    
    return doc.OuterXml;
}
```

### Orden Correcto en el DTE

```
<DTE>
  ├── <Documento ID="F1T33">  ← Primer hijo
  │   └── (contenido)
  └── <Signature>             ← Segundo hijo (HERMANA de Documento)
      └── (firma digital)
```

---

## 📦 Creación del EnvioDTE

### Estructura Correcta del Sobre

```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<EnvioDTE xmlns="http://www.sii.cl/SiiDte" 
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
          xsi:schemaLocation="http://www.sii.cl/SiiDte EnvioDTE_v10.xsd" 
          version="1.0">
  <SetDTE ID="SetDoc">
    <Caratula version="1.0">
      <RutEmisor>8451335-0</RutEmisor>
      <RutEnvia>8451335-0</RutEnvia>
      <RutReceptor>60803000-K</RutReceptor>
      <FchResol>2014-08-22</FchResol>
      <NroResol>80</NroResol>
      <TmstFirmaEnv>2026-01-18T23:28:50</TmstFirmaEnv>
      <SubTotDTE>
        <TpoDTE>33</TpoDTE>
        <NroDTE>1</NroDTE>
      </SubTotDTE>
    </Caratula>
    <!-- DTE firmado va aquí -->
    <DTE version="1.0">
      <!-- ... -->
    </DTE>
  </SetDTE>
  <!-- Signature del sobre va aquí (al final) -->
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    <!-- ... -->
  </Signature>
</EnvioDTE>
```

### Código para Generar EnvioDTE

```csharp
public string GenerarEnvioCompleto(
    string dteFirmado, string rutEmisor, string rutEnvia,
    string rutReceptor, string fechaResol, string nroResol, int cantidadDTE)
{
    // ⚠️ IMPORTANTE: Usar zona horaria de Chile para timestamp
    DateTime fechaChile = TimeZoneInfo.ConvertTime(
        DateTime.Now, 
        TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
    );
    string timestamp = fechaChile.ToString("yyyy-MM-ddTHH:mm:ss");
    
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
    sb.AppendLine("<EnvioDTE xmlns=\"http://www.sii.cl/SiiDte\" " +
                  "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                  "xsi:schemaLocation=\"http://www.sii.cl/SiiDte EnvioDTE_v10.xsd\" " +
                  "version=\"1.0\">");
    
    sb.AppendLine("  <SetDTE ID=\"SetDoc\">");
    sb.AppendLine("    <Caratula version=\"1.0\">");
    sb.AppendLine($"      <RutEmisor>{rutEmisor}</RutEmisor>");
    sb.AppendLine($"      <RutEnvia>{rutEnvia}</RutEnvia>");
    sb.AppendLine($"      <RutReceptor>{rutReceptor}</RutReceptor>");
    sb.AppendLine($"      <FchResol>{fechaResol}</FchResol>");
    sb.AppendLine($"      <NroResol>{nroResol}</NroResol>");
    sb.AppendLine($"      <TmstFirmaEnv>{timestamp}</TmstFirmaEnv>");
    sb.AppendLine("      <SubTotDTE>");
    sb.AppendLine("        <TpoDTE>33</TpoDTE>");
    sb.AppendLine($"        <NroDTE>{cantidadDTE}</NroDTE>");
    sb.AppendLine("      </SubTotDTE>");
    sb.AppendLine("    </Caratula>");
    
    // Insertar DTE firmado (sin declaración XML)
    string dteSinDeclaracion = RemoverDeclaracionXML(dteFirmado);
    sb.AppendLine(IndentarXML(dteSinDeclaracion, 4));  // 4 espacios
    
    sb.AppendLine("  </SetDTE>");
    sb.AppendLine("</EnvioDTE>");
    
    return sb.ToString();
}
```

---

## 🔏 Firma del EnvioDTE

### Configuración Correcta

```csharp
public string FirmarEnvio(string xmlEnvioFormateado, string subjectCert)
{
    XmlDocument doc = new XmlDocument();
    doc.PreserveWhitespace = true;  // ⚠️ CRÍTICO
    doc.LoadXml(xmlEnvioFormateado);
    
    XmlElement root = doc.DocumentElement;
    
    // ⚠️ FIX SCH-00001: Asegurar cabeceras correctas
    root.SetAttribute("xmlns", "http://www.sii.cl/SiiDte");
    root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
    root.SetAttribute(
        "schemaLocation",
        "http://www.w3.org/2001/XMLSchema-instance",
        "http://www.sii.cl/SiiDte EnvioDTE_v10.xsd"
    );
    root.SetAttribute("version", "1.0");
    
    // Asegurar SetDTE ID="SetDoc"
    XmlElement setDTE = root.SelectSingleNode("*[local-name()='SetDTE']") as XmlElement;
    setDTE.SetAttribute("ID", "SetDoc");
    
    // Eliminar firmas previas
    EliminarFirmasPrevias(setDTE, root);
    
    // Recuperar certificado
    X509Certificate2 certificado = RecuperarCertificado(subjectCert);
    
    // Construir SignedXml
    SignedXml signedXml = new SignedXml(doc)
    {
        SigningKey = certificado.GetRSAPrivateKey()
    };
    
    signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
    signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
    
    // ⚠️ Reference a #SetDoc (no al EnvioDTE completo)
    Reference reference = new Reference
    {
        Uri = "#SetDoc",
        DigestMethod = SignedXml.XmlDsigSHA1Url
    };
    reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
    signedXml.AddReference(reference);
    
    KeyInfo ki = new KeyInfo();
    ki.AddClause(new RSAKeyValue(certificado.GetRSAPublicKey()));
    ki.AddClause(new KeyInfoX509Data(certificado));
    signedXml.KeyInfo = ki;
    
    signedXml.ComputeSignature();
    XmlElement xmlSignature = signedXml.GetXml();
    
    // ⚠️ Agregar Signature al FINAL de EnvioDTE
    root.AppendChild(doc.ImportNode(xmlSignature, true));
    
    return doc.OuterXml;
}
```

---

## ✅ Validaciones Pre-Envío

### 1. Validación contra XSD

```csharp
public static List<string> ValidarContraEsquema(XmlDocument xmlDoc, string rutaXSD)
{
    List<string> errores = new List<string>();
    
    XmlReaderSettings settings = new XmlReaderSettings();
    settings.ValidationType = ValidationType.Schema;
    settings.Schemas.Add("http://www.sii.cl/SiiDte", rutaXSD);
    
    settings.ValidationEventHandler += (sender, e) =>
    {
        if (e.Severity == XmlSeverityType.Error)
        {
            errores.Add($"Error XSD: {e.Message}");
        }
    };
    
    using (StringReader sr = new StringReader(xmlDoc.OuterXml))
    using (XmlReader reader = XmlReader.Create(sr, settings))
    {
        while (reader.Read()) { }
    }
    
    return errores;
}
```

### 2. Validación de Firma Digital

```csharp
public static bool ValidarFirma(XmlDocument xmlDoc, X509Certificate2 certificado)
{
    XmlNodeList nodeList = xmlDoc.GetElementsByTagName(
        "Signature", 
        "http://www.w3.org/2000/09/xmldsig#"
    );
    
    if (nodeList.Count == 0)
        return false;
    
    SignedXml signedXml = new SignedXml(xmlDoc);
    signedXml.LoadXml((XmlElement)nodeList[0]);
    
    return signedXml.CheckSignature(certificado, true);
}
```

### 3. Checklist Pre-Envío

```csharp
public static List<string> ValidarAntesDeEnviar(XmlDocument envioDTE)
{
    List<string> errores = new List<string>();
    
    // ✅ 1. Verificar encoding ISO-8859-1
    if (!envioDTE.FirstChild.OuterXml.Contains("ISO-8859-1"))
        errores.Add("❌ El encoding debe ser ISO-8859-1");
    
    // ✅ 2. Verificar namespace correcto
    XmlElement root = envioDTE.DocumentElement;
    if (root.NamespaceURI != "http://www.sii.cl/SiiDte")
        errores.Add("❌ Namespace incorrecto en EnvioDTE");
    
    // ✅ 3. Verificar version="1.0"
    if (root.GetAttribute("version") != "1.0")
        errores.Add("❌ Falta version=\"1.0\" en EnvioDTE");
    
    // ✅ 4. Verificar schemaLocation
    string schemaLoc = root.GetAttribute("schemaLocation", 
        "http://www.w3.org/2001/XMLSchema-instance");
    if (schemaLoc != "http://www.sii.cl/SiiDte EnvioDTE_v10.xsd")
        errores.Add("❌ schemaLocation incorrecto");
    
    // ✅ 5. Verificar SetDTE ID="SetDoc"
    XmlElement setDTE = root.SelectSingleNode("*[local-name()='SetDTE']") as XmlElement;
    if (setDTE?.GetAttribute("ID") != "SetDoc")
        errores.Add("❌ SetDTE debe tener ID=\"SetDoc\"");
    
    // ✅ 6. Verificar firma del sobre
    XmlNodeList firmasEnvio = root.SelectNodes("*[local-name()='Signature']");
    if (firmasEnvio.Count == 0)
        errores.Add("❌ Falta firma del sobre (EnvioDTE)");
    
    // ✅ 7. Verificar firma del DTE
    XmlNodeList firmasDTE = envioDTE.SelectNodes(
        "//*[local-name()='DTE']/*[local-name()='Signature']");
    if (firmasDTE.Count == 0)
        errores.Add("❌ Falta firma del DTE");
    
    // ✅ 8. Verificar TED en el DTE
    XmlNode ted = envioDTE.SelectSingleNode("//*[local-name()='TED']");
    if (ted == null)
        errores.Add("❌ Falta el TED (Timbre Electrónico)");
    
    // ✅ 9. Verificar tamaño de líneas (máx 4090 caracteres)
    string xmlText = envioDTE.OuterXml;
    string[] lineas = xmlText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    for (int i = 0; i < lineas.Length; i++)
    {
        if (lineas[i].Length > 4090)
        {
            errores.Add($"❌ Línea {i + 1} excede 4090 caracteres ({lineas[i].Length} chars)");
        }
    }
    
    return errores;
}
```

---

## 🚨 Errores Comunes y Soluciones

### Error: CHR-00002 (Línea demasiado larga)

**Síntoma:**
```
CHR-00002: Linea demasiado larga [>4090]
```

**Causa:** Certificado X509 en KeyInfo sin saltos de línea

**Solución:**
```csharp
// Aplicar formato ANTES de firmar
XmlWriterSettings settings = new XmlWriterSettings
{
    Indent = true,
    IndentChars = "  ",  // 2 espacios
    NewLineChars = "\r\n"
};

// Y después de firmar, romper líneas en KeyInfo
string xmlFirmado = doc.OuterXml
    .Replace("</SignatureValue><KeyInfo>", "</SignatureValue>\r\n<KeyInfo>")
    .Replace("</KeyInfo></Signature>", "</KeyInfo>\r\n</Signature>");
```

### Error: SCH-00001 (Esquema inválido)

**Síntoma:**
```
SCH-00001: Esquema invalido - Documento
```

**Causa:** Namespace o schemaLocation incorrectos

**Solución:**
```csharp
// EnvioDTE
root.SetAttribute("xmlns", "http://www.sii.cl/SiiDte");
root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
root.SetAttribute(
    "schemaLocation",
    "http://www.w3.org/2001/XMLSchema-instance",
    "http://www.sii.cl/SiiDte EnvioDTE_v10.xsd"
);
root.SetAttribute("version", "1.0");

// DTE
dteNode.SetAttribute("xmlns", "http://www.sii.cl/SiiDte");
dteNode.SetAttribute("version", "1.0");
```

### Error: Firma inválida

**Síntoma:**
```
La firma digital no es válida
```

**Causas comunes:**
1. Modificar XML después de firmar
2. No usar `PreserveWhitespace = true`
3. Canonicalización incorrecta
4. BOM en el archivo

**Solución:**
```csharp
// 1. SIEMPRE preservar espacios
XmlDocument doc = new XmlDocument();
doc.PreserveWhitespace = true;

// 2. Usar C14N Inclusivo
signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

// 3. NO modificar después de firmar
string xmlFirmado = FirmarDocumento(xml);
// ❌ NO hacer: xmlFirmado = xmlFirmado.Replace(...)

// 4. Guardar sin BOM
byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(xmlFirmado);
File.WriteAllBytes(path, bytes);  // Sin BOM
```

### Error: Signature en lugar incorrecto

**Síntoma:**
```
El XSD no valida porque <Signature> está dentro de <Documento>
```

**Solución:**
```csharp
// ❌ INCORRECTO
documentoNode.AppendChild(signatureNode);

// ✅ CORRECTO: Signature como HERMANA de Documento
XmlElement dteNode = doc.GetElementsByTagName("DTE")[0] as XmlElement;
dteNode.InsertAfter(signatureNode, documentoNode);
```

### Error: TED inválido

**Síntoma:**
```
El timbre electrónico no verifica
```

**Causas:**
1. Espacios en el nodo DD antes de firmar
2. Clave privada incorrecta del CAF
3. Datos inconsistentes entre DTE y DD

**Solución:**
```csharp
// 1. Limpiar DD antes de firmar
string ddLimpio = nodoDD
    .Replace("\r\n", "")
    .Replace("\n", "")
    .Replace("  ", "")
    .Replace("> <", "><")
    .Trim();

// 2. Verificar que los datos coincidan
// RE, TD, F, FE, RR, MNT deben ser idénticos en DTE y DD

// 3. Usar la clave privada del CAF (NO del certificado digital)
RSA claveCAF = ImportarClavePrivadaCAF(cafXml);
```

### Error: Timestamp desfasado

**Síntoma:**
```
Fecha/hora del documento no coincide con servidor SII
```

**Solución:**
```csharp
// Usar zona horaria de Chile (UTC-3 o UTC-4 según DST)
DateTime fechaChile = TimeZoneInfo.ConvertTime(
    DateTime.Now,
    TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
);
string timestamp = fechaChile.ToString("yyyy-MM-ddTHH:mm:ss");
```

---

## 💻 Código de Referencia

### Clase Completa: GeneradorDTECorrecto.cs

```csharp
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Security.Cryptography.X509Certificates;

namespace FacturacionElectronica
{
    public class GeneradorDTECorrecto
    {
        public static string GenerarDTECompleto(
            DatosFactura datos,
            X509Certificate2 certificado,
            string cafXml)
        {
            // PASO 1: Generar estructura base del DTE con formato
            string dteBase = GenerarEstructuraDTE(datos);
            
            // PASO 2: Generar TED (Timbre Electrónico)
            string ted = GenerarTED(datos, cafXml);
            
            // PASO 3: Insertar TED en el DTE
            string dteConTED = InsertarTED(dteBase, ted);
            
            // PASO 4: Firmar el DTE
            string dteFirmado = FirmarDTE(dteConTED, certificado, datos.Folio);
            
            // PASO 5: Validar
            ValidarDTE(dteFirmado);
            
            return dteFirmado;
        }
        
        private static string GenerarEstructuraDTE(DatosFactura datos)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
            sb.AppendLine("<DTE xmlns=\"http://www.sii.cl/SiiDte\" version=\"1.0\">");
            sb.AppendLine($"  <Documento ID=\"F{datos.Folio}T{datos.TipoDTE}\">");
            
            // Encabezado
            sb.AppendLine("    <Encabezado>");
            sb.AppendLine("      <IdDoc>");
            sb.AppendLine($"        <TipoDTE>{datos.TipoDTE}</TipoDTE>");
            sb.AppendLine($"        <Folio>{datos.Folio}</Folio>");
            sb.AppendLine($"        <FchEmis>{datos.FechaEmision:yyyy-MM-dd}</FchEmis>");
            sb.AppendLine("      </IdDoc>");
            
            // Emisor
            sb.AppendLine("      <Emisor>");
            sb.AppendLine($"        <RUTEmisor>{datos.RutEmisor}</RUTEmisor>");
            sb.AppendLine($"        <RznSoc>{EscaparXML(datos.RazonSocialEmisor)}</RznSoc>");
            // ... más campos
            sb.AppendLine("      </Emisor>");
            
            // Receptor
            sb.AppendLine("      <Receptor>");
            sb.AppendLine($"        <RUTRecep>{datos.RutReceptor}</RUTRecep>");
            sb.AppendLine($"        <RznSocRecep>{EscaparXML(datos.RazonSocialReceptor)}</RznSocRecep>");
            // ... más campos
            sb.AppendLine("      </Receptor>");
            
            // Totales
            sb.AppendLine("      <Totales>");
            sb.AppendLine($"        <MntNeto>{datos.MontoNeto}</MntNeto>");
            sb.AppendLine($"        <TasaIVA>19</TasaIVA>");
            sb.AppendLine($"        <IVA>{datos.IVA}</IVA>");
            sb.AppendLine($"        <MntTotal>{datos.MontoTotal}</MntTotal>");
            sb.AppendLine("      </Totales>");
            sb.AppendLine("    </Encabezado>");
            
            // Detalles
            foreach (var detalle in datos.Detalles)
            {
                sb.AppendLine("    <Detalle>");
                sb.AppendLine($"      <NroLinDet>{detalle.Linea}</NroLinDet>");
                sb.AppendLine($"      <NmbItem>{EscaparXML(detalle.Nombre)}</NmbItem>");
                sb.AppendLine($"      <QtyItem>{detalle.Cantidad}</QtyItem>");
                sb.AppendLine($"      <PrcItem>{detalle.Precio:F2}</PrcItem>");
                sb.AppendLine($"      <MontoItem>{detalle.Total}</MontoItem>");
                sb.AppendLine("    </Detalle>");
            }
            
            // Placeholder para TED (se insertará después)
            sb.AppendLine("    <!-- TED_PLACEHOLDER -->");
            
            // TmstFirma
            DateTime ahora = TimeZoneInfo.ConvertTime(
                DateTime.Now,
                TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
            );
            sb.AppendLine($"    <TmstFirma>{ahora:yyyy-MM-ddTHH:mm:ss}</TmstFirma>");
            
            sb.AppendLine("  </Documento>");
            sb.AppendLine("</DTE>");
            
            return sb.ToString();
        }
        
        private static string EscaparXML(string texto)
        {
            return System.Security.SecurityElement.Escape(texto);
        }
    }
}
```

---

## 📚 Recursos Adicionales

### Archivos XSD Requeridos

Descarga los esquemas oficiales del SII:
- `DTE_v10.xsd`
- `EnvioDTE_v10.xsd`
- `SiiTypes_v10.xsd`
- `xmldsignature_v10.xsd`

Ubicación recomendada: `C:\SchemasSII\`

### Estructura de Carpetas Recomendada

```
C:\
├── SchemasSII\
│   ├── DTE_v10.xsd
│   ├── EnvioDTE_v10.xsd
│   ├── SiiTypes_v10.xsd
│   └── xmldsignature_v10.xsd
│
└── FacturasElectronicas\
    ├── CAF\
    │   └── CAF_33_1-60.xml
    ├── DTE\
    │   ├── DTE.xml
    │   └── DTE_Firmado.xml
    ├── EnvioDTE\
    │   ├── EnvioDTE.xml
    │   └── EnvioDTE_Firmado.xml
    └── Logs\
        ├── depuracion_log.txt
        └── ValidacionEnvioDTE.log
```

### URLs del SII

**Certificación (Maullin):**
- Semilla: `https://maullin.sii.cl/DTEWS/CrSeed.jws`
- Token: `https://maullin.sii.cl/DTEWS/GetTokenFromSeed.jws`
- Upload: `https://maullin.sii.cl/cgi_dte/UPL/DTEUpload`

**Producción (Palena):**
- Semilla: `https://palena.sii.cl/DTEWS/CrSeed.jws`
- Token: `https://palena.sii.cl/DTEWS/GetTokenFromSeed.jws`
- Upload: `https://palena.sii.cl/cgi_dte/UPL/DTEUpload`

---

## ✨ Resumen de Reglas de Oro

1. **PreserveWhitespace = true** en TODOS los XmlDocument
2. **ISO-8859-1 sin BOM** para todos los archivos XML
3. **Formatear ANTES de firmar**, nunca después
4. **C14N Inclusivo** (XmlDsigC14NTransformUrl) para canonicalización
5. **Signature como HERMANA** de Documento en DTE
6. **Limpiar DD** (sin espacios) antes de firmar el TED
7. **SetDTE ID="SetDoc"** en EnvioDTE
8. **Zona horaria de Chile** para todos los timestamps
9. **Validar contra XSD** antes de enviar
10. **Nunca modificar XML** después de firmar

---

## 🎓 Conclusión

La generación correcta de DTE requiere atención meticulosa a los detalles. Los errores más comunes provienen de:

- No preservar espacios en blanco
- Modificar XML después de firmar
- Ubicación incorrecta de elementos Signature
- Encoding incorrecto o con BOM
- Namespaces y schemaLocation incorrectos

Siguiendo esta guía y los ejemplos de código proporcionados, deberías poder generar DTEs que sean aceptados por el SII sin problemas.

**¡Buena suerte con tu implementación!** 🚀
