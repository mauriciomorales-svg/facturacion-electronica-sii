using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.CAF;
using FacturacionElectronicaSII.Models.DTE;
using FacturacionElectronicaSII.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio para generación del Timbre Electrónico (TED)
    /// 
    /// Este servicio replica EXACTAMENTE el proceso del sistema legacy para garantizar
    /// compatibilidad bit a bit con la firma digital del SII.
    /// 
    /// ⚠️ PUNTOS CRÍTICOS:
    /// - Usa BouncyCastle 2.2.1 (conversión manual de RSA, sin DotNetUtilities)
    /// - Encoding UTF-8 para el cálculo del hash SHA1
    /// - SignHash() con OID SHA1 (NO usar SignData() directamente)
    /// - Limpieza completa del nodo DD antes de firmar (canonicalización)
    /// </summary>
    public class TEDService : ITEDService
    {
        private readonly ILogger<TEDService> _logger;

        public TEDService(ILogger<TEDService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Genera el Timbre Electrónico Digital (TED) para una factura.
        /// Este método replica EXACTAMENTE el proceso del sistema legacy.
        /// </summary>
        /// <param name="documento">Documento tributario electrónico</param>
        /// <param name="caf">Datos del CAF (Código de Autorización de Folios)</param>
        /// <returns>XML del TED firmado</returns>
        /// <exception cref="ArgumentException">Si el folio está fuera del rango autorizado por el CAF</exception>
        public string GenerarTED(DocumentoTributario documento, CAFData caf)
        {
            _logger.LogInformation("=== [1] INICIO GENERACIÓN DEL TIMBRE ===");
            _logger.LogInformation("Generando TED para DTE tipo {TipoDTE}, folio {Folio}", documento.TipoDTE, documento.Folio);

            try
            {
                // 🔹 PASO 0: Validar que el folio esté dentro del rango autorizado por el CAF
                if (!ValidadorRangoFolios.ValidarFolioEnRango(
                    caf.XMLOriginal, 
                    documento.Folio, 
                    documento.TipoDTE, 
                    out int rangoDesde, 
                    out int rangoHasta))
                {
                    string mensajeError = $"El folio {documento.Folio} está FUERA del rango autorizado por el CAF (Desde: {rangoDesde}, Hasta: {rangoHasta}). " +
                                         "No se puede timbrar este folio.";
                    _logger.LogError(mensajeError);
                    throw new ArgumentException(mensajeError, nameof(documento));
                }
                _logger.LogInformation("Folio {Folio} validado correctamente dentro del rango autorizado ({Desde} - {Hasta})", 
                    documento.Folio, rangoDesde, rangoHasta);

                // Generar nodo DD usando la misma lógica que FuncionesComunes.GenerarNodoDDParaFirma
                var fechaEmision = documento.FechaEmision.ToString("yyyy-MM-dd");
                var fechaTimbre = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var rutReceptor = documento.Encabezado.Receptor.RUTRecep;
                var razonSocialReceptor = documento.Encabezado.Receptor.RznSocRecep;
                
                // Obtener primer item del detalle (truncado a 40 caracteres)
                var itemPrincipal = documento.Detalles.FirstOrDefault()?.Nombre ?? "";
                if (itemPrincipal.Length > 40)
                {
                    itemPrincipal = itemPrincipal.Substring(0, 40);
                }
                
                // Generar nodo DD usando la misma lógica exacta que FuncionesComunes.GenerarNodoDDParaFirma
                var nodoDD = GenerarNodoDDParaFirma(
                    caf.RutEmisor ?? "",
                    documento.TipoDTE,
                    documento.Folio,
                    fechaEmision,
                    rutReceptor,
                    razonSocialReceptor,
                    (int)documento.Totales.MntTotal,
                    itemPrincipal,
                    caf.XMLOriginal,
                    fechaTimbre
                );
                
                _logger.LogInformation("=== [2] Nodo DD listo para firmar ===");
                _logger.LogInformation("{NodoDD}", nodoDD);
                
                // Verificar que el nodo DD no esté vacío (como en código funcional)
                if (string.IsNullOrEmpty(nodoDD))
                {
                    throw new Exception("El nodo DD está vacío.");
                }
                
                // Limpiar nodo DD (sin espacios ni saltos de línea) - CRÍTICO antes de firmar
                // Usar exactamente la misma lógica que FuncionesComunes.LimpiarNodoDD
                var nodoDDLimpio = LimpiarNodoDD(nodoDD);
                _logger.LogInformation("Nodo DD limpiado para firma: {Length} caracteres", nodoDDLimpio.Length);
                
                // Extraer clave privada del CAF (misma lógica que FuncionesComunes.ExtraerClavePrivada)
                var clavePrivada = ExtraerClavePrivada(caf.XMLOriginal);
                if (string.IsNullOrEmpty(clavePrivada))
                {
                    throw new Exception("No se pudo extraer la clave privada del CAF.");
                }
                _logger.LogInformation("=== [3] Clave privada extraída correctamente ===");
                
                // Importar clave RSA (misma lógica que FuncionesComunes.ImportarClavePrivada)
                using var claveRSA = ImportarClavePrivada(clavePrivada);
                if (claveRSA == null)
                {
                    throw new Exception("No se pudo importar la clave privada.");
                }
                _logger.LogInformation("=== [4] Clave privada importada correctamente ===");
                
                // Firmar el nodo DD (misma lógica que FuncionesComunes.FirmarTexto)
                var firma = FirmarTexto(nodoDDLimpio, claveRSA);
                if (string.IsNullOrEmpty(firma))
                {
                    throw new Exception("La firma generada está vacía.");
                }
                _logger.LogInformation("=== [5] Firma generada correctamente ===");
                _logger.LogInformation("{Firma}", firma);
                
                // Ensamblar TED usando GenerarTEDCompleto (misma lógica exacta)
                string ted = GenerarTEDCompleto(nodoDDLimpio, firma);

                _logger.LogInformation("=== [6] TED generado correctamente ===");
                _logger.LogInformation("{TED}", ted);
                _logger.LogInformation("=== [7] FIN GENERACIÓN DEL TIMBRE ===");
                return ted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar TED");
                throw;
            }
        }
        
        /// <summary>
        /// Genera el nodo DD usando exactamente la misma lógica que FuncionesComunes.GenerarNodoDDParaFirma
        /// </summary>
        private string GenerarNodoDDParaFirma(
            string rutEmisor, int tipoDoc, int folio, string fechaEmision,
            string rutReceptor, string razonSocialReceptor, int montoTotal,
            string itemPrincipal, string cafXml, string fechaTimbre)
        {
            try
            {
                // CRÍTICO: Sanitizar el XML del CAF antes de parsearlo (defensive programming)
                if (string.IsNullOrWhiteSpace(cafXml))
                {
                    throw new InvalidOperationException("El XML del CAF está vacío o nulo");
                }
                
                _logger.LogInformation("Parseando XML del CAF, longitud: {Length} caracteres", cafXml.Length);
                
                // CRÍTICO: Obtener CAF blindado usando método funcional probado
                // El CAF ya debería venir limpio desde CAFService, pero lo sanitizamos defensivamente
                _logger.LogInformation("Obteniendo CAF blindado, longitud input: {Length} caracteres", cafXml.Length);
                var cafDoc = ObtenerCafBlindado(cafXml);
                _logger.LogInformation("CAF blindado obtenido y parseado correctamente");
                
                var cafNode = cafDoc.SelectSingleNode("//CAF");
                
                if (cafNode == null)
                {
                    _logger.LogError("No se encontró el nodo <CAF> en el XML. Contenido completo: {CAF}", cafXml);
                    throw new Exception("No se encontró el nodo <CAF> en el XML.");
                }
                
                // CRÍTICO: Sanitizar el OuterXml del nodo CAF antes de usarlo
                // El OuterXml podría contener tags HTML si el XML original tenía HTML mezclado
                string cafOuterXml = cafNode.OuterXml;
                
                // Verificar si el OuterXml tiene tags HTML mezclados (pero NO eliminar <TD> que es válido del XML del CAF)
                // Solo detectar HTML real, no tags XML válidos del CAF como <TD>, <RE>, <RS>, etc.
                bool tieneHTMLReal = cafOuterXml.Contains("<hr", StringComparison.OrdinalIgnoreCase) || 
                                     cafOuterXml.Contains("<tr", StringComparison.OrdinalIgnoreCase) ||
                                     cafOuterXml.Contains("<table", StringComparison.OrdinalIgnoreCase) ||
                                     (cafOuterXml.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                                      !cafOuterXml.Contains("<TD>", StringComparison.OrdinalIgnoreCase)); // Solo si hay <td> pero NO <TD>
                
                if (tieneHTMLReal)
                {
                    _logger.LogWarning("El OuterXml del nodo CAF contiene tags HTML mezclados. Sanitizando...");
                    // Eliminar SOLO tags HTML reales, NO tags XML válidos del CAF como <TD>, <RE>, <RS>, etc.
                    // IMPORTANTE: NO incluir "td" porque <TD> es un tag válido del XML del CAF
                    string[] etiquetasBasura = new[] { "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", "html", "head", "body", "th" };
                    foreach (var tag in etiquetasBasura)
                    {
                        cafOuterXml = Regex.Replace(cafOuterXml, $@"<{tag}\b[^>]*>", "", RegexOptions.IgnoreCase);
                        cafOuterXml = Regex.Replace(cafOuterXml, $@"</{tag}>", "", RegexOptions.IgnoreCase);
                        cafOuterXml = Regex.Replace(cafOuterXml, $@"<{tag}\s*/>", "", RegexOptions.IgnoreCase);
                    }
                    // Eliminar <td> y </td> solo si NO están precedidos por tags XML válidos del CAF
                    // Esto es más seguro: eliminar <td> solo si está claramente fuera de contexto XML
                    if (cafOuterXml.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                        !Regex.IsMatch(cafOuterXml, @"<TD>\d+</TD>", RegexOptions.IgnoreCase))
                    {
                        cafOuterXml = Regex.Replace(cafOuterXml, @"<td\b[^>]*>", "", RegexOptions.IgnoreCase);
                        cafOuterXml = Regex.Replace(cafOuterXml, @"</td>", "", RegexOptions.IgnoreCase);
                    }
                    _logger.LogInformation("OuterXml del nodo CAF sanitizado");
                }
                
                // Validar que el OuterXml del CAF sea XML válido
                try
                {
                    var testDoc = new XmlDocument();
                    testDoc.LoadXml(cafOuterXml);
                    _logger.LogInformation("XML del nodo CAF validado correctamente, longitud: {Length}", cafOuterXml.Length);
                }
                catch (XmlException xmlEx)
                {
                    _logger.LogError(xmlEx, "El OuterXml del nodo CAF no es XML válido después de sanitización. Error en línea {Line}, posición {Pos}", xmlEx.LineNumber, xmlEx.LinePosition);
                    _logger.LogError("Contenido del nodo CAF (primeros 1000 caracteres): {CAF}", 
                        cafOuterXml.Length > 1000 ? cafOuterXml.Substring(0, 1000) : cafOuterXml);
                    throw new Exception($"El XML del nodo CAF no es válido: {xmlEx.Message}", xmlEx);
                }
                
                // Truncar itemPrincipal a máximo 40 caracteres (requisito SII para IT1)
                string itemPrincipalTruncado = itemPrincipal != null && itemPrincipal.Length > 40 
                    ? itemPrincipal.Substring(0, 40) 
                    : itemPrincipal ?? "";
                
                // Codificar caracteres especiales XML en los valores de texto (misma lógica)
                string rutEmisorEscapado = System.Security.SecurityElement.Escape(rutEmisor ?? "");
                string rutReceptorEscapado = System.Security.SecurityElement.Escape(rutReceptor ?? "");
                string razonSocialReceptorEscapado = System.Security.SecurityElement.Escape(razonSocialReceptor ?? "");
                string itemPrincipalEscapado = System.Security.SecurityElement.Escape(itemPrincipalTruncado);
                
                // CRÍTICO: Generar DD con formato (como código funcional)
                // El formato será limpiado después por LimpiarNodoDD antes de firmar
                // IMPORTANTE: Usar cafOuterXml (sanitizado) en lugar de cafNode.OuterXml
                string nodoDD = $@"
<DD>
    <RE>{rutEmisorEscapado}</RE>
    <TD>{tipoDoc}</TD>
    <F>{folio}</F>
    <FE>{fechaEmision}</FE>
    <RR>{rutReceptorEscapado}</RR>
    <RSR>{razonSocialReceptorEscapado}</RSR>
    <MNT>{montoTotal}</MNT>
    <IT1>{itemPrincipalEscapado}</IT1>
    {cafOuterXml}
    <TSTED>{fechaTimbre}</TSTED>
</DD>";

                _logger.LogInformation("Nodo DD generado con formato, longitud: {Length} caracteres", nodoDD.Length);
                return nodoDD.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar nodo DD para firma");
                throw;
            }
        }
        
        /// <summary>
        /// Construye el TED final insertando el DD y la firma.
        /// Replica exactamente el método GenerarTEDCompleto del sistema legacy.
        /// </summary>
        private string GenerarTEDCompleto(string dd, string firma)
        {
            // Asegurar que el nodo DD no esté anidado incorrectamente
            if (dd.StartsWith("<DD>") && dd.EndsWith("</DD>"))
            {
                dd = dd.Substring(4, dd.Length - 9); // Elimina <DD> y </DD>
            }

            // Construir el TED con el formato exacto del sistema legacy
            string ted = $@"
<TED version=""1.0"">
    <DD>{dd}</DD>
    <FRMT algoritmo=""SHA1withRSA"">{firma}</FRMT>
</TED>";

            // Guardar TED final antes de enviarlo para depuración (igual que código legacy)
            GuardarTEDDepuracion(ted);

            return ted;
        }

        /// <summary>
        /// Guarda el TED final para depuración (igual que código legacy)
        /// </summary>
        /// <param name="ted">XML del TED a guardar</param>
        private void GuardarTEDDepuracion(string ted)
        {
            try
            {
                string rutaArchivo = PathHelper.GetDepuracionPath("Depuracion_TED_Final.txt");
                File.WriteAllText(rutaArchivo, ted, Encoding.UTF8);
                _logger.LogInformation("TED de depuración guardado en: {Ruta}", rutaArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo guardar el archivo TED de depuración");
                // No lanzar excepción, solo loggear el error (igual que código legacy)
            }
        }

        public string GenerarDD(DocumentoTributario documento, CAFData caf)
        {
            _logger.LogInformation("Generando nodo DD para DTE tipo {TipoDTE}, folio {Folio}", documento.TipoDTE, documento.Folio);

            try
            {
                // CRÍTICO: Sanitizar el XML del CAF antes de parsearlo (defensive programming)
                // El CAF podría tener HTML corrupto mezclado con XML
                _logger.LogInformation("Parseando XML del CAF para generar nodo DD, longitud: {Length} caracteres", caf.XMLOriginal.Length);
                
                // Obtener CAF blindado usando método funcional probado
                var cafDoc = ObtenerCafBlindado(caf.XMLOriginal);
                _logger.LogInformation("CAF blindado obtenido y parseado correctamente para nodo DD");
                
                var cafNode = cafDoc.SelectSingleNode("//CAF");
                
                if (cafNode == null)
                {
                    _logger.LogError("No se encontró el nodo <CAF> en el XML del CAF después de sanitización");
                    throw new InvalidOperationException("No se encontró el nodo <CAF> en el XML del CAF");
                }
                
                // Obtener primer item del detalle (truncado a 40 caracteres)
                var itemPrincipal = documento.Detalles.FirstOrDefault()?.Nombre ?? "";
                if (itemPrincipal.Length > 40)
                {
                    itemPrincipal = itemPrincipal.Substring(0, 40);
                }
                
                // Construir DD según código funcional (con formato primero, luego se limpia)
                var fechaEmision = documento.FechaEmision.ToString("yyyy-MM-dd");
                var fechaTimbre = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var rutReceptor = documento.Encabezado.Receptor.RUTRecep;
                var razonSocialReceptor = documento.Encabezado.Receptor.RznSocRecep;
                
                // Escapar caracteres especiales XML (como en código funcional)
                var rutEmisorEscapado = System.Security.SecurityElement.Escape(caf.RutEmisor ?? "");
                var rutReceptorEscapado = System.Security.SecurityElement.Escape(rutReceptor ?? "");
                var razonSocialEscapado = System.Security.SecurityElement.Escape(razonSocialReceptor ?? "");
                var itemPrincipalEscapado = System.Security.SecurityElement.Escape(itemPrincipal);
                
                // CRÍTICO: Sanitizar el OuterXml del nodo CAF antes de usarlo
                // El OuterXml podría contener tags HTML si el XML original tenía HTML mezclado
                string cafOuterXml = cafNode.OuterXml;
                
                // Verificar si el OuterXml tiene tags HTML mezclados (pero NO eliminar <TD> que es válido del XML del CAF)
                // Solo detectar HTML real, no tags XML válidos del CAF como <TD>, <RE>, <RS>, etc.
                bool tieneHTMLReal = cafOuterXml.Contains("<hr", StringComparison.OrdinalIgnoreCase) || 
                                     cafOuterXml.Contains("<tr", StringComparison.OrdinalIgnoreCase) ||
                                     cafOuterXml.Contains("<table", StringComparison.OrdinalIgnoreCase) ||
                                     (cafOuterXml.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                                      !cafOuterXml.Contains("<TD>", StringComparison.OrdinalIgnoreCase)); // Solo si hay <td> pero NO <TD>
                
                if (tieneHTMLReal)
                {
                    _logger.LogWarning("El OuterXml del nodo CAF contiene tags HTML mezclados. Sanitizando...");
                    // Eliminar SOLO tags HTML reales, NO tags XML válidos del CAF como <TD>, <RE>, <RS>, etc.
                    // IMPORTANTE: NO incluir "td" porque <TD> es un tag válido del XML del CAF
                    string[] etiquetasBasura = new[] { "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", "html", "head", "body", "th" };
                    foreach (var tag in etiquetasBasura)
                    {
                        cafOuterXml = Regex.Replace(cafOuterXml, $@"<{tag}\b[^>]*>", "", RegexOptions.IgnoreCase);
                        cafOuterXml = Regex.Replace(cafOuterXml, $@"</{tag}>", "", RegexOptions.IgnoreCase);
                        cafOuterXml = Regex.Replace(cafOuterXml, $@"<{tag}\s*/>", "", RegexOptions.IgnoreCase);
                    }
                    // Eliminar <td> y </td> solo si NO están precedidos por tags XML válidos del CAF
                    // Esto es más seguro: eliminar <td> solo si está claramente fuera de contexto XML
                    if (cafOuterXml.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                        !Regex.IsMatch(cafOuterXml, @"<TD>\d+</TD>", RegexOptions.IgnoreCase))
                    {
                        cafOuterXml = Regex.Replace(cafOuterXml, @"<td\b[^>]*>", "", RegexOptions.IgnoreCase);
                        cafOuterXml = Regex.Replace(cafOuterXml, @"</td>", "", RegexOptions.IgnoreCase);
                    }
                    _logger.LogInformation("OuterXml del nodo CAF sanitizado");
                }
                
                // Validar que el XML del CAF sea válido antes de insertarlo
                try
                {
                    // Verificar que el CAF sea XML válido
                    var testDoc = new XmlDocument();
                    testDoc.LoadXml(cafOuterXml);
                    _logger.LogInformation("XML del CAF validado correctamente, longitud: {Length}", cafOuterXml.Length);
                }
                catch (XmlException ex)
                {
                    _logger.LogError(ex, "El XML del CAF no es válido después de sanitización. Primeros 500 caracteres: {CAFXML}", 
                        cafOuterXml.Length > 500 ? cafOuterXml.Substring(0, 500) : cafOuterXml);
                    throw new InvalidOperationException($"El XML del CAF no es válido: {ex.Message}", ex);
                }
                
                // Generar DD con formato (como código funcional)
                // IMPORTANTE: Usar cafOuterXml (sanitizado) en lugar de cafNode.OuterXml
                string nodoDD = $@"
<DD>
    <RE>{rutEmisorEscapado}</RE>
    <TD>{documento.TipoDTE}</TD>
    <F>{documento.Folio}</F>
    <FE>{fechaEmision}</FE>
    <RR>{rutReceptorEscapado}</RR>
    <RSR>{razonSocialEscapado}</RSR>
    <MNT>{documento.Totales.MntTotal}</MNT>
    <IT1>{itemPrincipalEscapado}</IT1>
    {cafOuterXml}
    <TSTED>{fechaTimbre}</TSTED>
</DD>";
                
                // Validar que el nodo DD completo sea XML válido
                try
                {
                    var testDD = new XmlDocument();
                    testDD.LoadXml(nodoDD.Trim());
                    _logger.LogInformation("Nodo DD completo validado correctamente");
                }
                catch (XmlException ex)
                {
                    _logger.LogError(ex, "El nodo DD generado no es XML válido. Error: {Error}", ex.Message);
                    _logger.LogError("Nodo DD (primeros 1000 caracteres): {DD}", 
                        nodoDD.Length > 1000 ? nodoDD.Substring(0, 1000) : nodoDD);
                    throw new InvalidOperationException($"El nodo DD generado no es XML válido: {ex.Message}", ex);
                }
                
                _logger.LogInformation("Nodo DD generado");
                return nodoDD.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar nodo DD");
                throw;
            }
        }

        public string FirmarDD(string dd, string clavePrivadaPEM)
        {
            // Este método ya no se usa directamente, pero se mantiene por compatibilidad
            // La firma se hace en GenerarTED
            return FirmarTexto(dd, ImportarClavePrivada(clavePrivadaPEM));
        }

        /// <summary>
        /// Extrae la clave privada RSA del nodo RSASK del CAF.
        /// Replica exactamente el método ExtraerClavePrivada del sistema legacy.
        /// </summary>
        private string ExtraerClavePrivada(string cafXml)
        {
            try
            {
                // CRÍTICO: Usar ObtenerCafBlindado() para obtener el XML limpio y parseado correctamente
                // En lugar de parsear directamente cafXml que puede tener múltiples elementos raíz
                var cafDocBlindado = ObtenerCafBlindado(cafXml);
                XmlDocument xmlDoc = cafDocBlindado;

                // Buscar el nodo RSASK (clave privada)
                XmlNode? nodoClave = xmlDoc.SelectSingleNode("//RSASK");
                if (nodoClave == null || string.IsNullOrEmpty(nodoClave.InnerText))
                {
                    _logger.LogError("No se encontró el nodo <RSASK> en el XML del CAF o está vacío");
                    throw new XmlException("No se encontró el nodo <RSASK> en el XML del CAF.");
                }

                string clavePrivada = nodoClave.InnerText.Trim();

                // Limpiar headers PEM existentes
                clavePrivada = clavePrivada
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Trim();

                // Re-formatear con headers PEM estándar
                string claveFormateada =
                    $"-----BEGIN RSA PRIVATE KEY-----\n{clavePrivada}\n-----END RSA PRIVATE KEY-----";

                return claveFormateada;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extraer clave privada del CAF");
                throw;
            }
        }

        /// <summary>
        /// Importa la clave privada PEM usando BouncyCastle.
        /// Este método es CRÍTICO y debe replicar EXACTAMENTE el proceso del legacy.
        /// 
        /// ⚠️ PUNTOS CRÍTICOS:
        /// - Usa PemReader de BouncyCastle 2.2.1
        /// - Cast a RsaPrivateCrtKeyParameters
        /// - Conversión manual de parámetros RSA (DotNetUtilities no disponible en BC 2.x)
        /// - Replica el método ImportarClavePrivada del sistema legacy
        /// </summary>
        private RSACryptoServiceProvider ImportarClavePrivada(string clavePrivadaPEM)
        {
            try
            {
                _logger.LogInformation("Importando clave privada RSA usando BouncyCastle");

                // Limpiar el PEM (quitar headers y saltos de línea)
                string claveLimpia = clavePrivadaPEM
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();

                // Re-formatear con headers PEM para BouncyCastle
                using (var sr = new StringReader(
                    $"-----BEGIN RSA PRIVATE KEY-----\n{claveLimpia}\n-----END RSA PRIVATE KEY-----"))
                {
                    // ⚠️ CRÍTICO: Usar PemReader de BouncyCastle
                    PemReader pemReader = new PemReader(sr);
                    object obj = pemReader.ReadObject();

                    if (obj is AsymmetricCipherKeyPair keyPair)
                    {
                        // ⚠️ CRÍTICO: Cast a RsaPrivateCrtKeyParameters
                        RsaPrivateCrtKeyParameters keyParams =
                            (RsaPrivateCrtKeyParameters)keyPair.Private;

                        // ⚠️ CRÍTICO: Conversión manual de parámetros RSA (BC 2.x no tiene DotNetUtilities)
                        // Esta conversión es FUNDAMENTAL para la compatibilidad con el sistema legacy
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

                        // Crear el RSACryptoServiceProvider con los parámetros convertidos
                        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                        rsa.ImportParameters(parametros);

                        return rsa;
                    }

                    throw new Exception("La clave privada no es válida o no se pudo leer como AsymmetricCipherKeyPair.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al importar clave privada usando BouncyCastle");
                throw;
            }
        }

        /// <summary>
        /// Firma un texto usando SHA1 + RSA.
        /// Replica exactamente el método FirmarTexto del sistema legacy.
        /// 
        /// ⚠️ PUNTOS CRÍTICOS:
        /// - Encoding UTF-8 para convertir string a bytes
        /// - SHA1 para el hash
        /// - SignHash() con OID SHA1 (NO usar SignData() directamente)
        /// - Base64 para la salida
        /// </summary>
        private string FirmarTexto(string texto, RSACryptoServiceProvider clavePrivada)
        {
            try
            {
                _logger.LogInformation("Firmando texto con SHA1 + RSA");

                // ⚠️ CRÍTICO: Convertir el texto a bytes usando UTF-8
                byte[] datos = Encoding.UTF8.GetBytes(texto);

                // Calcular hash SHA1
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(datos);

                    // ⚠️ CRÍTICO: Firmar el hash usando SignHash (NO SignData directamente)
                    // El OID "1.3.14.3.2.26" es el OID de SHA1
                    byte[] firma = clavePrivada.SignHash(hash, CryptoConfig.MapNameToOID("SHA1"));

                    if (firma == null || firma.Length == 0)
                    {
                        throw new Exception("La firma generada está vacía.");
                    }

                    // Convertir la firma a Base64
                    return Convert.ToBase64String(firma);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar texto con SHA1 + RSA");
                throw;
            }
        }

        /// <summary>
        /// Limpia el nodo DD eliminando espacios entre tags y saltos de línea.
        /// Esta función es CRÍTICA para la canonicalización del SII.
        /// Replica exactamente el método LimpiarNodoDD del sistema legacy.
        /// 
        /// ⚠️ CRÍTICO: Eliminar TODOS los espacios en blanco entre tags
        /// El SII es extremadamente estricto con el formato exacto
        /// </summary>
        private string LimpiarNodoDD(string nodoDD)
        {
            if (string.IsNullOrEmpty(nodoDD))
                return string.Empty;

            // Eliminar declaración XML si existe
            nodoDD = Regex.Replace(nodoDD, @"<\?xml[^>]*\?>", "");

            // ⚠️ CRÍTICO: Eliminar TODOS los espacios en blanco entre tags
            // El SII es extremadamente estricto con el formato exacto
            nodoDD = Regex.Replace(nodoDD, @">\s+<", "><");
            
            // Eliminar saltos de línea, retornos de carro y tabs
            nodoDD = nodoDD
                .Replace("\r\n", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Trim();
            
            return nodoDD;
        }

        /// <summary>
        /// Obtiene un CAF blindado (sanitizado) desde un input que puede contener HTML corrupto.
        /// Basado en código funcional probado que extrae quirúrgicamente el bloque AUTORIZACION.
        /// </summary>
        /// <param name="input">Contenido crudo que puede contener HTML corrupto o XML escapado</param>
        /// <returns>XmlDocument limpio y válido</returns>
        private XmlDocument ObtenerCafBlindado(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new Exception("El contenido del CAF está vacío.");

            string raw = input.Trim();

            // CRÍTICO: Verificar que NO sea una respuesta HTML del SII (página de error/login/tabla)
            // Si contiene tags HTML al inicio, es una respuesta HTTP HTML, no XML
            string inicioLower = raw.Length > 200 ? raw.Substring(0, 200).ToLowerInvariant() : raw.ToLowerInvariant();
            if (inicioLower.Contains("<html") || 
                inicioLower.Contains("<!doctype html") ||
                (inicioLower.Contains("<td") && inicioLower.Contains("<hr")) ||
                (inicioLower.Contains("<table") && !inicioLower.Contains("<autorizacion")))
            {
                _logger.LogError("Se recibió una respuesta HTML del SII en lugar de XML. Esto indica un error HTTP/endpoint.");
                _logger.LogError("Inicio del contenido recibido (primeros 500 caracteres): {Contenido}", 
                    raw.Length > 500 ? raw.Substring(0, 500) : raw);
                throw new Exception("Se recibió una respuesta HTML del SII en lugar de XML. Verifica la conexión y el endpoint. El contenido recibido parece ser una página HTML (posible error de autenticación o endpoint incorrecto).");
            }

            // Verificar que el contenido sea XML válido (debe empezar con <?xml o <AUTORIZACION)
            if (!raw.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && 
                !raw.StartsWith("<AUTORIZACION", StringComparison.OrdinalIgnoreCase) &&
                !raw.Contains("<AUTORIZACION", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("El contenido no parece ser XML válido. No contiene <?xml ni <AUTORIZACION>");
                _logger.LogError("Inicio del contenido recibido (primeros 500 caracteres): {Contenido}", 
                    raw.Length > 500 ? raw.Substring(0, 500) : raw);
                throw new Exception("El contenido del CAF no es XML válido. Debe empezar con <?xml o contener <AUTORIZACION>");
            }

            // 1) CRÍTICO: Eliminar declaración XML antes de intentar parsear para evitar "multiple root elements"
            // Si el raw tiene declaración XML, extraer solo el bloque AUTORIZACION directamente
            if (raw.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("CAF contiene declaración XML. Extrayendo bloque AUTORIZACION directamente para evitar múltiples elementos raíz");
                string bloqueInicial = ExtraerBloqueAutorizacion(raw);
                
                // CRÍTICO: Usar XmlReader para mayor tolerancia con el bloque extraído
                var docInicial = new XmlDocument { PreserveWhitespace = true };
                try
                {
                    // Intentar primero con LoadXml
                    docInicial.LoadXml(bloqueInicial);
                    _logger.LogInformation("CAF parseado después de extraer bloque AUTORIZACION (evitando declaración XML)");
                    return docInicial;
                }
                catch (XmlException xmlEx)
                {
                    _logger.LogWarning("Error al parsear bloque extraído con LoadXml: {Error}. Intentando con XmlReader...", xmlEx.Message);
                    // Si falla LoadXml, intentar con XmlReader que es más tolerante
                    try
                    {
                        using (var reader = XmlReader.Create(new StringReader(bloqueInicial)))
                        {
                            docInicial.Load(reader);
                            _logger.LogInformation("CAF parseado exitosamente usando XmlReader después de extraer bloque AUTORIZACION");
                            return docInicial;
                        }
                    }
                    catch (Exception readerEx)
                    {
                        _logger.LogError("Error al parsear bloque extraído con XmlReader: {Error}", readerEx.Message);
                        _logger.LogError("Bloque extraído (primeros 200 caracteres): {Bloque}", bloqueInicial.Length > 200 ? bloqueInicial.Substring(0, 200) : bloqueInicial);
                        // NO continuar con el flujo normal - lanzar excepción
                        throw new Exception($"No se pudo parsear el bloque AUTORIZACION extraído: {readerEx.Message}", readerEx);
                    }
                }
            }

            // 2) Intentar parsear directamente (caso optimista - sin declaración XML)
            var doc = new XmlDocument { PreserveWhitespace = true };
            try
            {
                doc.LoadXml(raw);
                _logger.LogInformation("CAF parseado directamente sin necesidad de limpieza");
                return doc;
            }
            catch (XmlException xmlEx)
            {
                // Si el error es por HTML corrupto mezclado, intentar limpiar antes de extraer bloque
                if (xmlEx.Message.Contains("hr") || xmlEx.Message.Contains("td") || xmlEx.Message.Contains("tr") || 
                    xmlEx.Message.Contains("start tag") || xmlEx.Message.Contains("end tag"))
                {
                    _logger.LogWarning("CAF contiene HTML corrupto mezclado (error: {Error}). Aplicando limpieza previa antes de parsear...", xmlEx.Message);
                    // Limpiar HTML del raw completo antes de intentar parsear
                    string rawLimpio = LimpiarTagsHTMLDelBloque(raw);
                    // Aplicar limpieza agresiva también
                    rawLimpio = LimpiarTagsHTMLDelBloqueAgresivo(rawLimpio);
                    try
                    {
                        doc = new XmlDocument { PreserveWhitespace = true };
                        doc.LoadXml(rawLimpio);
                        _logger.LogInformation("CAF parseado después de limpieza previa de HTML");
                        return doc;
                    }
                    catch (XmlException xmlEx2)
                    {
                        _logger.LogWarning("CAF aún no válido después de limpieza previa (error: {Error}), procediendo con extracción y limpieza", xmlEx2.Message);
                    }
                }
                // Si el error es por múltiples elementos raíz, extraer solo el bloque AUTORIZACION
                else if (xmlEx.Message.Contains("multiple root") || xmlEx.Message.Contains("root elements"))
                {
                    _logger.LogWarning("CAF tiene múltiples elementos raíz (error: {Error}). Extrayendo solo el bloque AUTORIZACION...", xmlEx.Message);
                    // Extraer solo el bloque AUTORIZACION sin declaración XML
                    string bloqueRaiz = ExtraerBloqueAutorizacion(raw);
                    // Asegurar que el bloque no tenga declaración XML
                    if (bloqueRaiz.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                    {
                        int inicioAUTORIZACION = bloqueRaiz.IndexOf("<AUTORIZACION", StringComparison.OrdinalIgnoreCase);
                        if (inicioAUTORIZACION > 0)
                        {
                            bloqueRaiz = bloqueRaiz.Substring(inicioAUTORIZACION).Trim();
                        }
                    }
                    try
                    {
                        doc = new XmlDocument { PreserveWhitespace = true };
                        doc.LoadXml(bloqueRaiz);
                        _logger.LogInformation("CAF parseado después de extraer solo bloque AUTORIZACION");
                        return doc;
                    }
                    catch (XmlException xmlEx3)
                    {
                        _logger.LogWarning("CAF aún no válido después de extracción (error: {Error}), procediendo con limpieza adicional", xmlEx3.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("CAF no válido directamente (error: {Error}), procediendo con extracción y limpieza", xmlEx.Message);
                }
            }

            // 2) Extraer el bloque AUTORIZACION sin decodificar todo
            string bloque = ExtraerBloqueAutorizacion(raw);
            
            // CRÍTICO: Asegurar que el bloque NO tenga declaración XML (causa múltiples elementos raíz)
            if (bloque.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                int inicioAUTORIZACION = bloque.IndexOf("<AUTORIZACION", StringComparison.OrdinalIgnoreCase);
                if (inicioAUTORIZACION > 0)
                {
                    bloque = bloque.Substring(inicioAUTORIZACION).Trim();
                    _logger.LogInformation("Declaración XML eliminada del bloque extraído en paso 2");
                }
            }
            
            // CRÍTICO: Validar que el bloque tenga solo un elemento raíz
            int cantidadAperturas = (bloque.Length - bloque.Replace("<AUTORIZACION", "").Length) / "<AUTORIZACION".Length;
            int cantidadCierres = (bloque.Length - bloque.Replace("</AUTORIZACION>", "").Length) / "</AUTORIZACION>".Length;
            if (cantidadAperturas != 1 || cantidadCierres != 1)
            {
                _logger.LogError("El bloque extraído tiene {Aperturas} aperturas y {Cierres} cierres. Debe tener exactamente 1 de cada una.", cantidadAperturas, cantidadCierres);
                _logger.LogError("Bloque extraído (primeros 500 caracteres): {Bloque}", bloque.Length > 500 ? bloque.Substring(0, 500) : bloque);
                throw new Exception($"El bloque extraído tiene múltiples elementos AUTORIZACION: {cantidadAperturas} aperturas, {cantidadCierres} cierres");
            }

            // 3) CRÍTICO: Verificar que el bloque extraído NO contenga HTML mezclado
            // Buscar en TODO el bloque, no solo en los primeros 500 caracteres
            string bloqueLower = bloque.ToLowerInvariant();
            bool contieneHTML = bloqueLower.Contains("<td") || bloqueLower.Contains("<hr") || bloqueLower.Contains("<tr") || 
                                bloqueLower.Contains("<table") || bloqueLower.Contains("<div") || bloqueLower.Contains("<span") ||
                                bloqueLower.Contains("</td>") || bloqueLower.Contains("</hr>") || bloqueLower.Contains("</tr>");
            
            if (contieneHTML)
            {
                _logger.LogWarning("El bloque AUTORIZACION contiene tags HTML mezclados. Aplicando limpieza automática antes de continuar...");
                
                // En lugar de lanzar excepción, limpiar automáticamente el HTML
                bloque = LimpiarTagsHTMLDelBloque(bloque);
                
                // Verificar nuevamente después de la limpieza
                string bloqueLimpioLowerVerif = bloque.ToLowerInvariant();
                bool bloqueLimpioContieneHTMLVerif = bloqueLimpioLowerVerif.Contains("<td") || bloqueLimpioLowerVerif.Contains("<hr") || 
                                                bloqueLimpioLowerVerif.Contains("<tr") || bloqueLimpioLowerVerif.Contains("</td>") ||
                                                bloqueLimpioLowerVerif.Contains("</hr>") || bloqueLimpioLowerVerif.Contains("</tr>");
                
                if (bloqueLimpioContieneHTMLVerif)
                {
                    // Encontrar la posición exacta del HTML para logging
                    int posHTML = -1;
                    if (bloqueLimpioLowerVerif.Contains("<td")) posHTML = bloqueLimpioLowerVerif.IndexOf("<td");
                    else if (bloqueLimpioLowerVerif.Contains("<hr")) posHTML = bloqueLimpioLowerVerif.IndexOf("<hr");
                    else if (bloqueLimpioLowerVerif.Contains("<tr")) posHTML = bloqueLimpioLowerVerif.IndexOf("<tr");
                    
                    string contexto = posHTML >= 0 && posHTML < bloque.Length - 200 
                        ? bloque.Substring(Math.Max(0, posHTML - 100), Math.Min(300, bloque.Length - Math.Max(0, posHTML - 100)))
                        : bloque.Substring(0, Math.Min(500, bloque.Length));
                    
                    _logger.LogError("El bloque AUTORIZACION todavía contiene tags HTML después de limpieza automática en posición aproximada {Pos}.", posHTML);
                    _logger.LogError("Contexto del HTML encontrado: {Contexto}", contexto);
                    _logger.LogError("Bloque completo (primeros 2000 caracteres): {Bloque}", 
                        bloque.Length > 2000 ? bloque.Substring(0, 2000) : bloque);
                    throw new Exception($"El CAF almacenado en la base de datos contiene HTML corrupto mezclado con XML que no se pudo limpiar automáticamente. El bloque AUTORIZACION todavía contiene tags HTML (<td>, <hr>, <tr>, etc.) en la posición {posHTML}. Debes limpiar el CAF manualmente en la base de datos o descargarlo nuevamente del SII.");
                }
                else
                {
                    _logger.LogInformation("HTML eliminado exitosamente del bloque AUTORIZACION mediante limpieza automática");
                }
            }

            // 4) Si el bloque venía HTML-escapado, decodificar SOLO el bloque (no el documento completo)
            if (ContieneAutorizacionEscapada(bloque))
            {
                bloque = WebUtility.HtmlDecode(bloque);
                _logger.LogInformation("Bloque AUTORIZACION decodificado de HTML entities");
            }

            // 5) CRÍTICO: Eliminar tags HTML que puedan estar mezclados dentro del bloque (por si acaso)
            // Hacerlo de forma iterativa hasta que el XML sea válido
            bloque = LimpiarTagsHTMLDelBloque(bloque);

            // 6) Verificar nuevamente que el bloque limpio NO contenga HTML antes de parsear
            string bloqueLimpioLower = bloque.Length > 500 ? bloque.Substring(0, 500).ToLowerInvariant() : bloque.ToLowerInvariant();
            if (bloqueLimpioLower.Contains("<td") || bloqueLimpioLower.Contains("<hr") || bloqueLimpioLower.Contains("<tr"))
            {
                _logger.LogError("El bloque limpio todavía contiene tags HTML. La limpieza no fue efectiva.");
                _logger.LogError("Bloque limpio (primeros 1000 caracteres): {Bloque}", 
                    bloque.Length > 1000 ? bloque.Substring(0, 1000) : bloque);
                throw new Exception("El CAF contiene HTML corrupto que no se pudo limpiar automáticamente. Debes limpiar el CAF manualmente en la base de datos.");
            }

            // 7) Intentar parsear después de la limpieza
            doc = new XmlDocument { PreserveWhitespace = true };
            try
            {
                doc.LoadXml(bloque);
                _logger.LogInformation("CAF blindado validado correctamente después de limpieza. Longitud original: {Original}, Longitud bloque: {Bloque}", 
                    input.Length, bloque.Length);
                return doc;
            }
            catch (XmlException ex)
            {
                // Si aún falla, intentar una limpieza más agresiva
                _logger.LogWarning("Primera limpieza no fue suficiente, intentando limpieza más agresiva...");
                bloque = LimpiarTagsHTMLDelBloqueAgresivo(bloque);
                
                try
                {
                    doc = new XmlDocument { PreserveWhitespace = true };
                    doc.LoadXml(bloque);
                    _logger.LogInformation("CAF blindado validado después de limpieza agresiva");
                    return doc;
                }
                catch (XmlException ex2)
                {
                    string snippet = bloque.Length > 500 ? bloque.Substring(0, 500) : bloque;
                    _logger.LogError(ex2, "CAF inválido tras limpieza agresiva. Error: {Message}. Línea {Line}, posición {Pos}. Inicio: {Snippet}", 
                        ex2.Message, ex2.LineNumber, ex2.LinePosition, snippet);
                    throw new Exception($"CAF inválido tras limpieza agresiva. {ex2.Message}. Inicio: {snippet}", ex2);
                }
            }
        }

        /// <summary>
        /// Limpia tags HTML del bloque XML extraído de forma iterativa
        /// </summary>
        private string LimpiarTagsHTMLDelBloque(string bloque)
        {
            // Lista completa de tags HTML que pueden corromper el XML
            // IMPORTANTE: NO incluir "td" porque <TD> es un tag válido del XML del CAF (Tipo Documento)
            // Solo eliminar tags HTML reales, no tags XML válidos del CAF
            string[] etiquetasBasura = new[] { 
                "hr", "tr", "table", "tbody", "thead", "tfoot", "div", "span", "br", "p", 
                "html", "head", "body", "th", "font", "style", "script", "meta", "link", "title",
                "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "dl", "dt", "dd",
                "form", "input", "button", "select", "option", "textarea", "label"
            };
            
            // Primero eliminar tags HTML seguros
            string bloqueLimpio = bloque;
            foreach (var tag in etiquetasBasura)
            {
                bloqueLimpio = Regex.Replace(bloqueLimpio, $@"<{tag}\b[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                bloqueLimpio = Regex.Replace(bloqueLimpio, $@"</{tag}>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                bloqueLimpio = Regex.Replace(bloqueLimpio, $@"<{tag}\s*/>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            
            // Luego eliminar <td> y </td> solo si NO están en contexto XML válido (<TD>número</TD>)
            // Eliminar <td> solo si está claramente fuera de contexto XML válido
            if (bloqueLimpio.Contains("<td", StringComparison.OrdinalIgnoreCase) && 
                !Regex.IsMatch(bloqueLimpio, @"<TD>\d+</TD>", RegexOptions.IgnoreCase))
            {
                bloqueLimpio = Regex.Replace(bloqueLimpio, @"<td\b[^>]*>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                bloqueLimpio = Regex.Replace(bloqueLimpio, @"</td>", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            
            return bloqueLimpio;
        }

        /// <summary>
        /// Limpieza más agresiva que también elimina líneas completas que contengan solo HTML
        /// </summary>
        private string LimpiarTagsHTMLDelBloqueAgresivo(string bloque)
        {
            _logger.LogWarning("Aplicando limpieza agresiva de HTML");
            
            // Primero aplicar la limpieza normal
            string bloqueLimpio = LimpiarTagsHTMLDelBloque(bloque);
            
            // Luego eliminar líneas que contengan solo tags HTML o espacios
            string[] lineas = bloqueLimpio.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lineasLimpias = new List<string>();
            
            foreach (string linea in lineas)
            {
                string lineaTrim = linea.Trim();
                // Si la línea está vacía o contiene solo tags HTML, omitirla
                if (string.IsNullOrWhiteSpace(lineaTrim))
                {
                    continue;
                }
                
                // Si la línea contiene solo tags HTML (sin contenido XML válido), omitirla
                if (Regex.IsMatch(lineaTrim, @"^[\s<>\/]*$"))
                {
                    continue;
                }
                
                lineasLimpias.Add(linea);
            }
            
            string resultado = string.Join("\n", lineasLimpias);
            _logger.LogInformation("Limpieza agresiva completada. Líneas eliminadas: {Eliminadas}", lineas.Length - lineasLimpias.Count);
            
            return resultado;
        }

        private static bool ContieneAutorizacionEscapada(string s)
            => s.IndexOf("&lt;AUTORIZACION", StringComparison.OrdinalIgnoreCase) >= 0;

        private string ExtraerBloqueAutorizacion(string cafRaw)
        {
            if (string.IsNullOrWhiteSpace(cafRaw)) 
                throw new ArgumentException("El contenido del CAF está vacío.");

            // Regex para capturar todo desde <AUTORIZACION> hasta </AUTORIZACION> (ignorando mayúsculas/minúsculas)
            // Singleline: permite que el punto (.) coincida con saltos de línea.
            var match = Regex.Match(cafRaw, @"<AUTORIZACION>.*?</AUTORIZACION>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Intento de rescate: a veces el tag tiene atributos (ej: <AUTORIZACION xmlns="...">)
                match = Regex.Match(cafRaw, @"<AUTORIZACION\b.*?</AUTORIZACION>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            }

            if (!match.Success)
            {
                // Caso B: XML escapado dentro de HTML
                int ie = cafRaw.IndexOf("&lt;AUTORIZACION", StringComparison.OrdinalIgnoreCase);
                if (ie >= 0)
                {
                    int je = cafRaw.IndexOf("&lt;/AUTORIZACION&gt;", ie, StringComparison.OrdinalIgnoreCase);
                    if (je < 0) 
                    {
                        _logger.LogError("No se encontró cierre &lt;/AUTORIZACION&gt; en CAF.");
                        throw new Exception("No se encontró cierre &lt;/AUTORIZACION&gt; en CAF.");
                    }
                    je += "&lt;/AUTORIZACION&gt;".Length;
                    string bloque = cafRaw.Substring(ie, je - ie).Trim();
                    _logger.LogInformation("Bloque AUTORIZACION extraído (XML escapado), longitud: {Length}", bloque.Length);
                    return bloque;
                }
                
                _logger.LogError("No se encontró un bloque <AUTORIZACION> válido en el CAF.");
                throw new Exception("No se encontró un bloque <AUTORIZACION> válido en el CAF.");
            }

            string bloqueLimpio = match.Value;

            // LIMPIEZA QUIRÚRGICA ADICIONAL:
            // Eliminar cualquier caracter que no sea XML válido al inicio (BOM, espacios raros)
            bloqueLimpio = bloqueLimpio.TrimStart('\uFEFF', '\u200B', ' ', '\t', '\r', '\n');
            
            int indicePrimerTag = bloqueLimpio.IndexOf("<");
            if (indicePrimerTag > 0)
            {
                bloqueLimpio = bloqueLimpio.Substring(indicePrimerTag);
            }

            // CRÍTICO: Asegurar que el bloque NO tenga declaración XML
            if (bloqueLimpio.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                int inicioAUTORIZACION = bloqueLimpio.IndexOf("<AUTORIZACION", StringComparison.OrdinalIgnoreCase);
                if (inicioAUTORIZACION > 0)
                {
                    bloqueLimpio = bloqueLimpio.Substring(inicioAUTORIZACION).Trim();
                    _logger.LogInformation("Declaración XML eliminada del bloque extraído por regex");
                }
            }

            // CRÍTICO: Asegurar que el bloque empiece exactamente con <AUTORIZACION
            if (!bloqueLimpio.TrimStart().StartsWith("<AUTORIZACION", StringComparison.OrdinalIgnoreCase))
            {
                int inicioAUTORIZACION = bloqueLimpio.IndexOf("<AUTORIZACION", StringComparison.OrdinalIgnoreCase);
                if (inicioAUTORIZACION > 0)
                {
                    bloqueLimpio = bloqueLimpio.Substring(inicioAUTORIZACION).Trim();
                    _logger.LogInformation("Contenido adicional antes de <AUTORIZACION eliminado");
                }
            }

            // Validar que el bloque tenga solo un elemento raíz
            int cantidadAperturas = (bloqueLimpio.Length - bloqueLimpio.Replace("<AUTORIZACION", "").Length) / "<AUTORIZACION".Length;
            int cantidadCierres = (bloqueLimpio.Length - bloqueLimpio.Replace("</AUTORIZACION>", "").Length) / "</AUTORIZACION>".Length;
            
            if (cantidadAperturas != 1 || cantidadCierres != 1)
            {
                _logger.LogError("El bloque extraído por regex tiene {Aperturas} aperturas y {Cierres} cierres. Debe tener exactamente 1 de cada una.", cantidadAperturas, cantidadCierres);
                _logger.LogError("Bloque extraído (primeros 500 caracteres): {Bloque}", bloqueLimpio.Length > 500 ? bloqueLimpio.Substring(0, 500) : bloqueLimpio);
                throw new Exception($"El bloque extraído tiene múltiples elementos AUTORIZACION: {cantidadAperturas} aperturas, {cantidadCierres} cierres");
            }

            // CRÍTICO: Asegurar que el bloque termine exactamente con </AUTORIZACION>
            bloqueLimpio = bloqueLimpio.Trim();
            if (!bloqueLimpio.EndsWith("</AUTORIZACION>", StringComparison.OrdinalIgnoreCase))
            {
                int ultimoCierre = bloqueLimpio.LastIndexOf("</AUTORIZACION>", StringComparison.OrdinalIgnoreCase);
                if (ultimoCierre > 0)
                {
                    bloqueLimpio = bloqueLimpio.Substring(0, ultimoCierre + "</AUTORIZACION>".Length).Trim();
                    _logger.LogInformation("Contenido adicional después de </AUTORIZACION> eliminado");
                }
            }

            _logger.LogInformation("Bloque AUTORIZACION extraído (usando regex), longitud: {Length}, aperturas: {Aperturas}, cierres: {Cierres}", 
                bloqueLimpio.Length, cantidadAperturas, cantidadCierres);
            return bloqueLimpio;
        }
    }
}
