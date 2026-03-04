# 🔒 AUDITORÍA DE SEGURIDAD Y CALIDAD - .NET 8

**Fecha:** 2026-01-19  
**Arquitecto:** Senior Software Architect  
**Framework:** .NET 8.0  
**Alcance:** Revisión línea por línea de código crítico

---

## 🛑 CRÍTICO: VULNERABILIDADES Y ERRORES QUE ROMPERÁN LA APP

### 1. 🛑 **CRÍTICO: Parseo XML sin sanitización en múltiples lugares**

#### **Ubicación:** `SIIService.cs` - Líneas 81, 90, 165, 175, 259, 284

**Problema:**
```csharp
// Línea 81
soap.LoadXml(xmlResp);  // ❌ Sin validar que xmlResp sea XML válido

// Línea 90
xml.LoadXml(innerXml);  // ❌ innerXml puede contener HTML corrupto

// Línea 259
doc.LoadXml(respuesta);  // ❌ respuesta puede tener HTML mezclado
```

**Riesgo:** 
- XML Injection
- Denial of Service (DoS) por XML malformado
- Excepciones no controladas que rompen la aplicación

**Solución:**
```csharp
// ✅ SOLUCIÓN SEGURA
private XmlDocument CargarXMLSeguro(string xmlString, string contexto)
{
    if (string.IsNullOrWhiteSpace(xmlString))
        throw new ArgumentException($"El XML está vacío en {contexto}");
    
    // Sanitizar antes de parsear
    var xmlSanitizado = SanitizarXML(xmlString);
    
    var doc = new XmlDocument { PreserveWhitespace = true };
    try
    {
        doc.LoadXml(xmlSanitizado);
        return doc;
    }
    catch (XmlException ex)
    {
        _logger.LogError(ex, "Error al parsear XML en {Contexto}. XML: {Xml}", contexto, 
            xmlString.Length > 500 ? xmlString.Substring(0, 500) : xmlString);
        throw new InvalidOperationException($"XML inválido en {contexto}: {ex.Message}", ex);
    }
}

private string SanitizarXML(string xml)
{
    // Eliminar BOM y caracteres de control
    xml = xml.TrimStart('\uFEFF', '\u200B');
    
    // Eliminar HTML corrupto
    var htmlTags = new[] { "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", "html", "head", "body", "th" };
    foreach (var tag in htmlTags)
    {
        xml = Regex.Replace(xml, $@"<{tag}\b[^>]*>", "", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, $@"</{tag}>", "", RegexOptions.IgnoreCase);
        xml = Regex.Replace(xml, $@"<{tag}\s*/>", "", RegexOptions.IgnoreCase);
    }
    
    return xml;
}
```

---

### 2. 🛑 **CRÍTICO: NullReferenceException en SelectSingleNode sin validación**

#### **Ubicación:** `SIIService.cs` - Líneas 83, 92, 93, 168, 177, 178, 293, 294

**Problema:**
```csharp
// Línea 83
var innerEscaped = soap.SelectSingleNode("//*[local-name()='getSeedReturn']")?.InnerText;
// ❌ Si InnerText es null, puede causar problemas downstream

// Línea 293
var estado = doc.SelectSingleNode("//*[local-name()='ESTADO']")?.InnerText;
var trackId = doc.SelectSingleNode("//*[local-name()='TRACKID']")?.InnerText;
// ❌ No valida si estado o trackId son null antes de usar
```

**Riesgo:** NullReferenceException en producción

**Solución:**
```csharp
// ✅ SOLUCIÓN SEGURA
var estadoNode = doc.SelectSingleNode("//*[local-name()='ESTADO']");
var estado = estadoNode?.InnerText?.Trim();
if (string.IsNullOrEmpty(estado))
{
    _logger.LogError("No se encontró nodo ESTADO en respuesta del SII");
    throw new InvalidOperationException("Respuesta del SII inválida: falta nodo ESTADO");
}

var trackIdNode = doc.SelectSingleNode("//*[local-name()='TRACKID']");
var trackId = trackIdNode?.InnerText?.Trim() ?? "";
```

---

### 3. 🛑 **CRÍTICO: SQL Injection potencial en consultas dinámicas**

#### **Ubicación:** `CAFService.cs` - Línea 146-156

**Problema:**
```csharp
// Aunque usa parámetros, no valida el tipoDTE antes de usarlo
command.Parameters.AddWithValue("@TipoDTE", tipoDTE);
// ❌ Si tipoDTE viene de input no validado, puede ser problemático
```

**Riesgo:** Aunque usa parámetros, falta validación de entrada

**Solución:**
```csharp
// ✅ SOLUCIÓN SEGURA
if (tipoDTE <= 0 || tipoDTE > 999)
{
    throw new ArgumentException($"Tipo DTE inválido: {tipoDTE}", nameof(tipoDTE));
}

using var command = new MySqlCommand(query, connection);
command.Parameters.Add("@TipoDTE", MySqlDbType.Int32).Value = tipoDTE;
```

---

### 4. 🛑 **CRÍTICO: Manejo de excepciones en llamadas externas sin logging adecuado**

#### **Ubicación:** `DTEService.cs` - Línea 139-145

**Problema:**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error al emitir DTE");
    response.Errores.Add($"Error interno: {ex.Message}");  // ❌ Expone mensaje interno
    response.Mensaje = "Error al procesar la emisión";
    return response;
}
```

**Riesgo:** 
- Exposición de información sensible
- No diferencia entre errores transitorios y permanentes

**Solución:**
```csharp
// ✅ SOLUCIÓN SEGURA
catch (ArgumentException argEx)
{
    _logger.LogWarning(argEx, "Error de validación al emitir DTE");
    response.Errores.Add(argEx.Message);
    response.Mensaje = "Error de validación";
    return response;
}
catch (InvalidOperationException invEx)
{
    _logger.LogError(invEx, "Error de operación al emitir DTE");
    response.Errores.Add("Error al procesar la emisión. Por favor, intente nuevamente.");
    response.Mensaje = "Error al procesar la emisión";
    return response;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error inesperado al emitir DTE. Tipo: {Tipo}, Mensaje: {Mensaje}", 
        ex.GetType().Name, ex.Message);
    response.Errores.Add("Error interno del servidor. Contacte al administrador.");
    response.Mensaje = "Error al procesar la emisión";
    return response;
}
```

---

### 5. 🛑 **CRÍTICO: Path hardcodeado con riesgo de seguridad**

#### **Ubicación:** `CAFService.cs` - Línea 67

**Problema:**
```csharp
string rutaCAFDirecto = @"C:\Users\ComercioIsabel\Music\Comercial_isabel\Comercial_isabel\bin\Debug\FoliosSII84513353312026119258.xml";
// ❌ Path hardcodeado, no funciona en otros ambientes
// ❌ No valida permisos de lectura
// ❌ No maneja excepciones de acceso a archivo
```

**Riesgo:** 
- Falla en producción
- Problemas de permisos
- No portable

**Solución:**
```csharp
// ✅ SOLUCIÓN SEGURA
private string GetRutaCAFDirecto()
{
    var rutaBase = _configuration["FacturacionElectronica:CAF:RutaArchivo"] 
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAF");
    
    if (!Directory.Exists(rutaBase))
    {
        Directory.CreateDirectory(rutaBase);
    }
    
    var nombreArchivo = _configuration["FacturacionElectronica:CAF:NombreArchivo"] 
        ?? "FoliosSII.xml";
    
    return Path.Combine(rutaBase, nombreArchivo);
}
```

---

## ⚠️ ADVERTENCIAS: MEJORAS RECOMENDADAS

### 6. ⚠️ **Lógica de negocio en el Controlador**

#### **Ubicación:** `DTEController.cs` - Línea 40-47

**Problema:**
```csharp
var response = await _dteService.EmitirDocumentoAsync(request);

if (!response.Exito)  // ❌ Lógica de negocio en controlador
{
    return BadRequest(response);
}
```

**Recomendación:** El controlador debería solo delegar. La lógica de respuesta HTTP debería estar en el servicio o usar un resultado pattern.

**Solución:**
```csharp
// ✅ SOLUCIÓN MEJORADA
[HttpPost("emitir")]
public async Task<ActionResult<EmitirDTEResponse>> EmitirDTE([FromBody] EmitirDTERequest request)
{
    if (request == null)
        return BadRequest("Request no puede ser nulo");

    var response = await _dteService.EmitirDocumentoAsync(request);
    
    // El servicio ya maneja la lógica de éxito/error
    return response.Exito 
        ? Ok(response) 
        : BadRequest(response);
}
```

---

### 7. ⚠️ **Falta validación de entrada en endpoints**

#### **Ubicación:** `DTEController.cs` - Línea 58-68

**Problema:**
```csharp
public async Task<ActionResult<EstadoEnvioResponse>> ConsultarEstado(string trackId)
{
    if (string.IsNullOrWhiteSpace(trackId))
    {
        return BadRequest("TrackID es requerido");
    }
    // ❌ No valida formato del trackId
    // ❌ No valida longitud máxima
}
```

**Solución:**
```csharp
// ✅ SOLUCIÓN MEJORADA
public async Task<ActionResult<EstadoEnvioResponse>> ConsultarEstado(string trackId)
{
    if (string.IsNullOrWhiteSpace(trackId))
        return BadRequest("TrackID es requerido");
    
    if (trackId.Length > 50)
        return BadRequest("TrackID excede longitud máxima");
    
    if (!Regex.IsMatch(trackId, @"^[A-Za-z0-9\-]+$"))
        return BadRequest("TrackID contiene caracteres inválidos");
    
    var estado = await _dteService.ConsultarEstadoAsync(trackId);
    return Ok(estado);
}
```

---

### 8. ⚠️ **Falta manejo de transacciones en operaciones de BD**

#### **Ubicación:** `CAFService.cs` - Líneas 124, 224

**Problema:**
```csharp
// Marcar folio como usado - no está en transacción
await _cafService.MarcarFolioUsadoAsync(request.TipoDTE, folio);
// ❌ Si falla después, el folio queda marcado pero el DTE no se emitió
```

**Solución:**
```csharp
// ✅ SOLUCIÓN MEJORADA
public async Task MarcarFolioUsadoAsync(int tipoDTE, int folio)
{
    using var connection = new MySqlConnection(GetConnectionString());
    await connection.OpenAsync();
    
    using var transaction = await connection.BeginTransactionAsync();
    try
    {
        // Marcar folio como usado
        var updateQuery = @"
            UPDATE folios 
            SET Usado = 1, FechaUso = @FechaUso 
            WHERE TipoDTE = @TipoDTE AND Folio = @Folio";
        
        using var command = new MySqlCommand(updateQuery, connection, transaction);
        command.Parameters.AddWithValue("@TipoDTE", tipoDTE);
        command.Parameters.AddWithValue("@Folio", folio);
        command.Parameters.AddWithValue("@FechaUso", DateTime.Now);
        
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

---

### 9. ⚠️ **Falta validación de certificado antes de usar**

#### **Ubicación:** `FirmaService.cs` - Línea 50, `SIIService.cs` - Línea 380

**Problema:**
```csharp
var certificado = RecuperarCertificado(nombreCertificado);
if (certificado != null)  // ❌ Solo verifica null, no valida expiración
{
    // Usa certificado sin validar si está expirado
}
```

**Solución:**
```csharp
// ✅ SOLUCIÓN MEJORADA
private X509Certificate2 RecuperarCertificado(string nombreCertificado)
{
    var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    
    try
    {
        var resultados = store.Certificates.Find(
            X509FindType.FindBySubjectName, nombreCertificado, false);
        
        if (resultados.Count == 0)
            throw new InvalidOperationException($"Certificado no encontrado: {nombreCertificado}");
        
        var certificado = resultados[0];
        
        // Validar expiración
        if (certificado.NotAfter < DateTime.Now)
            throw new InvalidOperationException($"Certificado expirado: {nombreCertificado}. Expira: {certificado.NotAfter}");
        
        // Validar que tenga clave privada
        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException($"Certificado no tiene clave privada: {nombreCertificado}");
        
        // Validar que no esté revocado (opcional pero recomendado)
        // TODO: Implementar validación de CRL si es necesario
        
        _logger.LogInformation("Certificado válido encontrado. Expira: {FechaExpiracion}", certificado.NotAfter);
        return certificado;
    }
    finally
    {
        store.Close();
    }
}
```

---

### 10. ⚠️ **Falta sanitización de entrada en construcción de XML**

#### **Ubicación:** `XMLBuilderService.cs` - Múltiples lugares

**Problema:**
```csharp
// Si los valores vienen de input del usuario, pueden contener caracteres XML especiales
// que rompan la estructura XML o causen XML Injection
```

**Solución:**
```csharp
// ✅ SOLUCIÓN SEGURA
private string EscaparXML(string valor)
{
    if (string.IsNullOrEmpty(valor))
        return string.Empty;
    
    return SecurityElement.Escape(valor);
}

// Usar en construcción de XML:
var razonSocial = EscaparXML(receptor.RznSocRecep);
```

---

## ✅ SOLUCIONES REFACTORIZADAS

### Servicio de Sanitización XML Centralizado

```csharp
// ✅ NUEVO SERVICIO: XMLSanitizationService.cs
public interface IXMLSanitizationService
{
    XmlDocument CargarXMLSeguro(string xmlString, string contexto);
    string SanitizarXML(string xml);
    string EscaparXML(string valor);
    bool ValidarEstructuraXML(XmlDocument doc, string esquemaEsperado);
}

public class XMLSanitizationService : IXMLSanitizationService
{
    private readonly ILogger<XMLSanitizationService> _logger;
    private static readonly string[] HtmlTagsToRemove = 
        { "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", "html", "head", "body", "th" };
    
    public XMLSanitizationService(ILogger<XMLSanitizationService> logger)
    {
        _logger = logger;
    }
    
    public XmlDocument CargarXMLSeguro(string xmlString, string contexto)
    {
        if (string.IsNullOrWhiteSpace(xmlString))
            throw new ArgumentException($"El XML está vacío en {contexto}", nameof(xmlString));
        
        var xmlSanitizado = SanitizarXML(xmlString);
        var doc = new XmlDocument { PreserveWhitespace = true };
        
        try
        {
            doc.LoadXml(xmlSanitizado);
            return doc;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "Error al parsear XML en {Contexto}", contexto);
            var snippet = xmlString.Length > 500 ? xmlString.Substring(0, 500) : xmlString;
            throw new InvalidOperationException(
                $"XML inválido en {contexto}: {ex.Message}. Inicio: {snippet}", ex);
        }
    }
    
    public string SanitizarXML(string xml)
    {
        if (string.IsNullOrEmpty(xml))
            return string.Empty;
        
        // Eliminar BOM y caracteres de control
        xml = xml.TrimStart('\uFEFF', '\u200B');
        
        // Eliminar HTML corrupto
        foreach (var tag in HtmlTagsToRemove)
        {
            xml = Regex.Replace(xml, $@"<{tag}\b[^>]*>", "", RegexOptions.IgnoreCase);
            xml = Regex.Replace(xml, $@"</{tag}>", "", RegexOptions.IgnoreCase);
            xml = Regex.Replace(xml, $@"<{tag}\s*/>", "", RegexOptions.IgnoreCase);
        }
        
        // Eliminar <td> solo si no está en contexto XML válido (<TD>)
        if (xml.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
            !Regex.IsMatch(xml, @"<TD>\d+</TD>", RegexOptions.IgnoreCase))
        {
            xml = Regex.Replace(xml, @"<td\b[^>]*>", "", RegexOptions.IgnoreCase);
            xml = Regex.Replace(xml, @"</td>", "", RegexOptions.IgnoreCase);
        }
        
        return xml;
    }
    
    public string EscaparXML(string valor)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;
        
        return SecurityElement.Escape(valor);
    }
    
    public bool ValidarEstructuraXML(XmlDocument doc, string esquemaEsperado)
    {
        // TODO: Implementar validación contra XSD si es necesario
        return doc.DocumentElement != null;
    }
}
```

---

### Mejora del Manejo de Excepciones

```csharp
// ✅ NUEVA CLASE: CustomExceptionHandler.cs
public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CustomExceptionHandler> _logger;
    
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var response = exception switch
        {
            ArgumentException argEx => new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = argEx.Message,
                Type = "ValidationError"
            },
            InvalidOperationException invEx => new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Error en la operación solicitada",
                Type = "OperationError"
            },
            XmlException xmlEx => new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Error al procesar XML",
                Type = "XMLProcessingError"
            },
            _ => new ErrorResponse
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = "Error interno del servidor",
                Type = "InternalError"
            }
        };
        
        _logger.LogError(exception, "Error procesado: {Tipo}, Mensaje: {Mensaje}", 
            response.Type, exception.Message);
        
        httpContext.Response.StatusCode = response.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        
        return true;
    }
}

public record ErrorResponse
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
```

---

## 📊 RESUMEN DE VULNERABILIDADES

| Severidad | Cantidad | Estado |
|-----------|----------|--------|
| 🛑 Crítico | 5 | Requiere corrección inmediata |
| ⚠️ Advertencia | 5 | Mejora recomendada |
| ✅ Implementado | 0 | Soluciones propuestas listas |

---

## 🔧 PLAN DE ACCIÓN PRIORITARIO

1. **URGENTE:** Implementar sanitización XML centralizada
2. **URGENTE:** Agregar validación de null en todos los SelectSingleNode
3. **URGENTE:** Mover path hardcodeado a configuración
4. **ALTA:** Mejorar manejo de excepciones con tipos específicos
5. **MEDIA:** Agregar validación de certificados
6. **MEDIA:** Implementar transacciones en operaciones de BD

---

**Generado:** 2026-01-19  
**Revisado por:** Senior Software Architect
