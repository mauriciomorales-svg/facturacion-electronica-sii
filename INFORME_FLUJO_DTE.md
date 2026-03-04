# INFORME COMPLETO: Flujo de Construcción de Documentos Electrónicos (DTE)

## 1. VISIÓN GENERAL DEL FLUJO

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        FLUJO DE GENERACIÓN DE DTE                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  [1] CAFService          [2] TEDService           [3] XMLBuilderService     │
│  ─────────────────       ──────────────           ─────────────────────     │
│  • Lee archivo CAF       • Genera nodo DD          • Construye XML DTE      │
│  • Extrae claves RSA     • Firma con SHA1+RSA      • Agrega TED al doc      │
│  • Valida rango folios   • Genera TED completo     • Construye EnvioDTE     │
│                                                                             │
│         ↓                        ↓                          ↓               │
│                                                                             │
│  [4] FirmaService                           [5] SIIService                  │
│  ─────────────────                          ──────────────                  │
│  • Firma DTE (documento)                    • Obtiene semilla               │
│  • Firma EnvioDTE (sobre)                   • Obtiene token                 │
│  • Usa certificado X.509                    • Envía DTE al SII              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. PASO 1: CAFService - Lectura del CAF

**Archivo:** `Services/CAFService.cs`

### 2.1 ¿Qué es el CAF?
El **CAF (Código de Autorización de Folios)** es un archivo XML entregado por el SII que autoriza a una empresa a emitir documentos electrónicos con un rango específico de folios.

### 2.2 Estructura del CAF
```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<AUTORIZACION>
  <CAF version="1.0">
    <DA>
      <RE>78301789-K</RE>           <!-- RUT Emisor -->
      <RS>NOMBRE EMPRESA</RS>        <!-- Razón Social -->
      <TD>33</TD>                    <!-- Tipo Documento (33=Factura) -->
      <RNG>
        <D>1</D>                     <!-- Folio Desde -->
        <H>100</H>                   <!-- Folio Hasta -->
      </RNG>
      <FA>2026-01-29</FA>            <!-- Fecha Autorización -->
      <RSAPK>
        <M>pucMQKHL83...</M>         <!-- Módulo RSA (clave pública) -->
        <E>Aw==</E>                  <!-- Exponente RSA -->
      </RSAPK>
      <IDK>100</IDK>                 <!-- ID de la clave -->
    </DA>
    <FRMA algoritmo="SHA1withRSA">k4qCFE4U4fM...</FRMA>
  </CAF>
  <RSASK>-----BEGIN RSA PRIVATE KEY-----
MIIBOgIBAAJBAKbnF...               <!-- Clave privada RSA -->
-----END RSA PRIVATE KEY-----</RSASK>
</AUTORIZACION>
```

### 2.3 Proceso de Lectura (CAFService.cs:62-234)

1. **Prioridad 1: Archivo en disco** (`Data/CAFs/FoliosSII{RUT}{TipoDTE}*.xml`)
2. **Prioridad 2: Base de datos** (tabla `CAF`)

```csharp
// Patrón de búsqueda del archivo CAF
var patronCAF = $"FoliosSII{rutEmisor}{tipoDTE}*.xml";
// Ejemplo: FoliosSII7830178933*.xml para facturas de RUT 78301789-K
```

### 2.4 Sanitización del CAF (CAFService.cs:349-684)

El método `ObtenerCafBlindado()` limpia el XML de posible HTML corrupto:
- Elimina tags HTML (`<hr>`, `<tr>`, `<table>`, etc.)
- Extrae solo el bloque `<AUTORIZACION>...</AUTORIZACION>`
- Valida que sea XML válido antes de usarlo

### 2.5 Datos Extraídos del CAF

```csharp
return new CAFData
{
    RutEmisor = "78301789-K",
    RazonSocial = "SOFTWARE MAURICIO...",
    TipoDTE = 33,
    FolioInicial = 1,
    FolioFinal = 100,
    FechaAutorizacion = DateTime.Parse("2026-01-29"),
    ModuloRSA = "pucMQKHL83...",
    ExponenteRSA = "Aw==",
    IdK = 100,
    ClavePrivadaPEM = "-----BEGIN RSA PRIVATE KEY-----...",
    XMLOriginal = "<AUTORIZACION>..."  // CAF completo
};
```

---

## 3. PASO 2: TEDService - Generación del Timbre Electrónico

**Archivo:** `Services/TEDService.cs`

### 3.1 ¿Qué es el TED?
El **TED (Timbre Electrónico Digital)** es una firma digital que valida la autenticidad del documento. Contiene datos resumidos del DTE firmados con la clave privada del CAF.

### 3.2 Estructura del TED

```xml
<TED version="1.0">
  <DD>
    <RE>78301789-K</RE>              <!-- RUT Emisor -->
    <TD>33</TD>                       <!-- Tipo Documento -->
    <F>1</F>                          <!-- Folio -->
    <FE>2026-01-30</FE>               <!-- Fecha Emisión -->
    <RR>66666666-6</RR>               <!-- RUT Receptor -->
    <RSR>EMPRESA PRUEBA SII</RSR>     <!-- Razón Social Receptor -->
    <MNT>286730</MNT>                 <!-- Monto Total -->
    <IT1>Cajon 125x1020</IT1>         <!-- Primer Item (max 40 chars) -->
    <CAF version="1.0">...</CAF>      <!-- CAF completo -->
    <TSTED>2026-01-30T02:45:27</TSTED> <!-- Timestamp del Timbre -->
  </DD>
  <FRMT algoritmo="SHA1withRSA">n15Cu4dUNkrIOcf...</FRMT>  <!-- Firma -->
</TED>
```

### 3.3 Proceso de Generación (TEDService.cs:50-152)

```csharp
public string GenerarTED(DocumentoTributario documento, CAFData caf)
{
    // PASO 0: Validar folio en rango autorizado
    ValidadorRangoFolios.ValidarFolioEnRango(caf.XMLOriginal, documento.Folio, ...);

    // PASO 1: Generar nodo DD
    var nodoDD = GenerarNodoDDParaFirma(...);

    // PASO 2: Limpiar DD (eliminar espacios entre tags)
    var nodoDDLimpio = LimpiarNodoDD(nodoDD);

    // PASO 3: Extraer clave privada del CAF
    var clavePrivada = ExtraerClavePrivada(caf.XMLOriginal);

    // PASO 4: Importar clave RSA con BouncyCastle
    using var claveRSA = ImportarClavePrivada(clavePrivada);

    // PASO 5: Firmar DD con SHA1+RSA
    var firma = FirmarTexto(nodoDDLimpio, claveRSA);

    // PASO 6: Ensamblar TED completo
    return GenerarTEDCompleto(nodoDDLimpio, firma);
}
```

### 3.4 Limpieza del DD (CRÍTICO)

El nodo DD debe estar **sin espacios entre tags** antes de firmar:

```csharp
// TEDService.cs:620-641
private string LimpiarNodoDD(string nodoDD)
{
    // Eliminar declaración XML
    nodoDD = Regex.Replace(nodoDD, @"<\?xml[^>]*\?>", "");

    // CRÍTICO: Eliminar TODOS los espacios entre tags
    nodoDD = Regex.Replace(nodoDD, @">\s+<", "><");

    // Eliminar saltos de línea
    nodoDD = nodoDD.Replace("\r\n", "").Replace("\n", "").Replace("\t", "").Trim();

    return nodoDD;
}
```

**Resultado:**
```xml
<DD><RE>78301789-K</RE><TD>33</TD><F>1</F><FE>2026-01-30</FE>...</DD>
```

### 3.5 Firma del DD (TEDService.cs:578-610)

```csharp
private string FirmarTexto(string texto, RSACryptoServiceProvider clavePrivada)
{
    // 1. Convertir texto a bytes (UTF-8)
    byte[] datos = Encoding.UTF8.GetBytes(texto);

    // 2. Calcular hash SHA1
    using (SHA1 sha1 = SHA1.Create())
    {
        byte[] hash = sha1.ComputeHash(datos);

        // 3. Firmar el hash (NO SignData, sino SignHash)
        byte[] firma = clavePrivada.SignHash(hash, CryptoConfig.MapNameToOID("SHA1"));

        // 4. Convertir a Base64
        return Convert.ToBase64String(firma);
    }
}
```

### 3.6 Importación de Clave Privada (BouncyCastle)

```csharp
// TEDService.cs:509-566
private RSACryptoServiceProvider ImportarClavePrivada(string clavePrivadaPEM)
{
    using (var sr = new StringReader(clavePrivadaPEM))
    {
        // Usar BouncyCastle para leer PEM
        PemReader pemReader = new PemReader(sr);
        var keyPair = (AsymmetricCipherKeyPair)pemReader.ReadObject();
        var keyParams = (RsaPrivateCrtKeyParameters)keyPair.Private;

        // Convertir a RSAParameters de .NET
        RSAParameters parametros = new RSAParameters
        {
            Modulus = keyParams.Modulus.ToByteArrayUnsigned(),
            Exponent = keyParams.PublicExponent.ToByteArrayUnsigned(),
            D = keyParams.Exponent.ToByteArrayUnsigned(),
            P = keyParams.P.ToByteArrayUnsigned(),
            Q = keyParams.Q.ToByteArrayUnsigned(),
            DP = keyParams.DP.ToByteArrayUnsigned(),
            DQ = keyParams.DQ.ToByteArrayUnsigned(),
            InverseQ = keyParams.QInv.ToByteArrayUnsigned()
        };

        var rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(parametros);
        return rsa;
    }
}
```

---

## 4. PASO 3: XMLBuilderService - Construcción del XML

**Archivo:** `Services/XMLBuilderService.cs`

### 4.1 Construcción del DTE (XMLBuilderService.cs:22-142)

```csharp
public string ConstruirXMLDTE(DocumentoTributario documento, string ted)
{
    var doc = new XmlDocument();
    doc.AppendChild(doc.CreateXmlDeclaration("1.0", "ISO-8859-1", null));

    // Crear elemento DTE
    var dte = doc.CreateElement("DTE");
    dte.SetAttribute("version", "1.0");
    doc.AppendChild(dte);

    // Crear Documento con ID="F{folio}T{tipoDTE}"
    var documentoElement = doc.CreateElement("Documento");
    documentoElement.SetAttribute("ID", $"F{documento.Folio}T{documento.TipoDTE}");
    dte.AppendChild(documentoElement);

    // Agregar Encabezado (con Totales DENTRO)
    var encabezado = ConstruirEncabezadoXml(doc, documento);
    documentoElement.AppendChild(encabezado);

    // Agregar Detalles (múltiples elementos <Detalle>)
    var detalles = ConstruirDetallesXml(doc, documento);
    foreach (var detalle in detalles)
    {
        documentoElement.AppendChild(detalle);
    }

    // Insertar TED
    var tedFragment = doc.CreateDocumentFragment();
    tedFragment.InnerXml = ted;
    documentoElement.AppendChild(tedFragment);

    // Agregar namespace al DTE
    // xmlns="http://www.sii.cl/SiiDte"

    return doc.OuterXml;
}
```

### 4.2 Estructura del Encabezado (XMLBuilderService.cs:321-342)

```csharp
private XmlElement ConstruirEncabezadoXml(XmlDocument doc, DocumentoTributario documento)
{
    var encabezado = doc.CreateElement("Encabezado");

    // IdDoc (identificación del documento)
    var idDoc = doc.CreateElement("IdDoc");
    idDoc.AppendChild(CrearElemento(doc, "TipoDTE", documento.TipoDTE.ToString()));
    idDoc.AppendChild(CrearElemento(doc, "Folio", documento.Folio.ToString()));
    idDoc.AppendChild(CrearElemento(doc, "FchEmis", documento.FechaEmision.ToString("yyyy-MM-dd")));
    idDoc.AppendChild(CrearElemento(doc, "FmaPago", "1")); // 1=Contado
    encabezado.AppendChild(idDoc);

    // Emisor
    encabezado.AppendChild(ConstruirEmisorXml(doc, documento.Encabezado.Emisor));

    // Receptor
    encabezado.AppendChild(ConstruirReceptorXml(doc, documento.Encabezado.Receptor));

    // IMPORTANTE: Totales DENTRO de Encabezado (según esquema SII)
    encabezado.AppendChild(ConstruirTotalesXml(doc, documento));

    return encabezado;
}
```

### 4.3 Estructura de Totales (XMLBuilderService.cs:417-442)

```csharp
private XmlElement ConstruirTotalesXml(XmlDocument doc, DocumentoTributario documento)
{
    var totales = doc.CreateElement("Totales");

    // MntNeto (solo si hay items afectos)
    if (documento.Totales.MntNeto > 0)
    {
        totales.AppendChild(CrearElemento(doc, "MntNeto", documento.Totales.MntNeto.ToString()));
    }

    // MntExe (si hay items exentos)
    if (documento.Totales.MntExe.HasValue && documento.Totales.MntExe.Value > 0)
    {
        totales.AppendChild(CrearElemento(doc, "MntExe", documento.Totales.MntExe.Value.ToString()));
    }

    // TasaIVA e IVA (solo si hay items afectos)
    if (documento.Totales.IVA > 0)
    {
        totales.AppendChild(CrearElemento(doc, "TasaIVA", "19"));
        totales.AppendChild(CrearElemento(doc, "IVA", documento.Totales.IVA.ToString()));
    }

    totales.AppendChild(CrearElemento(doc, "MntTotal", documento.Totales.MntTotal.ToString()));
    return totales;
}
```

### 4.4 Estructura de Detalles (XMLBuilderService.cs:372-415)

```csharp
private List<XmlElement> ConstruirDetallesXml(XmlDocument doc, DocumentoTributario documento)
{
    var detalles = new List<XmlElement>();

    for (int i = 0; i < documento.Detalles.Count; i++)
    {
        var detalle = documento.Detalles[i];
        var detalleElement = doc.CreateElement("Detalle");

        detalleElement.AppendChild(CrearElemento(doc, "NroLinDet", (i + 1).ToString()));

        // IndExe (1=Exento, debe ir ANTES de CdgItem)
        if (detalle.IndExe.HasValue)
            detalleElement.AppendChild(CrearElemento(doc, "IndExe", detalle.IndExe.Value.ToString()));

        // Código del item
        if (!string.IsNullOrEmpty(detalle.Codigo))
        {
            var cdgItem = doc.CreateElement("CdgItem");
            cdgItem.AppendChild(CrearElemento(doc, "TpoCodigo", "INT1"));
            cdgItem.AppendChild(CrearElemento(doc, "VlrCodigo", detalle.Codigo));
            detalleElement.AppendChild(cdgItem);
        }

        detalleElement.AppendChild(CrearElemento(doc, "NmbItem", detalle.Nombre));
        detalleElement.AppendChild(CrearElemento(doc, "QtyItem", detalle.Cantidad.ToString()));
        detalleElement.AppendChild(CrearElemento(doc, "UnmdItem", detalle.Unidad));
        detalleElement.AppendChild(CrearElemento(doc, "PrcItem", ((int)detalle.PrecioUnitario).ToString()));
        detalleElement.AppendChild(CrearElemento(doc, "MontoItem", ((int)detalle.MontoItem).ToString()));

        detalles.Add(detalleElement);
    }

    return detalles;
}
```

### 4.5 Construcción del EnvioDTE (XMLBuilderService.cs:144-217)

```csharp
public string ConstruirXMLEnvioDTE(string xmlDTE, string rutEmisor, string rutEnvia,
    string rutReceptor, string fechaResol = "2025-12-03", string nroResol = "0", int cantidadDTE = 1)
{
    var sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
    sb.AppendLine("<EnvioDTE xmlns=\"http://www.sii.cl/SiiDte\" ...");

    // SetDTE
    sb.AppendLine("  <SetDTE ID=\"SetDoc\">");

    // Carátula
    sb.AppendLine("    <Caratula version=\"1.0\">");
    sb.AppendLine($"      <RutEmisor>{rutEmisor}</RutEmisor>");     // 78301789-K
    sb.AppendLine($"      <RutEnvia>{rutEnvia}</RutEnvia>");         // 16238583-6
    sb.AppendLine($"      <RutReceptor>{rutReceptor}</RutReceptor>"); // 60803000-K
    sb.AppendLine($"      <FchResol>{fechaResol}</FchResol>");
    sb.AppendLine($"      <NroResol>{nroResol}</NroResol>");
    sb.AppendLine($"      <TmstFirmaEnv>{timestampFirmaEnv}</TmstFirmaEnv>");
    sb.AppendLine("      <SubTotDTE>");
    sb.AppendLine("        <TpoDTE>33</TpoDTE>");
    sb.AppendLine($"        <NroDTE>{cantidadDTE}</NroDTE>");
    sb.AppendLine("      </SubTotDTE>");
    sb.AppendLine("    </Caratula>");

    // DTE firmado (sin declaración XML)
    sb.AppendLine("    " + dteSinDeclaracion);

    sb.AppendLine("  </SetDTE>");
    sb.AppendLine("</EnvioDTE>");

    return sb.ToString();
}
```

---

## 5. PASO 4: FirmaService - Firma Digital con Certificado X.509

**Archivo:** `Services/FirmaService.cs`

### 5.1 Firma del DTE (FirmaService.cs:24-145)

```csharp
public string FirmarDTE(string xmlDTE, string idDocumento)
{
    var doc = new XmlDocument();
    doc.PreserveWhitespace = true;  // CRÍTICO
    doc.LoadXml(xmlDTE);

    // Buscar nodo Documento por ID (ej: "F1T33")
    var documentoNode = doc.SelectNodes($"//*[@ID='{idDocumento}']")?[0];

    // Obtener certificado del almacén de Windows
    var certificado = RecuperarCertificado(nombreCertificado);

    // Construir SignedXml
    var signedXml = new SignedXml(doc)
    {
        SigningKey = certificado.GetRSAPrivateKey()
    };

    signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
    signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

    var reference = new Reference
    {
        Uri = $"#{idDocumento}",  // #F1T33
        DigestMethod = SignedXml.XmlDsigSHA1Url
    };
    reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
    signedXml.AddReference(reference);

    // KeyInfo con RSAKeyValue y X509Data
    var keyInfo = new KeyInfo();
    keyInfo.AddClause(new RSAKeyValue(certificado.GetRSAPublicKey()));
    keyInfo.AddClause(new KeyInfoX509Data(certificado));
    signedXml.KeyInfo = keyInfo;

    signedXml.ComputeSignature();

    // Insertar firma como HERMANA del Documento (no hija)
    var dteNode = doc.GetElementsByTagName("DTE")[0] as XmlElement;
    dteNode.InsertAfter(doc.ImportNode(signedXml.GetXml(), true), documentoNode);

    return doc.OuterXml;
}
```

### 5.2 Firma del EnvioDTE (FirmaService.cs:147-261)

```csharp
public string FirmarEnvioDTE(string xmlEnvioDTE)
{
    var doc = new XmlDocument();
    doc.PreserveWhitespace = true;
    doc.LoadXml(xmlEnvioDTE);

    // Obtener SetDTE y asegurar ID="SetDoc"
    var setDTE = root.SelectSingleNode("*[local-name()='SetDTE']") as XmlElement;
    setDTE.Attributes["ID"].Value = "SetDoc";

    // Obtener certificado
    var certificado = RecuperarCertificado(nombreCertificado);

    // Construir SignedXml
    var signedXml = new SignedXml(doc)
    {
        SigningKey = certificado.GetRSAPrivateKey()
    };

    var reference = new Reference
    {
        Uri = "#SetDoc",
        DigestMethod = SignedXml.XmlDsigSHA1Url
    };
    reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
    signedXml.AddReference(reference);

    signedXml.ComputeSignature();

    // Agregar Signature al final de EnvioDTE
    root.AppendChild(doc.ImportNode(signedXml.GetXml(), true));

    return doc.OuterXml;
}
```

---

## 6. PASO 5: SIIService - Envío al SII

**Archivo:** `Services/SIIService.cs`

### 6.1 Obtener Semilla (SIIService.cs:72-155)

```csharp
public async Task<string> ObtenerSemillaAsync()
{
    var url = "https://maullin.sii.cl/DTEWS/CrSeed.jws";

    var soapRequest = @"<?xml version=""1.0"" encoding=""UTF-8""?>
    <soapenv:Envelope ...>
       <soapenv:Body>
          <def:getSeed .../>
       </soapenv:Body>
    </soapenv:Envelope>";

    // POST SOAP
    var response = await HttpRequest(url, soapRequest);

    // Parsear respuesta
    // <SEMILLA>155619883284</SEMILLA>
    return semilla;
}
```

### 6.2 Obtener Token (SIIService.cs:157-263)

```csharp
public async Task<string> ObtenerTokenAsync(string semilla)
{
    var url = "https://maullin.sii.cl/DTEWS/GetTokenFromSeed.jws";

    // Firmar semilla con certificado
    var semillaFirmada = FirmarSemilla(semilla, nombreCertificado);

    var soapRequest = @"<?xml version=""1.0"" encoding=""UTF-8""?>
    <soapenv:Envelope ...>
       <soapenv:Body>
          <def:getToken ...>
             <string>{semillaFirmada}</string>
          </def:getToken>
       </soapenv:Body>
    </soapenv:Envelope>";

    // POST SOAP
    var response = await HttpRequest(url, soapRequest);

    // Parsear respuesta
    // <TOKEN>58IALLMLV75EK</TOKEN>
    return token;
}
```

### 6.3 Enviar DTE (SIIService.cs:286-645)

```csharp
public async Task<EnvioResponse> EnviarDTEAsync(string xmlEnvioDTE, string token)
{
    // URL con parámetros de RUT
    var url = $"{urlBase}?RUTCOMPANY={rutEmpresa}&DVCOMPANY={dvEmpresa}" +
              $"&RUTUSER={rutUsuario}&DVUSER={dvUsuario}";
    // Ejemplo: https://maullin.sii.cl/cgi_dte/UPL/DTEUpload
    //          ?RUTCOMPANY=78301789&DVCOMPANY=K
    //          &RUTUSER=16238583&DVUSER=6

    // Multipart form-data
    var boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");

    // POST multipart con token en cookie
    request.Headers.Add("Cookie", $"TOKEN={token}");

    // Archivo XML en encoding ISO-8859-1
    var xmlBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(xmlEnvioDTE);

    // Enviar y procesar respuesta
    var respuesta = await PostMultipart(url, xmlBytes);

    return new EnvioResponse { TrackID = trackId, ... };
}
```

---

## 7. ESTRUCTURA XML FINAL DEL DTE

```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<DTE xmlns="http://www.sii.cl/SiiDte" version="1.0">
  <Documento ID="F1T33">
    <Encabezado>
      <IdDoc>
        <TipoDTE>33</TipoDTE>
        <Folio>1</Folio>
        <FchEmis>2026-01-30</FchEmis>
        <FmaPago>1</FmaPago>
      </IdDoc>
      <Emisor>
        <RUTEmisor>78301789-K</RUTEmisor>
        <RznSoc>SOFTWARE MAURICIO MORALES...</RznSoc>
        <GiroEmis>Actividades de programación...</GiroEmis>
        <Acteco>620100</Acteco>
        <DirOrigen>Santiago Watt 205</DirOrigen>
        <CmnaOrigen>RENAICO</CmnaOrigen>
        <CiudadOrigen>RENAICO</CiudadOrigen>
      </Emisor>
      <Receptor>
        <RUTRecep>66666666-6</RUTRecep>
        <RznSocRecep>EMPRESA PRUEBA SII</RznSocRecep>
        <GiroRecep>GIRO DE PRUEBA</GiroRecep>
        <DirRecep>CALLE PRUEBA 123</DirRecep>
        <CmnaRecep>SANTIAGO</CmnaRecep>
        <CiudadRecep>SANTIAGO</CiudadRecep>
      </Receptor>
      <Totales>
        <MntNeto>240950</MntNeto>
        <TasaIVA>19</TasaIVA>
        <IVA>45780</IVA>
        <MntTotal>286730</MntTotal>
      </Totales>
    </Encabezado>
    <Detalle>
      <NroLinDet>1</NroLinDet>
      <CdgItem>
        <TpoCodigo>INT1</TpoCodigo>
        <VlrCodigo>CAJON125</VlrCodigo>
      </CdgItem>
      <NmbItem>Cajon 125x1020</NmbItem>
      <QtyItem>176</QtyItem>
      <UnmdItem>UN</UnmdItem>
      <PrcItem>1290</PrcItem>
      <MontoItem>227040</MontoItem>
    </Detalle>
    <TED version="1.0">
      <DD>
        <RE>78301789-K</RE>
        <TD>33</TD>
        <F>1</F>
        <FE>2026-01-30</FE>
        <RR>66666666-6</RR>
        <RSR>EMPRESA PRUEBA SII</RSR>
        <MNT>286730</MNT>
        <IT1>Cajon 125x1020</IT1>
        <CAF version="1.0">...</CAF>
        <TSTED>2026-01-30T02:45:27</TSTED>
      </DD>
      <FRMT algoritmo="SHA1withRSA">n15Cu4dUNkr...</FRMT>
    </TED>
  </Documento>
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    <!-- Firma del Documento -->
  </Signature>
</DTE>
```

---

## 8. ESTRUCTURA XML FINAL DEL ENVIODTE

```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<EnvioDTE xmlns="http://www.sii.cl/SiiDte" version="1.0">
  <SetDTE ID="SetDoc">
    <Caratula version="1.0">
      <RutEmisor>78301789-K</RutEmisor>
      <RutEnvia>16238583-6</RutEnvia>
      <RutReceptor>60803000-K</RutReceptor>
      <FchResol>2025-12-03</FchResol>
      <NroResol>0</NroResol>
      <TmstFirmaEnv>2026-01-30T02:45:27</TmstFirmaEnv>
      <SubTotDTE>
        <TpoDTE>33</TpoDTE>
        <NroDTE>1</NroDTE>
      </SubTotDTE>
    </Caratula>
    <DTE>
      <!-- DTE firmado completo -->
    </DTE>
  </SetDTE>
  <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
    <!-- Firma del SetDTE -->
  </Signature>
</EnvioDTE>
```

---

## 9. PUNTOS CRÍTICOS A TENER EN CUENTA

| Aspecto | Requisito | Archivo/Línea |
|---------|-----------|---------------|
| **Encoding** | ISO-8859-1 para XML | XMLBuilderService.cs:98 |
| **TED - DD limpio** | Sin espacios entre tags | TEDService.cs:629 |
| **TED - Firma** | SHA1 + RSA con SignHash() | TEDService.cs:594 |
| **TED - BouncyCastle** | Para leer clave PEM | TEDService.cs:528 |
| **Totales** | DENTRO de Encabezado | XMLBuilderService.cs:339 |
| **TasaIVA** | Incluir si hay IVA > 0 | XMLBuilderService.cs:436 |
| **Detalles** | Múltiples `<Detalle>`, no wrapper | XMLBuilderService.cs:48-52 |
| **Firma DTE** | HERMANA de Documento | FirmaService.cs:121 |
| **RutEnvia** | RUT del certificado | XMLBuilderService.cs:160 |
| **RUTUSER** | RUT del certificado | SIIService.cs:316 |

---

## 10. DIAGRAMA DE SECUENCIA COMPLETO

```
Usuario          Controller        CAFService      TEDService      XMLBuilder      FirmaService    SIIService
   |                 |                 |               |               |               |              |
   |--EmitirFactura->|                 |               |               |               |              |
   |                 |--ObtenerCAF---->|               |               |               |              |
   |                 |<---CAFData------|               |               |               |              |
   |                 |                 |               |               |               |              |
   |                 |--GenerarTED----------------->|               |               |              |
   |                 |                 |               |--LimpiarDD--->|               |              |
   |                 |                 |               |--FirmarDD---->|               |              |
   |                 |<-----------TED XML--------------|               |               |              |
   |                 |                 |               |               |               |              |
   |                 |--ConstruirDTE----------------------------------->|               |              |
   |                 |<------------DTE XML------------------------------|               |              |
   |                 |                 |               |               |               |              |
   |                 |--FirmarDTE--------------------------------------------------->|              |
   |                 |<---------DTE Firmado------------------------------------------|              |
   |                 |                 |               |               |               |              |
   |                 |--ConstruirEnvioDTE------------------------------>|               |              |
   |                 |<----------EnvioDTE XML---------------------------|               |              |
   |                 |                 |               |               |               |              |
   |                 |--FirmarEnvioDTE---------------------------------------------->|              |
   |                 |<------EnvioDTE Firmado----------------------------------------|              |
   |                 |                 |               |               |               |              |
   |                 |--ObtenerSemilla----------------------------------------------------------->|
   |                 |<----------Semilla-------------------------------------------------------------|
   |                 |                 |               |               |               |              |
   |                 |--ObtenerToken--------------------------------------------------------------->|
   |                 |<----------Token---------------------------------------------------------------|
   |                 |                 |               |               |               |              |
   |                 |--EnviarDTE------------------------------------------------------------------>|
   |                 |<--------TrackID/Respuesta-----------------------------------------------------|
   |                 |                 |               |               |               |              |
   |<---Resultado----|                 |               |               |               |              |
```

---

## 11. GLOSARIO

| Término | Descripción |
|---------|-------------|
| **CAF** | Código de Autorización de Folios - Archivo XML del SII que autoriza emisión de documentos |
| **TED** | Timbre Electrónico Digital - Firma digital del documento con clave del CAF |
| **DTE** | Documento Tributario Electrónico - Factura, boleta, nota de crédito, etc. |
| **EnvioDTE** | Sobre que contiene uno o más DTEs firmados para enviar al SII |
| **SetDTE** | Conjunto de DTEs dentro del EnvioDTE |
| **Carátula** | Metadatos del envío (RUTs, fecha resolución, etc.) |
| **RutEmisor** | RUT de la empresa que emite (78301789-K) |
| **RutEnvia** | RUT de la persona que envía/firma (16238583-6) |
| **RutReceptor** | RUT del SII (60803000-K) o del cliente |
| **FchResol** | Fecha de resolución SII que autoriza facturación electrónica |
| **NroResol** | Número de resolución (0 para certificación) |
| **Maullin** | Ambiente de certificación del SII |
| **Palena** | Ambiente de producción del SII |

---

*Documento generado el 2026-01-30*
*Sistema: FacturacionElectronicaSII*
