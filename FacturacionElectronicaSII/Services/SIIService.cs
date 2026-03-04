using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.DTO;
using FacturacionElectronicaSII.Models.SII;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using X509Certificates = System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.IO;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio real para comunicación con el SII
    /// Basado en código funcional probado
    /// </summary>
    public class SIIService : ISIIService
    {
        private readonly ILogger<SIIService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IFirmaService _firmaService;

        private readonly string _respuestasSiiPath;
        private readonly string _documentosEnviadosPath;

        public SIIService(ILogger<SIIService> logger, IConfiguration configuration, IFirmaService firmaService)
        {
            _logger = logger;
            _configuration = configuration;
            _firmaService = firmaService;
            
            // Crear directorio para respuestas del SII
            _respuestasSiiPath = Path.Combine(Directory.GetCurrentDirectory(), "RespuestasSII");
            if (!Directory.Exists(_respuestasSiiPath))
            {
                Directory.CreateDirectory(_respuestasSiiPath);
            }
            
            // Crear directorio para documentos enviados al SII
            _documentosEnviadosPath = Path.Combine(Directory.GetCurrentDirectory(), "DocumentosEnviadosSII");
            if (!Directory.Exists(_documentosEnviadosPath))
            {
                Directory.CreateDirectory(_documentosEnviadosPath);
            }
        }

        /// <summary>
        /// Guarda una respuesta del SII en un archivo con timestamp
        /// </summary>
        private void GuardarRespuestaSII(string tipoOperacion, string respuesta, string contexto = "")
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var nombreArchivo = $"{tipoOperacion}_{timestamp}.txt";
                if (!string.IsNullOrEmpty(contexto))
                {
                    nombreArchivo = $"{tipoOperacion}_{contexto}_{timestamp}.txt";
                }
                
                var rutaCompleta = Path.Combine(_respuestasSiiPath, nombreArchivo);
                File.WriteAllText(rutaCompleta, respuesta, Encoding.UTF8);
                _logger.LogInformation("Respuesta del SII guardada en: {Ruta}", rutaCompleta);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo guardar la respuesta del SII en archivo");
            }
        }

        public async Task<string> ObtenerSemillaAsync()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [SEMILLA_INI] Llamando a CrSeed.getSeed() vía HttpWebRequest", timestamp);

            try
            {
                // Configurar protocolos de seguridad
                ServicePointManager.SecurityProtocol = 
                    SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion";
                var url = ambiente == "Produccion"
                    ? _configuration["SII:Produccion:UrlSemilla"] ?? "https://palena.sii.cl/DTEWS/CrSeed.jws"
                    : _configuration["SII:Certificacion:UrlSemilla"] ?? "https://maullin.sii.cl/DTEWS/CrSeed.jws";

                _logger.LogInformation("[{Timestamp}] [SEMILLA_REQ] {Url}", timestamp, url);

                var soapRequest = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" 
                  xmlns:def=""http://DefaultNamespace"">
   <soapenv:Header/>
   <soapenv:Body>
      <def:getSeed soapenv:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/""/>
   </soapenv:Body>
</soapenv:Envelope>";

                _logger.LogInformation("[{Timestamp}] [SEMILLA_REQ_XML] {SoapRequest}", timestamp, soapRequest);

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "text/xml; charset=utf-8";
                request.Headers.Add("SOAPAction", "");

                var bytes = Encoding.UTF8.GetBytes(soapRequest);
                using (var reqStream = await request.GetRequestStreamAsync())
                {
                    await reqStream.WriteAsync(bytes, 0, bytes.Length);
                }

                string xmlResp;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    xmlResp = await reader.ReadToEndAsync();
                }

                // Guardar respuesta del SII
                GuardarRespuestaSII("Semilla", xmlResp);

                _logger.LogInformation("[{Timestamp}] [SEMILLA_RAW] {XmlResp}", timestamp, xmlResp);

                // Parsear respuesta SOAP
                var soap = new XmlDocument();
                soap.LoadXml(xmlResp);

                var innerEscaped = soap.SelectSingleNode("//*[local-name()='getSeedReturn']")?.InnerText;
                _logger.LogInformation("[{Timestamp}] [SEMILLA_INNER_ESCAPED] {InnerEscaped}", timestamp, innerEscaped);
                
                var innerXml = System.Net.WebUtility.HtmlDecode(innerEscaped ?? "");
                _logger.LogInformation("[{Timestamp}] [SEMILLA_INNER_XML] {InnerXml}", timestamp, innerXml);

                var xml = new XmlDocument();
                xml.LoadXml(innerXml);

                var estado = xml.SelectSingleNode("//*[local-name()='ESTADO']")?.InnerText;
                var semilla = xml.SelectSingleNode("//*[local-name()='SEMILLA']")?.InnerText;

                _logger.LogInformation("[{Timestamp}] [SEMILLA_PARSE] ESTADO={Estado} SEMILLA={Semilla}", timestamp, estado, semilla);

                if (estado != "00" || string.IsNullOrEmpty(semilla))
                {
                    throw new InvalidOperationException($"Error al obtener semilla. Estado: {estado}");
                }

                _logger.LogInformation("[{Timestamp}] [SEMILLA_OK] {Semilla}", timestamp, semilla);
                return semilla;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener semilla del SII");
                throw;
            }
        }

        public async Task<string> ObtenerTokenAsync(string semilla)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [TOKEN_SEMILLA_OK] {Semilla}", timestamp, semilla);

            try
            {
                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion";
                var url = ambiente == "Produccion"
                    ? _configuration["SII:Produccion:UrlToken"] ?? "https://palena.sii.cl/DTEWS/GetTokenFromSeed.jws"
                    : _configuration["SII:Certificacion:UrlToken"] ?? "https://maullin.sii.cl/DTEWS/GetTokenFromSeed.jws";

                // Firmar semilla
                var nombreCertificado = _configuration["FacturacionElectronica:Certificado:Nombre"] ?? "";
                var semillaFirmadaXml = FirmarSemilla(semilla, nombreCertificado);
                _logger.LogInformation("[{Timestamp}] [TOKEN_FIRMA_OK] {SemillaFirmadaXml}", timestamp, semillaFirmadaXml);

                var soapReq = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/""
                  xmlns:def=""http://DefaultNamespace"">
   <soapenv:Header/>
   <soapenv:Body>
      <def:getToken soapenv:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
         <string>{System.Security.SecurityElement.Escape(semillaFirmadaXml)}</string>
      </def:getToken>
   </soapenv:Body>
</soapenv:Envelope>";

                _logger.LogInformation("[{Timestamp}] [TOKEN_XML_FIRMADO] {SoapReq}", timestamp, soapReq);
                _logger.LogInformation("[{Timestamp}] [TOKEN_REQ_URL] {Url}", timestamp, url);
                _logger.LogInformation("[{Timestamp}] [TOKEN_REQ_SOAP] {SoapReq}", timestamp, soapReq);

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "text/xml; charset=UTF-8";
                req.Headers.Add("SOAPAction", "");

                var bytes = Encoding.UTF8.GetBytes(soapReq);
                using (var reqStream = await req.GetRequestStreamAsync())
                {
                    await reqStream.WriteAsync(bytes, 0, bytes.Length);
                }

                string xmlResp;
                HttpWebResponse? httpResponse = null;
                try
                {
                    httpResponse = (HttpWebResponse)await req.GetResponseAsync();
                    using (var reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                    {
                        xmlResp = await reader.ReadToEndAsync();
                    }
                }
                catch (WebException wex)
                {
                    // Capturar respuesta incluso si hay error
                    if (wex.Response != null)
                    {
                        using (var reader = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                        {
                            xmlResp = await reader.ReadToEndAsync();
                        }
                        GuardarRespuestaSII("Token_Error", xmlResp, $"HTTP_{((HttpWebResponse)wex.Response).StatusCode}");
                        _logger.LogError(wex, "[{Timestamp}] [TOKEN_ERROR] {XmlResp}", timestamp, xmlResp);
                        throw;
                    }
                    throw;
                }

                // Guardar respuesta del SII
                GuardarRespuestaSII("Token", xmlResp, $"HTTP_{httpResponse?.StatusCode ?? 0}");

                _logger.LogInformation("[{Timestamp}] [TOKEN_RESP_RAW] {XmlResp}", timestamp, xmlResp);

                // Parsear respuesta
                var soap = new XmlDocument();
                soap.LoadXml(xmlResp);

                var innerEscaped = soap.SelectSingleNode("//*[local-name()='getTokenReturn']")?.InnerText;
                _logger.LogInformation("[{Timestamp}] [TOKEN_ESCAPED_XML] {InnerEscaped}", timestamp, innerEscaped);
                
                var innerXml = System.Net.WebUtility.HtmlDecode(innerEscaped ?? "");
                _logger.LogInformation("[{Timestamp}] [TOKEN_INNER_XML] {InnerXml}", timestamp, innerXml);

                var xml = new XmlDocument();
                xml.LoadXml(innerXml);

                var estado = xml.SelectSingleNode("//*[local-name()='ESTADO']")?.InnerText;
                var token = xml.SelectSingleNode("//*[local-name()='TOKEN']")?.InnerText;
                var glosa = xml.SelectSingleNode("//*[local-name()='GLOSA']")?.InnerText;

                _logger.LogInformation("[{Timestamp}] [TOKEN_PARSE] ESTADO={Estado} TOKEN={Token}", timestamp, estado, token);

                if (estado != "00" || string.IsNullOrEmpty(token))
                {
                    throw new InvalidOperationException($"Error al obtener token. Estado: {estado}");
                }

                _logger.LogInformation("[{Timestamp}] [TOKEN_OK] Token obtenido correctamente.", timestamp);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener token del SII");
                throw;
            }
        }

        /// <summary>
        /// Guarda el XML del documento enviado al SII
        /// </summary>
        private void GuardarDocumentoEnviado(string xmlEnvioDTE, string tipoOperacion = "EnvioDTE")
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var nombreArchivo = $"{tipoOperacion}_{timestamp}.xml";
                var rutaCompleta = Path.Combine(_documentosEnviadosPath, nombreArchivo);
                
                // Guardar con encoding ISO-8859-1 (como se envía al SII)
                File.WriteAllText(rutaCompleta, xmlEnvioDTE, Encoding.GetEncoding("ISO-8859-1"));
                _logger.LogInformation("Documento enviado al SII guardado en: {Ruta}", rutaCompleta);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo guardar el documento enviado al SII en archivo");
            }
        }

        public async Task<EnvioResponse> EnviarDTEAsync(string xmlEnvioDTE, string token)
        {
            _logger.LogInformation("Enviando DTE al SII");

            try
            {
                // CRÍTICO: Guardar el documento que se envía al SII
                GuardarDocumentoEnviado(xmlEnvioDTE, "EnvioDTE");
                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion";
                var urlBase = ambiente == "Produccion"
                    ? _configuration["SII:Produccion:UrlUpload"] ?? "https://palena.sii.cl/cgi_dte/UPL/DTEUpload"
                    : _configuration["SII:Certificacion:UrlUpload"] ?? "https://maullin.sii.cl/cgi_dte/UPL/DTEUpload";

                // Extraer RUT del emisor (empresa) de la configuración
                var rutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "";
                var rutEmisorParts = rutEmisor.Split('-');
                if (rutEmisorParts.Length != 2)
                {
                    throw new InvalidOperationException("RUT del emisor no válido");
                }

                // Extraer RUT del usuario que envía (dueño del certificado)
                var rutEnvia = _configuration["FacturacionElectronica:Certificado:RUT"] ?? rutEmisor;
                var rutEnviaParts = rutEnvia.Split('-');
                if (rutEnviaParts.Length != 2)
                {
                    throw new InvalidOperationException("RUT del certificado no válido");
                }

                _logger.LogInformation("[UPLOAD_URL] {Url}", urlBase);

                var boundary = "7d23e2a11301c4";

                var request = (HttpWebRequest)WebRequest.Create(urlBase);
                request.Method = "POST";
                request.Accept = "image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, application/vnd.ms-powerpoint, application/ms-excel, application/msword, */*";
                request.Referer = "www.sii.cl";
                request.ContentType = $"multipart/form-data; boundary={boundary}";
                request.UserAgent = "Mozilla/4.0 (compatible; PROG 1.0; Windows NT 5.0; YComp 5.0.2.4)";
                request.KeepAlive = true;
                request.Headers.Add("Accept-Language", "es-cl");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("Cache-Control", "no-cache");
                request.Headers.Add("Cookie", $"TOKEN={token}");

                var fileName = $"DTE_{DateTime.Now:yyyyMMddHHmmss}.xml";

                // Construir multipart con RUTs en el body (formato correcto SII)
                var sb = new StringBuilder();
                sb.Append($"--{boundary}\r\n");
                sb.Append("Content-Disposition: form-data; name=\"rutSender\"\r\n\r\n");
                sb.Append(rutEnviaParts[0] + "\r\n");
                sb.Append($"--{boundary}\r\n");
                sb.Append("Content-Disposition: form-data; name=\"dvSender\"\r\n\r\n");
                sb.Append(rutEnviaParts[1] + "\r\n");
                sb.Append($"--{boundary}\r\n");
                sb.Append("Content-Disposition: form-data; name=\"rutCompany\"\r\n\r\n");
                sb.Append(rutEmisorParts[0] + "\r\n");
                sb.Append($"--{boundary}\r\n");
                sb.Append("Content-Disposition: form-data; name=\"dvCompany\"\r\n\r\n");
                sb.Append(rutEmisorParts[1] + "\r\n");
                sb.Append($"--{boundary}\r\n");
                sb.Append($"Content-Disposition: form-data; name=\"archivo\"; filename=\"{fileName}\"\r\n");
                sb.Append("Content-Type: text/xml\r\n\r\n");
                sb.Append(xmlEnvioDTE);
                sb.Append("\r\n");
                sb.Append($"--{boundary}--\r\n");

                var bodyBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(sb.ToString());
                request.ContentLength = bodyBytes.Length;

                using (var requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                }

                string respuesta;
                HttpWebResponse? httpResponse = null;
                try
                {
                    httpResponse = (HttpWebResponse)await request.GetResponseAsync();
                    using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        respuesta = await reader.ReadToEndAsync();
                    }
                }
                catch (WebException wex)
                {
                    // Capturar respuesta incluso si hay error
                    if (wex.Response != null)
                    {
                        using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                        {
                            respuesta = await reader.ReadToEndAsync();
                        }
                        GuardarRespuestaSII("EnvioDTE_Error", respuesta, $"HTTP_{((HttpWebResponse)wex.Response).StatusCode}");
                        _logger.LogError(wex, "Error al recibir respuesta del SII: {Respuesta}", respuesta);
                        throw;
                    }
                    throw;
                }

                // Guardar respuesta del SII (siempre, éxito o error)
                GuardarRespuestaSII("EnvioDTE", respuesta, $"HTTP_{httpResponse?.StatusCode ?? 0}");

                // Parsear respuesta
                // CRÍTICO: Validar que la respuesta no contenga HTML corrupto antes de parsear
                _logger.LogInformation("Respuesta del SII recibida (primeros 500 caracteres): {Respuesta}", 
                    respuesta.Length > 500 ? respuesta.Substring(0, 500) : respuesta);
                _logger.LogInformation("Longitud total de la respuesta: {Length} caracteres", respuesta.Length);
                
                // Detectar si la respuesta es HTML puro (sin XML válido)
                bool esHTMLPuro = (respuesta.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                                  respuesta.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                                  (respuesta.Contains("<html", StringComparison.OrdinalIgnoreCase) && 
                                   !respuesta.Contains("<soapenv:Envelope", StringComparison.OrdinalIgnoreCase) &&
                                   !respuesta.Contains("<RESPUESTA", StringComparison.OrdinalIgnoreCase)));
                
                if (esHTMLPuro)
                {
                    _logger.LogWarning("El SII devolvió una respuesta HTML pura (sin XML válido). Extrayendo información...");
                    
                    // Intentar extraer TrackID del HTML (puede estar en respuestas exitosas)
                    string trackIdHtml = "";
                    var matchTrackId = System.Text.RegularExpressions.Regex.Match(respuesta, 
                        @"(?:Identificador\s+de\s+env[íi]o|TrackID|trackId)[\s:]*<strong>(\d+)</strong>", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (matchTrackId.Success)
                    {
                        trackIdHtml = matchTrackId.Groups[1].Value;
                        _logger.LogInformation("TrackID extraído del HTML: {TrackID}", trackIdHtml);
                    }
                    
                    // Verificar si es una respuesta exitosa (DOCUMENTO RECIBIDO)
                    bool esExitoso = respuesta.Contains("DOCUMENTO TRIBUTARIO ELECTRONICO RECIBIDO", StringComparison.OrdinalIgnoreCase) ||
                                     (respuesta.Contains("RECIBIDO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(trackIdHtml));
                    
                    // Intentar extraer información del HTML
                    string mensajeError = "El SII rechazó la solicitud";
                    string codigoError = "";
                    string glosa = "";
                    
                    if (esExitoso)
                    {
                        mensajeError = "DOCUMENTO TRIBUTARIO ELECTRONICO RECIBIDO";
                        glosa = "El documento fue recibido correctamente por el SII";
                        
                        // Intentar extraer fecha de recepción
                        var matchFecha = System.Text.RegularExpressions.Regex.Match(respuesta, 
                            @"(\d{4}-\d{2}-\d{2}),?\s+a\s+las\s+(\d{2}:\d{2}:\d{2})", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (matchFecha.Success)
                        {
                            var fechaStr = $"{matchFecha.Groups[1].Value} {matchFecha.Groups[2].Value}";
                            if (DateTime.TryParse(fechaStr, out DateTime fechaRecepcionHtml))
                            {
                                _logger.LogInformation("Fecha de recepción extraída: {Fecha}", fechaRecepcionHtml);
                            }
                        }
                    }
                    else if (respuesta.Contains("NO ESTA AUTOR", StringComparison.OrdinalIgnoreCase))
                    {
                        mensajeError = "La empresa no está autorizada para emitir documentos electrónicos en el SII";
                        codigoError = "NO_AUTORIZADA";
                        glosa = "LA EMPRESA INGRESADA NO ESTA AUTORIZADA A REALIZAR ENVIOS DE ARCHIVOS DE DOCUMENTOS ELECTRONICOS";
                    }
                    else if (respuesta.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        // Intentar extraer el texto del error
                        var matchError = System.Text.RegularExpressions.Regex.Match(respuesta, 
                            @"<font[^>]*>([^<]*ERROR[^<]*)</font>", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (matchError.Success)
                        {
                            mensajeError = System.Net.WebUtility.HtmlDecode(matchError.Groups[1].Value.Trim());
                        }
                    }
                    
                    // Intentar buscar cualquier código o número en la respuesta
                    if (string.IsNullOrEmpty(codigoError))
                    {
                        var matchCodigo = System.Text.RegularExpressions.Regex.Match(respuesta, 
                            @"(?:codigo|code|error)[\s:=]*(\d+)", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (matchCodigo.Success)
                        {
                            codigoError = matchCodigo.Groups[1].Value;
                        }
                    }
                    
                    _logger.LogInformation("Respuesta HTML del SII analizada. Exitoso: {Exitoso}, TrackID: {TrackID}, Mensaje: {Mensaje}", 
                        esExitoso, trackIdHtml, mensajeError);
                    
                    // Crear una respuesta estructurada incluso cuando el SII devuelve HTML
                    var envioResponseHtml = new EnvioResponse
                    {
                        Exito = esExitoso,
                        TrackID = trackIdHtml,
                        Mensaje = mensajeError,
                        FechaRecepcion = DateTime.Now
                    };
                    
                    if (!esExitoso)
                    {
                        if (!string.IsNullOrEmpty(codigoError))
                        {
                            envioResponseHtml.Errores.Add($"Código: {codigoError}");
                        }
                        if (!string.IsNullOrEmpty(glosa))
                        {
                            envioResponseHtml.Errores.Add(glosa);
                        }
                        else
                        {
                            envioResponseHtml.Errores.Add(mensajeError);
                        }
                    }
                    
                    _logger.LogInformation("Respuesta del SII procesada. Exitoso: {Exitoso}, TrackID: {TrackID}, Código: {Codigo}, Mensaje: {Mensaje}", 
                        esExitoso, trackIdHtml, codigoError, mensajeError);
                    
                    return envioResponseHtml; // Retornar respuesta estructurada
                }
                
                var doc = new XmlDocument();
                try
                {
                    doc.LoadXml(respuesta);
                }
                catch (XmlException xmlEx)
                {
                    // Si el error es por HTML corrupto o múltiples elementos raíz, intentar limpiarlo
                    if (xmlEx.Message.Contains("hr") || xmlEx.Message.Contains("td") || xmlEx.Message.Contains("tr") || 
                        xmlEx.Message.Contains("start tag") || xmlEx.Message.Contains("end tag") ||
                        xmlEx.Message.Contains("multiple root") || xmlEx.Message.Contains("root elements"))
                    {
                        _logger.LogWarning("La respuesta del SII contiene HTML corrupto o múltiples elementos raíz. Limpiando antes de parsear...");
                        // Limpiar HTML de la respuesta
                        string respuestaLimpia = respuesta;
                        string[] etiquetasBasura = new[] { "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", "html", "head", "body", "th", "title", "center", "font", "strong", "small" };
                        foreach (var tag in etiquetasBasura)
                        {
                            respuestaLimpia = System.Text.RegularExpressions.Regex.Replace(respuestaLimpia, $@"<{tag}\b[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            respuestaLimpia = System.Text.RegularExpressions.Regex.Replace(respuestaLimpia, $@"</{tag}>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            respuestaLimpia = System.Text.RegularExpressions.Regex.Replace(respuestaLimpia, $@"<{tag}\s*/>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        // Eliminar <td> solo si no está en contexto XML válido
                        if (respuestaLimpia.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                            !System.Text.RegularExpressions.Regex.IsMatch(respuestaLimpia, @"<TD>\d+</TD>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            respuestaLimpia = System.Text.RegularExpressions.Regex.Replace(respuestaLimpia, @"<td\b[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            respuestaLimpia = System.Text.RegularExpressions.Regex.Replace(respuestaLimpia, @"</td>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        
                        // CRÍTICO: Si tiene declaración XML y múltiples elementos raíz, extraer solo el bloque SOAP válido
                        _logger.LogInformation("Respuesta limpia antes de extracción (primeros 500 caracteres): {Respuesta}", 
                            respuestaLimpia.Length > 500 ? respuestaLimpia.Substring(0, 500) : respuestaLimpia);
                        
                        // Buscar el bloque SOAP válido (sin requerir declaración XML)
                        var matchSoap = System.Text.RegularExpressions.Regex.Match(respuestaLimpia, 
                            @"<soapenv:Envelope\b.*?</soapenv:Envelope>", 
                            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (matchSoap.Success)
                        {
                            respuestaLimpia = matchSoap.Value;
                            // CRÍTICO: Eliminar declaración XML si está presente en el bloque extraído
                            if (respuestaLimpia.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                            {
                                int inicioEnvelope = respuestaLimpia.IndexOf("<soapenv:Envelope", StringComparison.OrdinalIgnoreCase);
                                if (inicioEnvelope > 0)
                                {
                                    respuestaLimpia = respuestaLimpia.Substring(inicioEnvelope).Trim();
                                }
                            }
                            _logger.LogInformation("Bloque SOAP extraído de la respuesta del SII (sin declaración XML), longitud: {Length}", respuestaLimpia.Length);
                        }
                        else
                        {
                            // Si no hay SOAP, buscar cualquier bloque XML válido (RESPUESTA, etc.)
                            var matchRespuesta = System.Text.RegularExpressions.Regex.Match(respuestaLimpia, 
                                @"<[A-Za-z]+:?RESPUESTA\b.*?</[A-Za-z]+:?RESPUESTA>", 
                                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (matchRespuesta.Success)
                            {
                                respuestaLimpia = matchRespuesta.Value;
                                // CRÍTICO: Eliminar declaración XML si está presente
                                if (respuestaLimpia.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                                {
                                    int inicioRespuesta = respuestaLimpia.IndexOf("<", StringComparison.OrdinalIgnoreCase);
                                    if (inicioRespuesta > 0 && respuestaLimpia.Substring(0, inicioRespuesta).Contains("<?xml"))
                                    {
                                        respuestaLimpia = respuestaLimpia.Substring(inicioRespuesta).Trim();
                                    }
                                }
                                _logger.LogInformation("Bloque RESPUESTA extraído de la respuesta del SII (sin declaración XML), longitud: {Length}", respuestaLimpia.Length);
                            }
                            else
                            {
                                _logger.LogError("No se encontró bloque SOAP ni RESPUESTA en la respuesta del SII. Respuesta limpia (primeros 1000 caracteres): {Respuesta}", 
                                    respuestaLimpia.Length > 1000 ? respuestaLimpia.Substring(0, 1000) : respuestaLimpia);
                                throw new InvalidOperationException("El SII devolvió una respuesta que no contiene XML válido. Verifique la configuración del emisor y la autorización del SII.");
                            }
                        }
                        
                        try
                        {
                            // Intentar primero con LoadXml
                            doc.LoadXml(respuestaLimpia);
                            _logger.LogInformation("Respuesta del SII limpiada y parseada correctamente");
                        }
                        catch (XmlException xmlEx2)
                        {
                            // Si LoadXml falla, intentar con XmlReader que es más tolerante
                            _logger.LogWarning("Error al parsear con LoadXml: {Error}. Intentando con XmlReader...", xmlEx2.Message);
                            try
                            {
                                var settings = new System.Xml.XmlReaderSettings
                                {
                                    DtdProcessing = System.Xml.DtdProcessing.Ignore, // Ignorar DTD por seguridad
                                    XmlResolver = null // No resolver referencias externas
                                };
                                using (var reader = System.Xml.XmlReader.Create(new System.IO.StringReader(respuestaLimpia), settings))
                                {
                                    doc = new XmlDocument();
                                    doc.Load(reader);
                                    _logger.LogInformation("Respuesta del SII parseada exitosamente usando XmlReader");
                                }
                            }
                            catch (Exception readerEx)
                            {
                                _logger.LogError(readerEx, "Error al parsear respuesta del SII con XmlReader: {Error}", readerEx.Message);
                                _logger.LogError("Respuesta limpia (primeros 1000 caracteres): {Respuesta}", 
                                    respuestaLimpia.Length > 1000 ? respuestaLimpia.Substring(0, 1000) : respuestaLimpia);
                                throw new InvalidOperationException($"No se pudo parsear la respuesta del SII después de limpieza: {readerEx.Message}. El SII puede estar rechazando la solicitud. Verifique la configuración del emisor.");
                            }
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                var estado = doc.SelectSingleNode("//*[local-name()='ESTADO']")?.InnerText
                          ?? doc.SelectSingleNode("//*[local-name()='STATUS']")?.InnerText;
                var trackId = doc.SelectSingleNode("//*[local-name()='TRACKID']")?.InnerText;

                bool exito = estado == "00" || estado == "0";
                var envioResponse = new EnvioResponse
                {
                    Exito = exito,
                    TrackID = trackId ?? "",
                    Mensaje = exito ? "DOCUMENTO TRIBUTARIO ELECTRONICO RECIBIDO" : "Error al enviar",
                    FechaRecepcion = DateTime.Now
                };

                if (!envioResponse.Exito)
                {
                    var errores = doc.SelectNodes("//*[local-name()='ERROR']");
                    if (errores != null)
                    {
                        foreach (XmlNode error in errores)
                        {
                            envioResponse.Errores.Add(error.InnerText);
                        }
                    }
                }

                _logger.LogInformation("DTE enviado. TrackID: {TrackID}, Estado: {Estado}", trackId, estado);
                return envioResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar DTE al SII");
                throw;
            }
        }

        public async Task<EnvioResponse> EnviarLibroAsync(string xmlLibro, string token)
        {
            _logger.LogInformation("Enviando Libro de Compras/Ventas al SII");

            try
            {
                // Guardar el libro que se envía al SII
                GuardarDocumentoEnviado(xmlLibro, "EnvioLibro");
                
                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion";
                // URL específica para libros (diferente a DTEs)
                var urlBase = ambiente == "Produccion"
                    ? "https://palena.sii.cl/cgi_dte/UPL/DTEUpload" // Mismo endpoint para libros
                    : "https://maullin.sii.cl/cgi_dte/UPL/DTEUpload";

                // Extraer RUT del emisor (empresa) de la configuración
                var rutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "";
                var rutEmisorParts = rutEmisor.Split('-');
                if (rutEmisorParts.Length != 2)
                {
                    throw new InvalidOperationException("RUT del emisor no válido");
                }

                // Extraer RUT del usuario que envía (dueño del certificado)
                var rutEnvia = _configuration["FacturacionElectronica:Certificado:RUT"] ?? rutEmisor;
                var rutEnviaParts = rutEnvia.Split('-');
                if (rutEnviaParts.Length != 2)
                {
                    throw new InvalidOperationException("RUT del certificado no válido");
                }

                var url = $"{urlBase}?RUTCOMPANY={rutEmisorParts[0]}&DVCOMPANY={rutEmisorParts[1]}&RUTUSER={rutEnviaParts[0]}&DVUSER={rutEnviaParts[1]}";
                _logger.LogInformation("[UPLOAD_LIBRO_URL] {Url}", url);

                var boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = $"multipart/form-data; boundary={boundary}";
                request.Headers.Add("Cookie", $"TOKEN={token}");

                // CRÍTICO: Usar ISO-8859-1 para el XML del archivo
                var xmlBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(xmlLibro);
                var fileName = $"Libro_{DateTime.Now:yyyyMMddHHmmss}.xml";

                // Construir multipart
                var sb = new StringBuilder();
                sb.AppendLine($"--{boundary}");
                sb.AppendLine($"Content-Disposition: form-data; name=\"archivo\"; filename=\"{fileName}\"");
                sb.AppendLine("Content-Type: text/xml");
                sb.AppendLine();

                var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
                var footerBytes = Encoding.ASCII.GetBytes($"\r\n--{boundary}--\r\n");

                using (var requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await requestStream.WriteAsync(xmlBytes, 0, xmlBytes.Length);
                    await requestStream.WriteAsync(footerBytes, 0, footerBytes.Length);
                }

                string respuesta;
                HttpWebResponse? httpResponse = null;
                try
                {
                    httpResponse = (HttpWebResponse)await request.GetResponseAsync();
                    using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        respuesta = await reader.ReadToEndAsync();
                    }
                }
                catch (WebException wex)
                {
                    if (wex.Response != null)
                    {
                        using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                        {
                            respuesta = await reader.ReadToEndAsync();
                        }
                        GuardarRespuestaSII("EnvioLibro_Error", respuesta, $"HTTP_{((HttpWebResponse)wex.Response).StatusCode}");
                        _logger.LogError(wex, "Error al recibir respuesta del SII: {Respuesta}", respuesta);
                        throw;
                    }
                    throw;
                }

                GuardarRespuestaSII("EnvioLibro", respuesta, $"HTTP_{httpResponse?.StatusCode ?? 0}");

                // Parsear respuesta (similar a DTE)
                _logger.LogInformation("Respuesta del SII (Libro): {Respuesta}", respuesta.Length > 500 ? respuesta.Substring(0, 500) : respuesta);

                var envioResponse = new EnvioResponse { Exito = false };

                // Intentar parsear como XML
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(respuesta);

                    var trackIdNode = xmlDoc.SelectSingleNode("//*[local-name()='TRACKID']");
                    var estadoNode = xmlDoc.SelectSingleNode("//*[local-name()='ESTADO']");
                    var errorNode = xmlDoc.SelectSingleNode("//*[local-name()='ERR']");

                    if (trackIdNode != null)
                    {
                        envioResponse.TrackID = trackIdNode.InnerText;
                        envioResponse.Exito = true;
                        _logger.LogInformation("Libro enviado exitosamente. TrackID: {TrackID}", envioResponse.TrackID);
                    }
                    else if (errorNode != null)
                    {
                        var codigo = errorNode.SelectSingleNode(".//*[local-name()='COD']")?.InnerText ?? "";
                        var descripcion = errorNode.SelectSingleNode(".//*[local-name()='DESC']")?.InnerText ?? "";
                        envioResponse.Errores.Add($"Código: {codigo}");
                        envioResponse.Errores.Add(descripcion);
                        _logger.LogError("Error del SII al enviar libro: {Codigo} - {Descripcion}", codigo, descripcion);
                    }
                }
                catch
                {
                    // Si no es XML, intentar extraer info del HTML
                    if (respuesta.Contains("RECIBIDO", StringComparison.OrdinalIgnoreCase))
                    {
                        envioResponse.Exito = true;
                        envioResponse.TrackID = "PENDIENTE";
                        _logger.LogInformation("Libro recibido por el SII (respuesta HTML)");
                    }
                    else
                    {
                        envioResponse.Errores.Add("Error al procesar respuesta del SII");
                        _logger.LogError("No se pudo parsear la respuesta del SII");
                    }
                }

                return envioResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar Libro al SII");
                throw;
            }
        }

        public async Task<string> ObtenerTokenAsync()
        {
            var semilla = await ObtenerSemillaAsync();
            return await ObtenerTokenAsync(semilla);
        }

        public async Task<EnvioResponse> EnviarRCOFAsync(string xmlRCOF, string token)
        {
            _logger.LogInformation("Enviando RCOF al SII");

            try
            {
                // Guardar el RCOF que se envía al SII
                GuardarDocumentoEnviado(xmlRCOF, "EnvioRCOF");
                
                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion";
                // URL específica para RCOF (mismo endpoint que DTEs y Libros)
                var urlBase = ambiente == "Produccion"
                    ? "https://palena.sii.cl/cgi_dte/UPL/DTEUpload"
                    : "https://maullin.sii.cl/cgi_dte/UPL/DTEUpload";

                // Extraer RUT del emisor (empresa) de la configuración
                var rutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "";
                var rutEmisorParts = rutEmisor.Split('-');
                if (rutEmisorParts.Length != 2)
                {
                    throw new InvalidOperationException("RUT del emisor no válido");
                }

                // Extraer RUT del usuario que envía (dueño del certificado)
                var rutEnvia = _configuration["FacturacionElectronica:Certificado:RUT"] ?? rutEmisor;
                var rutEnviaParts = rutEnvia.Split('-');
                if (rutEnviaParts.Length != 2)
                {
                    throw new InvalidOperationException("RUT del certificado no válido");
                }

                var url = $"{urlBase}?RUTCOMPANY={rutEmisorParts[0]}&DVCOMPANY={rutEmisorParts[1]}&RUTUSER={rutEnviaParts[0]}&DVUSER={rutEnviaParts[1]}";
                _logger.LogInformation("[UPLOAD_RCOF_URL] {Url}", url);

                var boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = $"multipart/form-data; boundary={boundary}";
                request.Headers.Add("Cookie", $"TOKEN={token}");

                // CRÍTICO: Usar ISO-8859-1 para el XML del archivo
                var xmlBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(xmlRCOF);
                var fileName = $"RCOF_{DateTime.Now:yyyyMMddHHmmss}.xml";

                // Construir multipart
                var sb = new StringBuilder();
                sb.AppendLine($"--{boundary}");
                sb.AppendLine($"Content-Disposition: form-data; name=\"archivo\"; filename=\"{fileName}\"");
                sb.AppendLine("Content-Type: text/xml");
                sb.AppendLine();

                var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
                var footerBytes = Encoding.ASCII.GetBytes($"\r\n--{boundary}--\r\n");

                using (var requestStream = await request.GetRequestStreamAsync())
                {
                    await requestStream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await requestStream.WriteAsync(xmlBytes, 0, xmlBytes.Length);
                    await requestStream.WriteAsync(footerBytes, 0, footerBytes.Length);
                }

                string respuesta;
                HttpWebResponse? httpResponse = null;
                try
                {
                    httpResponse = (HttpWebResponse)await request.GetResponseAsync();
                    using (var reader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        respuesta = await reader.ReadToEndAsync();
                    }
                }
                catch (WebException wex)
                {
                    if (wex.Response != null)
                    {
                        using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                        {
                            respuesta = await reader.ReadToEndAsync();
                        }
                        GuardarRespuestaSII("EnvioRCOF_Error", respuesta, $"HTTP_{((HttpWebResponse)wex.Response).StatusCode}");
                        _logger.LogError(wex, "Error al recibir respuesta del SII: {Respuesta}", respuesta);
                        throw;
                    }
                    throw;
                }

                GuardarRespuestaSII("EnvioRCOF", respuesta, $"HTTP_{httpResponse?.StatusCode ?? 0}");

                // Parsear respuesta
                _logger.LogInformation("Respuesta del SII (RCOF): {Respuesta}", respuesta.Length > 500 ? respuesta.Substring(0, 500) : respuesta);

                var envioResponse = new EnvioResponse { Exito = false };

                // Intentar parsear como XML
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(respuesta);

                    var trackIdNode = xmlDoc.SelectSingleNode("//*[local-name()='TRACKID']");
                    var estadoNode = xmlDoc.SelectSingleNode("//*[local-name()='ESTADO']");
                    var errorNode = xmlDoc.SelectSingleNode("//*[local-name()='ERROR']");

                    if (trackIdNode != null)
                    {
                        envioResponse.Exito = true;
                        envioResponse.TrackID = trackIdNode.InnerText;
                        envioResponse.Mensaje = $"RCOF enviado exitosamente. Track ID: {envioResponse.TrackID}";
                        _logger.LogInformation("RCOF enviado exitosamente. TrackID: {TrackID}", envioResponse.TrackID);
                    }
                    else if (errorNode != null)
                    {
                        envioResponse.Mensaje = $"Error del SII: {errorNode.InnerText}";
                        _logger.LogWarning("Error reportado por el SII: {Error}", errorNode.InnerText);
                    }
                    else
                    {
                        envioResponse.Mensaje = "Respuesta inesperada del SII";
                        _logger.LogWarning("Respuesta inesperada del SII: {Respuesta}", respuesta);
                    }
                }
                catch (XmlException)
                {
                    // Si no es XML, es un error
                    envioResponse.Mensaje = $"Error del SII: {respuesta}";
                }

                return envioResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar RCOF al SII");
                throw;
            }
        }

        public async Task<EstadoEnvioResponse> ConsultarEstadoEnvioAsync(string trackId, string token)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_INI] Consultando estado del envío TrackID: {TrackID}", timestamp, trackId);

            if (string.IsNullOrWhiteSpace(trackId))
            {
                throw new ArgumentException("TrackID no puede estar vacío", nameof(trackId));
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token no puede estar vacío", nameof(token));
            }

            try
            {
                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion";
                var url = ambiente == "Produccion"
                    ? _configuration["SII:Produccion:UrlEstado"] ?? "https://palena.sii.cl/DTEWS/QueryEstUp.jws"
                    : _configuration["SII:Certificacion:UrlEstado"] ?? "https://maullin.sii.cl/DTEWS/QueryEstUp.jws";

                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_REQ] URL: {Url}, TrackID: {TrackID}", timestamp, url, trackId);

                // Construir SOAP request para consultar estado
                // El servicio QueryEstUp requiere el trackId en el body del SOAP
                var soapRequest = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/""
                  xmlns:def=""http://DefaultNamespace"">
   <soapenv:Header/>
   <soapenv:Body>
      <def:getEstUp soapenv:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
         <string>{System.Security.SecurityElement.Escape(trackId)}</string>
      </def:getEstUp>
   </soapenv:Body>
</soapenv:Envelope>";

                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_SOAP] {SoapRequest}", timestamp, soapRequest);

                // Configurar protocolos de seguridad
                ServicePointManager.SecurityProtocol = 
                    SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "text/xml; charset=utf-8";
                request.Headers.Add("SOAPAction", "");
                request.Headers.Add("Cookie", $"TOKEN={token}");

                var bytes = Encoding.UTF8.GetBytes(soapRequest);
                using (var reqStream = await request.GetRequestStreamAsync())
                {
                    await reqStream.WriteAsync(bytes, 0, bytes.Length);
                }

                string xmlResp;
                HttpWebResponse? httpResponse = null;
                try
                {
                    httpResponse = (HttpWebResponse)await request.GetResponseAsync();
                    using (var reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                    {
                        xmlResp = await reader.ReadToEndAsync();
                    }
                }
                catch (WebException wex)
                {
                    // Capturar respuesta incluso si hay error
                    if (wex.Response != null)
                    {
                        using (var reader = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                        {
                            xmlResp = await reader.ReadToEndAsync();
                        }
                        GuardarRespuestaSII("ConsultaEstado_Error", xmlResp, $"HTTP_{((HttpWebResponse)wex.Response).StatusCode}");
                        _logger.LogError(wex, "[{Timestamp}] [CONSULTA_ESTADO_ERROR] {XmlResp}", timestamp, xmlResp);
                        throw;
                    }
                    throw;
                }

                // Guardar respuesta del SII
                GuardarRespuestaSII("ConsultaEstado", xmlResp, $"HTTP_{httpResponse?.StatusCode ?? 0}");

                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_RESP_RAW] {XmlResp}", timestamp, xmlResp);

                // Parsear respuesta SOAP
                var soap = new XmlDocument();
                try
                {
                    soap.LoadXml(xmlResp);
                }
                catch (XmlException xmlEx)
                {
                    _logger.LogError(xmlEx, "[{Timestamp}] Error al parsear respuesta SOAP del SII", timestamp);
                    throw new InvalidOperationException($"Error al parsear respuesta del SII: {xmlEx.Message}", xmlEx);
                }

                var innerEscaped = soap.SelectSingleNode("//*[local-name()='getEstUpReturn']")?.InnerText;
                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_INNER_ESCAPED] {InnerEscaped}", timestamp, innerEscaped);

                if (string.IsNullOrEmpty(innerEscaped))
                {
                    _logger.LogWarning("[{Timestamp}] No se encontró nodo getEstUpReturn en la respuesta", timestamp);
                    throw new InvalidOperationException("Respuesta del SII inválida: no se encontró getEstUpReturn");
                }

                var innerXml = System.Net.WebUtility.HtmlDecode(innerEscaped);
                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_INNER_XML] {InnerXml}", timestamp, innerXml);

                // Parsear XML interno
                var xml = new XmlDocument();
                try
                {
                    xml.LoadXml(innerXml);
                }
                catch (XmlException xmlEx)
                {
                    _logger.LogError(xmlEx, "[{Timestamp}] Error al parsear XML interno de la respuesta", timestamp);
                    // Intentar limpiar HTML corrupto si existe
                    if (xmlEx.Message.Contains("hr") || xmlEx.Message.Contains("td") || xmlEx.Message.Contains("tr") ||
                        xmlEx.Message.Contains("start tag") || xmlEx.Message.Contains("end tag"))
                    {
                        _logger.LogWarning("[{Timestamp}] La respuesta contiene HTML corrupto. Limpiando...", timestamp);
                        var xmlLimpio = LimpiarHTMLCorrupto(innerXml);
                        xml.LoadXml(xmlLimpio);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Error al parsear XML de respuesta: {xmlEx.Message}", xmlEx);
                    }
                }

                // Extraer datos del estado
                var estadoNode = xml.SelectSingleNode("//*[local-name()='ESTADO']");
                var estado = estadoNode?.InnerText?.Trim();

                var trackIdNode = xml.SelectSingleNode("//*[local-name()='TRACKID']");
                var trackIdRespuesta = trackIdNode?.InnerText?.Trim() ?? trackId;

                var glosaNode = xml.SelectSingleNode("//*[local-name()='GLOSA']");
                var glosa = glosaNode?.InnerText?.Trim() ?? "";

                // Extraer contadores de documentos
                var aceptadosNode = xml.SelectSingleNode("//*[local-name()='NRO_DOC_ACEPTADOS']");
                var rechazadosNode = xml.SelectSingleNode("//*[local-name()='NRO_DOC_RECHAZADOS']");
                var reparosNode = xml.SelectSingleNode("//*[local-name()='NRO_DOC_REPAROS']");

                int aceptados = 0;
                int rechazados = 0;
                int reparos = 0;

                if (aceptadosNode != null && int.TryParse(aceptadosNode.InnerText?.Trim(), out int a))
                    aceptados = a;

                if (rechazadosNode != null && int.TryParse(rechazadosNode.InnerText?.Trim(), out int r))
                    rechazados = r;

                if (reparosNode != null && int.TryParse(reparosNode.InnerText?.Trim(), out int rep))
                    reparos = rep;

                // Extraer fecha de consulta si está disponible
                var fechaNode = xml.SelectSingleNode("//*[local-name()='FECHA_RECEPCION']");
                DateTime? fechaConsulta = null;
                if (fechaNode != null && !string.IsNullOrEmpty(fechaNode.InnerText))
                {
                    if (DateTime.TryParse(fechaNode.InnerText.Trim(), out DateTime fecha))
                        fechaConsulta = fecha;
                }

                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_PARSE] ESTADO={Estado}, TrackID={TrackID}, Glosa={Glosa}, Aceptados={Aceptados}, Rechazados={Rechazados}, Reparos={Reparos}",
                    timestamp, estado, trackIdRespuesta, glosa, aceptados, rechazados, reparos);

                var estadoResponse = new EstadoEnvioResponse
                {
                    TrackID = trackIdRespuesta,
                    Estado = estado ?? "DESCONOCIDO",
                    GlosaEstado = glosa,
                    Aceptados = aceptados,
                    Rechazados = rechazados,
                    Reparos = reparos,
                    FechaConsulta = fechaConsulta ?? DateTime.Now
                };

                _logger.LogInformation("[{Timestamp}] [CONSULTA_ESTADO_OK] Estado consultado exitosamente", timestamp);
                return estadoResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Timestamp}] Error al consultar estado del envío TrackID: {TrackID}", timestamp, trackId);
                throw;
            }
        }

        /// <summary>
        /// Limpia HTML corrupto de una respuesta XML del SII
        /// </summary>
        private string LimpiarHTMLCorrupto(string xml)
        {
            string xmlLimpio = xml;
            string[] etiquetasBasura = new[] { "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", "html", "head", "body", "th" };
            
            foreach (var tag in etiquetasBasura)
            {
                xmlLimpio = System.Text.RegularExpressions.Regex.Replace(xmlLimpio, $@"<{tag}\b[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                xmlLimpio = System.Text.RegularExpressions.Regex.Replace(xmlLimpio, $@"</{tag}>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                xmlLimpio = System.Text.RegularExpressions.Regex.Replace(xmlLimpio, $@"<{tag}\s*/>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            // Eliminar <td> solo si no está en contexto XML válido
            if (xmlLimpio.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                !System.Text.RegularExpressions.Regex.IsMatch(xmlLimpio, @"<TD>\d+</TD>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                xmlLimpio = System.Text.RegularExpressions.Regex.Replace(xmlLimpio, @"<td\b[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                xmlLimpio = System.Text.RegularExpressions.Regex.Replace(xmlLimpio, @"</td>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            return xmlLimpio;
        }

        /// <summary>
        /// Firma la semilla para obtener el token
        /// </summary>
        private string FirmarSemilla(string semilla, string nombreCertificado)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml($"<getToken><item><Semilla>{semilla}</Semilla></item></getToken>");

            // Recuperar certificado (similar a FirmaService)
            var store = new X509Certificates.X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                var collection = store.Certificates;
                var resultados = collection.Find(X509FindType.FindBySubjectName, nombreCertificado, false);

                if (resultados.Count == 0)
                {
                    throw new Exception($"Certificado no encontrado: {nombreCertificado}");
                }

                var certificado = resultados[0];

                var semillaNode = (XmlElement)doc.GetElementsByTagName("Semilla")[0];
                semillaNode.SetAttribute("ID", "semillaID");

                var signedXml = new SignedXml(doc);
                signedXml.SigningKey = certificado.GetRSAPrivateKey();

                var reference = new Reference("#semillaID");
                reference.DigestMethod = SignedXml.XmlDsigSHA1Url;
                signedXml.AddReference(reference);

                var ki = new KeyInfo();
                ki.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = ki;

                signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
                signedXml.ComputeSignature();

                doc.DocumentElement?.AppendChild(doc.ImportNode(signedXml.GetXml(), true));

                return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + doc.OuterXml;
            }
            finally
            {
                store.Close();
            }
        }
    }
}
