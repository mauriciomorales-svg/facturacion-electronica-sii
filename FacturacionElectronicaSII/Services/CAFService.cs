using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.CAF;
using FacturacionElectronicaSII.Helpers;
// using MySqlConnector; // ⚠️ DESACTIVADO: No usar MySQL por ahora
using System.Collections.Generic;
// using System.Data; // ⚠️ DESACTIVADO: No usar MySQL por ahora
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio real para gestión de CAFs desde base de datos
    /// </summary>
    public class CAFService : ICAFService
    {
        private readonly ILogger<CAFService> _logger;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<int, HashSet<int>> _foliosUsados = new();

        public CAFService(ILogger<CAFService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        // ⚠️ DESACTIVADO: No usar MySQL por ahora
        // private string GetConnectionString()
        // {
        //     return _configuration.GetConnectionString("DefaultConnection") 
        //         ?? throw new InvalidOperationException("No se ha configurado la cadena de conexión");
        // }

        /// <summary>
        /// Obtiene ruta del archivo JSON para guardar folios usados
        /// </summary>
        private string GetFoliosUsadosFilePath()
        {
            var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (!Directory.Exists(dataPath))
                Directory.CreateDirectory(dataPath);
            return Path.Combine(dataPath, "FoliosUsados.json");
        }

        /// <summary>
        /// Carga folios usados desde archivo JSON (sin BD)
        /// </summary>
        private async Task<HashSet<int>> CargarFoliosUsadosAsync(int tipoDTE)
        {
            var filePath = GetFoliosUsadosFilePath();
            if (!File.Exists(filePath))
                return new HashSet<int>();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(json);
                if (data != null && data.ContainsKey(tipoDTE))
                    return new HashSet<int>(data[tipoDTE]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al cargar folios usados desde archivo. Iniciando limpio.");
            }
            return new HashSet<int>();
        }

        /// <summary>
        /// Guarda folios usados en archivo JSON (sin BD)
        /// </summary>
        private async Task GuardarFoliosUsadosAsync(int tipoDTE, HashSet<int> folios)
        {
            var filePath = GetFoliosUsadosFilePath();
            Dictionary<int, List<int>> data;

            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                data = JsonSerializer.Deserialize<Dictionary<int, List<int>>>(json) ?? new Dictionary<int, List<int>>();
            }
            else
            {
                data = new Dictionary<int, List<int>>();
            }

            data[tipoDTE] = folios.ToList();
            var newJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, newJson);
        }

        public async Task<int> ObtenerFolioDisponibleAsync(int tipoDTE)
        {
            _logger.LogInformation("Obteniendo folio disponible para tipo DTE {TipoDTE}", tipoDTE);

            var caf = await ObtenerCAFAsync(tipoDTE);
            if (caf == null)
            {
                throw new InvalidOperationException($"No hay CAF disponible para tipo DTE {tipoDTE}");
            }

            // ✅ NUEVO: Obtener folios usados desde archivo JSON (sin BD)
            var foliosUsados = await CargarFoliosUsadosAsync(tipoDTE);
            _logger.LogInformation("Folios usados cargados desde archivo: {Count}", foliosUsados.Count);

            for (int folio = caf.FolioInicial; folio <= caf.FolioFinal; folio++)
            {
                if (!foliosUsados.Contains(folio))
                {
                    _logger.LogInformation("Folio {Folio} disponible para tipo DTE {TipoDTE}", folio, tipoDTE);
                    return folio;
                }
            }

            throw new InvalidOperationException($"No hay folios disponibles para tipo DTE {tipoDTE}");
        }

        public async Task<int> VerProximoFolioAsync(int tipoDTE)
        {
            return await ObtenerFolioDisponibleAsync(tipoDTE);
        }

        public async Task<CAFData?> ObtenerCAFAsync(int tipoDTE)
        {
            _logger.LogInformation("Obteniendo CAF para tipo DTE {TipoDTE}", tipoDTE);

            // PRIORIDAD 1: Intentar leer desde archivo descargado del SII (carpeta Data/CAFs)
            var rutaCAFsBase = _configuration["FacturacionElectronica:Rutas:CAFs"] ?? "./Data/CAFs";
            
            // Determinar subcarpeta según tipo de DTE
            string subcarpeta = tipoDTE switch
            {
                33 => "Tipo_33_Facturas",
                61 => "Tipo_61_NotasCredito",
                56 => "Tipo_56_NotasDebito",
                39 => "Tipo_39_Boletas",
                _ => ""
            };
            
            var rutaCAFs = !string.IsNullOrEmpty(subcarpeta) 
                ? Path.Combine(rutaCAFsBase, subcarpeta)
                : rutaCAFsBase;
            
            // Extraer solo los números del RUT (sin dígito verificador) para buscar el CAF
            var rutCompleto = _configuration["FacturacionElectronica:Emisor:RUT"]?.Replace(".", "").Replace("-", "") ?? "";
            // El nombre del CAF del SII usa solo los números, sin el dígito verificador
            var rutEmisor = rutCompleto.Length > 1 ? rutCompleto.Substring(0, rutCompleto.Length - 1) : rutCompleto;
            var patronCAF = $"FoliosSII{rutEmisor}{tipoDTE}*.xml";

            // Buscar el CAF más reciente que coincida con el RUT y tipo DTE
            string rutaCAFDirecto = "";
            var directorioCAFs = Path.GetFullPath(rutaCAFs);
            if (Directory.Exists(directorioCAFs))
            {
                var archivosCAF = Directory.GetFiles(directorioCAFs, patronCAF)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(archivosCAF))
                {
                    rutaCAFDirecto = archivosCAF;
                    _logger.LogInformation("CAF encontrado en carpeta {Subcarpeta}: {Ruta}", subcarpeta, rutaCAFDirecto);
                }
                else
                {
                    _logger.LogWarning("No se encontró archivo CAF para tipo DTE {TipoDTE}. Coloca el archivo en: {Ruta}", tipoDTE, directorioCAFs);
                }
            }
            else
            {
                _logger.LogError("No existe el directorio: {Directorio}", directorioCAFs);
                throw new InvalidOperationException($"No se encontró archivo CAF para tipo DTE {tipoDTE}. Coloca el archivo en: {directorioCAFs}");
            }
            
            if (!string.IsNullOrEmpty(rutaCAFDirecto) && File.Exists(rutaCAFDirecto))
            {
                try
                {
                    _logger.LogInformation("Leyendo CAF directamente desde archivo del SII: {Ruta}", rutaCAFDirecto);
                    var xmlData = await File.ReadAllTextAsync(rutaCAFDirecto, Encoding.UTF8);
                    
                    if (string.IsNullOrWhiteSpace(xmlData))
                    {
                        _logger.LogWarning("El archivo CAF está vacío");
                        return null;
                    }

                    _logger.LogInformation("CAF leído desde archivo, longitud: {Length} caracteres", xmlData.Length);
                    
                    // Obtener CAF blindado (sanitizado) usando método funcional probado
                    var cafDocBlindado = ObtenerCafBlindado(xmlData);
                    
                    // Verificar que contiene el nodo CAF
                    var cafNode = cafDocBlindado.SelectSingleNode("//CAF");
                    if (cafNode == null)
                    {
                        _logger.LogError("El XML del CAF no contiene el nodo <CAF>");
                        throw new InvalidOperationException("El XML del CAF no contiene el nodo <CAF>");
                    }
                    
                    // Extraer rangos de folios del XML
                    var daNode = cafNode.SelectSingleNode(".//DA");
                    int folioInicial = 0, folioFinal = 0;
                    DateTime fechaAutorizacion = DateTime.Now;
                    
                    if (daNode != null)
                    {
                        var rngNode = daNode.SelectSingleNode(".//RNG");
                        if (rngNode != null)
                        {
                            var dNode = rngNode.SelectSingleNode(".//D");
                            var hNode = rngNode.SelectSingleNode(".//H");
                            if (dNode != null && int.TryParse(dNode.InnerText, out int d))
                                folioInicial = d;
                            if (hNode != null && int.TryParse(hNode.InnerText, out int h))
                                folioFinal = h;
                        }
                        
                        var faNode = daNode.SelectSingleNode(".//FA");
                        if (faNode != null && DateTime.TryParse(faNode.InnerText, out DateTime fa))
                            fechaAutorizacion = fa;
                    }
                    
                    _logger.LogInformation("Rango de folios extraído del XML: {FolioInicial} - {FolioFinal}", folioInicial, folioFinal);
                    
                    // CRÍTICO: Pasar el XML original (raw) a ParsearCAF, no el OuterXml
                    // ParsearCAF usará ObtenerCafBlindado internamente para sanitizarlo correctamente
                    return ParsearCAF(xmlData, tipoDTE, folioInicial, folioFinal, fechaAutorizacion, "", "");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al leer CAF desde archivo");
                    throw;
                }
            }
            
            // ⚠️ SIN BASE DE DATOS: Si no se encontró el archivo, retornar null
            _logger.LogError("Archivo CAF no encontrado para RUT {RUT} y tipo DTE {TipoDTE} en {Directorio}",
                rutEmisor, tipoDTE, directorioCAFs);
            throw new InvalidOperationException($"No se encontró archivo CAF para tipo DTE {tipoDTE}. Coloca el archivo en: {directorioCAFs}");
            
            // ⚠️ CÓDIGO DE BASE DE DATOS DESACTIVADO - NO SE USA
            // // PRIORIDAD 2: Leer desde base de datos (fallback)
            // _logger.LogInformation("Obteniendo CAF para tipo DTE {TipoDTE} desde base de datos", tipoDTE);
            // try
            // {
            //     using var connection = new MySqlConnection(GetConnectionString());
            //     await connection.OpenAsync();
            //     ...
            // }
        }

        public async Task<bool> MarcarFolioUsadoAsync(int tipoDTE, int folio)
        {
            _logger.LogInformation("Marcando folio {Folio} como usado para tipo DTE {TipoDTE}", folio, tipoDTE);

            try
            {
                // ✅ NUEVO: Usar archivo JSON en lugar de BD
                var foliosUsados = await CargarFoliosUsadosAsync(tipoDTE);
                
                if (foliosUsados.Contains(folio))
                {
                    _logger.LogWarning("El folio {Folio} ya estaba marcado como usado", folio);
                    return false;
                }
                
                foliosUsados.Add(folio);
                await GuardarFoliosUsadosAsync(tipoDTE, foliosUsados);
                
                _logger.LogInformation("Folio {Folio} marcado como usado exitosamente", folio);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar folio como usado");
                throw;
            }
            
            // ⚠️ CÓDIGO DE BASE DE DATOS DESACTIVADO - NO SE USA
            // try
            // {
            //     using var connection = new MySqlConnection(GetConnectionString());
            //     await connection.OpenAsync();
            //     ...
            // }
        }

        public async Task<int> FoliosDisponiblesAsync(int tipoDTE)
        {
            try
            {
                _logger.LogInformation("Calculando folios disponibles para tipo DTE {TipoDTE}", tipoDTE);
                
                var caf = await ObtenerCAFAsync(tipoDTE);
                if (caf == null)
                {
                    _logger.LogWarning("No se encontró CAF para tipo DTE {TipoDTE}", tipoDTE);
                    return 0;
                }

                _logger.LogInformation("CAF encontrado: Rango {FolioInicial} - {FolioFinal}", caf.FolioInicial, caf.FolioFinal);
                
                // ✅ NUEVO: Usar archivo JSON en lugar de BD
                var foliosUsados = await CargarFoliosUsadosAsync(tipoDTE);
                var totalFolios = (caf.FolioFinal - caf.FolioInicial + 1);
                var disponibles = totalFolios - foliosUsados.Count;
                
                _logger.LogInformation("Folios totales: {Total}, Usados: {Usados}, Disponibles: {Disponibles}", 
                    totalFolios, foliosUsados.Count, disponibles);
                
                return disponibles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar folios disponibles para tipo DTE {TipoDTE}", tipoDTE);
                return 0;
            }
        }

        // ⚠️ MÉTODO DESACTIVADO - AHORA SE USA CargarFoliosUsadosAsync (archivo JSON)
        // private async Task<HashSet<int>> ObtenerFoliosUsadosAsync(int tipoDTE, int folioInicial, int folioFinal)
        // {
        //     var foliosUsados = new HashSet<int>();
        //     try
        //     {
        //         using var connection = new MySqlConnection(GetConnectionString());
        //         await connection.OpenAsync();
        //         ...
        //     }
        //     return foliosUsados;
        // }

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

            // Log snippet inicial de 300 caracteres
            string snippet = raw.Length > 300 ? raw.Substring(0, 300) : raw;
            _logger.LogInformation("[CAFService] Snippet inicial (300 chars): {Snippet}", snippet);

            // CRÍTICO: Verificar que NO sea una respuesta HTML del SII (página de error/login/tabla)
            // Primero verificar que sea XML válido antes de buscar HTML
            bool esXmlValido = raw.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || 
                              raw.StartsWith("<AUTORIZACION", StringComparison.OrdinalIgnoreCase) ||
                              raw.Contains("<AUTORIZACION", StringComparison.OrdinalIgnoreCase);
            
            // Solo buscar HTML si NO es XML válido obvio
            bool esHtml = false;
            if (!esXmlValido)
            {
                // Revisar primeros 1200 caracteres como en SiiResponseGuard
                string sample = raw.Length > 1200 ? raw.Substring(0, 1200).ToLowerInvariant() : raw.ToLowerInvariant();
                // Buscar patrones HTML específicos que NO aparecen en XML válido
                esHtml = sample.Contains("<html") ||
                         sample.Contains("<!doctype html") ||
                         (sample.Contains("<table") && !sample.Contains("<autorizacion")) ||
                         (sample.Contains("<td") && sample.Contains("<hr") && !sample.Contains("<autorizacion")) ||
                         sample.Contains("text/html");
            }

            if (esHtml)
            {
                // Guardar dump antes de lanzar excepción
                try
                {
                    string rutaCompleta = PathHelper.GetDumpPath("CAF_HTML_DETECTADO");
                    File.WriteAllText(rutaCompleta, raw, Encoding.UTF8);
                    _logger.LogError("HTML detectado en CAF. Dump guardado en: {Ruta}", rutaCompleta);
                    throw new Exception($"El contenido recibido es HTML (página del SII), no XML válido. Dump guardado en: {rutaCompleta}");
                }
                catch (Exception ex) when (!ex.Message.Contains("HTML"))
                {
                    _logger.LogError(ex, "Error al guardar dump de HTML");
                    throw new Exception("El contenido recibido es HTML (página del SII), no XML válido. Error al guardar dump.");
                }
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
                    // Guardar dump del bloque corrupto antes de lanzar excepción
                    string dumpPath = "";
                    try
                    {
                        dumpPath = PathHelper.GetDumpPath("CAF_BLOQUE_HTML");
                        File.WriteAllText(dumpPath, bloque, Encoding.UTF8);
                        _logger.LogError("Bloque AUTORIZACION con HTML detectado después de limpieza. Dump guardado en: {Ruta}", dumpPath);
                    }
                    catch (Exception dumpEx)
                    {
                        _logger.LogWarning(dumpEx, "No se pudo guardar dump del bloque corrupto");
                    }

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
                    
                    string mensajeError = $"El CAF almacenado en la base de datos contiene HTML corrupto mezclado con XML que no se pudo limpiar automáticamente. El bloque AUTORIZACION todavía contiene tags HTML (<td>, <hr>, <tr>, etc.) en la posición {posHTML}. Debes limpiar el CAF manualmente en la base de datos o descargarlo nuevamente del SII.";
                    if (!string.IsNullOrEmpty(dumpPath))
                    {
                        mensajeError += $" Dump guardado en: {dumpPath}";
                    }
                    throw new Exception(mensajeError);
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
            // Buscar en TODO el bloque limpio, no solo en los primeros 500 caracteres
            string bloqueLimpioLower = bloque.ToLowerInvariant();
            bool bloqueLimpioContieneHTML = bloqueLimpioLower.Contains("<td") || bloqueLimpioLower.Contains("<hr") || 
                                            bloqueLimpioLower.Contains("<tr") || bloqueLimpioLower.Contains("</td>") ||
                                            bloqueLimpioLower.Contains("</hr>") || bloqueLimpioLower.Contains("</tr>");
            
            if (bloqueLimpioContieneHTML)
            {
                // Guardar dump del bloque limpio que todavía contiene HTML
                string dumpPath = "";
                try
                {
                    dumpPath = PathHelper.GetDumpPath("CAF_BLOQUE_LIMPIO_HTML");
                    File.WriteAllText(dumpPath, bloque, Encoding.UTF8);
                    _logger.LogError("Bloque limpio que todavía contiene HTML. Dump guardado en: {Ruta}", dumpPath);
                }
                catch (Exception dumpEx)
                {
                    _logger.LogWarning(dumpEx, "No se pudo guardar dump del bloque limpio corrupto");
                }

                // Encontrar la posición exacta del HTML restante
                int posHTMLRestante = -1;
                if (bloqueLimpioLower.Contains("<td")) posHTMLRestante = bloqueLimpioLower.IndexOf("<td");
                else if (bloqueLimpioLower.Contains("<hr")) posHTMLRestante = bloqueLimpioLower.IndexOf("<hr");
                else if (bloqueLimpioLower.Contains("<tr")) posHTMLRestante = bloqueLimpioLower.IndexOf("<tr");
                
                string contextoRestante = posHTMLRestante >= 0 && posHTMLRestante < bloque.Length - 200 
                    ? bloque.Substring(Math.Max(0, posHTMLRestante - 100), Math.Min(300, bloque.Length - Math.Max(0, posHTMLRestante - 100)))
                    : bloque.Substring(0, Math.Min(500, bloque.Length));
                
                _logger.LogError("El bloque limpio todavía contiene tags HTML en posición {Pos}. La limpieza automática no fue efectiva.", posHTMLRestante);
                _logger.LogError("Contexto del HTML restante: {Contexto}", contextoRestante);
                _logger.LogError("Bloque limpio completo (primeros 2000 caracteres): {Bloque}", 
                    bloque.Length > 2000 ? bloque.Substring(0, 2000) : bloque);
                
                string mensajeError = $"El CAF contiene HTML corrupto que no se pudo limpiar automáticamente. Tags HTML encontrados en la posición {posHTMLRestante} después de la limpieza. Debes limpiar el CAF manualmente en la base de datos o descargarlo nuevamente del SII.";
                if (!string.IsNullOrEmpty(dumpPath))
                {
                    mensajeError += $" Dump guardado en: {dumpPath}";
                }
                throw new Exception(mensajeError);
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
                    string snippetError = bloque.Length > 500 ? bloque.Substring(0, 500) : bloque;
                    _logger.LogError(ex2, "CAF inválido tras limpieza agresiva. Error: {Message}. Línea {Line}, posición {Pos}. Inicio: {Snippet}", 
                        ex2.Message, ex2.LineNumber, ex2.LinePosition, snippetError);
                    throw new Exception($"CAF inválido tras limpieza agresiva. {ex2.Message}. Inicio: {snippetError}", ex2);
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

        /// <summary>
        /// Limpia el XML del CAF extrayendo solo la parte XML válida, eliminando HTML corrupto
        /// [DEPRECADO] Usar SanitizeCafXml en su lugar
        /// </summary>
        private string LimpiarCAFConHTML(string xmlData)
        {
            try
            {
                // Verificar si contiene HTML corrupto (case insensitive)
                bool tieneHTML = xmlData.Contains("<td>", StringComparison.OrdinalIgnoreCase) ||
                                xmlData.Contains("</td>", StringComparison.OrdinalIgnoreCase) ||
                                xmlData.Contains("<hr>", StringComparison.OrdinalIgnoreCase) ||
                                xmlData.Contains("<hr ", StringComparison.OrdinalIgnoreCase) ||
                                xmlData.Contains("<html>", StringComparison.OrdinalIgnoreCase) ||
                                xmlData.Contains("<table>", StringComparison.OrdinalIgnoreCase);

                if (!tieneHTML)
                {
                    _logger.LogInformation("El XML del CAF no contiene HTML corrupto, se usa tal cual");
                    return xmlData;
                }

                _logger.LogWarning("El XML del CAF contiene HTML corrupto. Limpiando... Longitud original: {Length}", xmlData.Length);

                // Buscar el inicio del XML válido (buscar desde el principio)
                int inicioXML = xmlData.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
                if (inicioXML < 0)
                {
                    inicioXML = xmlData.IndexOf("<AUTORIZACION>", StringComparison.OrdinalIgnoreCase);
                    if (inicioXML < 0)
                    {
                        inicioXML = xmlData.IndexOf("<CAF", StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (inicioXML < 0)
                {
                    _logger.LogError("No se encontró el inicio del XML válido en el CAF. Primeros 200 caracteres: {Inicio}", 
                        xmlData.Length > 200 ? xmlData.Substring(0, 200) : xmlData);
                    throw new InvalidOperationException("El CAF no contiene XML válido");
                }

                _logger.LogInformation("Inicio del XML encontrado en posición: {Posicion}", inicioXML);

                // Buscar el final del XML válido (buscar desde el final hacia atrás)
                int finXML = xmlData.LastIndexOf("</AUTORIZACION>", StringComparison.OrdinalIgnoreCase);
                if (finXML < 0)
                {
                    finXML = xmlData.LastIndexOf("</CAF>", StringComparison.OrdinalIgnoreCase);
                    if (finXML >= 0)
                    {
                        finXML += "</CAF>".Length;
                    }
                }
                else
                {
                    finXML += "</AUTORIZACION>".Length;
                }

                if (finXML < 0 || finXML <= inicioXML)
                {
                    _logger.LogError("No se encontró el final del XML válido en el CAF. FinXML: {FinXML}, InicioXML: {InicioXML}", finXML, inicioXML);
                    _logger.LogError("Últimos 200 caracteres: {Fin}", 
                        xmlData.Length > 200 ? xmlData.Substring(Math.Max(0, xmlData.Length - 200)) : xmlData);
                    throw new InvalidOperationException("El CAF no contiene XML válido completo");
                }

                _logger.LogInformation("Fin del XML encontrado en posición: {Posicion}", finXML);

                // Extraer solo la parte XML válida
                string xmlLimpio = xmlData.Substring(inicioXML, finXML - inicioXML).Trim();

                // Validar que el XML limpio sea válido
                try
                {
                    var testDoc = new XmlDocument();
                    testDoc.LoadXml(xmlLimpio);
                    _logger.LogInformation("XML del CAF limpiado y validado correctamente. Longitud original: {Original}, Longitud limpia: {Limpia}", 
                        xmlData.Length, xmlLimpio.Length);
                }
                catch (XmlException xmlEx)
                {
                    _logger.LogError(xmlEx, "El XML limpio aún no es válido. Error en línea {Line}, posición {Pos}", xmlEx.LineNumber, xmlEx.LinePosition);
                    _logger.LogError("XML limpio (primeros 500 caracteres): {XML}", 
                        xmlLimpio.Length > 500 ? xmlLimpio.Substring(0, 500) : xmlLimpio);
                    throw new InvalidOperationException($"El XML limpio no es válido: {xmlEx.Message}", xmlEx);
                }

                return xmlLimpio;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar el XML del CAF");
                throw;
            }
        }

        private CAFData ParsearCAF(string xmlData, int tipoDTE, int folioInicial, int folioFinal, 
            DateTime fechaAutorizacion, string rutEmisor, string razonSocial)
        {
            try
            {
                // CRÍTICO: Usar ObtenerCafBlindado para sanitizar y parsear correctamente el CAF
                // Esto evita errores de "multiple root elements" cuando el CAF tiene declaración XML
                _logger.LogInformation("Parseando CAF usando ObtenerCafBlindado, longitud: {Length} caracteres", xmlData.Length);
                var doc = ObtenerCafBlindado(xmlData);

                // Extraer datos del CAF
                var cafNode = doc.SelectSingleNode("//CAF");
                var daNode = cafNode?.SelectSingleNode("DA");

                // Si no se pasaron desde BD, extraer del XML
                if (string.IsNullOrEmpty(rutEmisor))
                    rutEmisor = daNode?.SelectSingleNode("RE")?.InnerText ?? "";
                if (string.IsNullOrEmpty(razonSocial))
                    razonSocial = daNode?.SelectSingleNode("RS")?.InnerText ?? "";
                if (folioInicial == 0 || folioFinal == 0)
                {
                    var rngNode = daNode?.SelectSingleNode("RNG");
                    folioInicial = int.Parse(rngNode?.SelectSingleNode("D")?.InnerText ?? "1");
                    folioFinal = int.Parse(rngNode?.SelectSingleNode("H")?.InnerText ?? "1");
                }
                
                // CRÍTICO: Extraer la fecha de autorización del XML (nodo FA)
                // La fecha del XML es la fecha real de autorización del CAF por el SII
                if (fechaAutorizacion == default || fechaAutorizacion.Year < 2020)
                {
                    var faNode = daNode?.SelectSingleNode("FA");
                    if (faNode != null && !string.IsNullOrEmpty(faNode.InnerText))
                    {
                        if (DateTime.TryParse(faNode.InnerText, out DateTime fechaFA))
                        {
                            fechaAutorizacion = fechaFA;
                            _logger.LogInformation("Fecha de autorización extraída del XML (nodo FA): {Fecha}", fechaAutorizacion);
                        }
                    }
                }

                var moduloRSA = daNode?.SelectSingleNode("RSAPK/M")?.InnerText ?? "";
                var exponenteRSA = daNode?.SelectSingleNode("RSAPK/E")?.InnerText ?? "";
                var idK = int.Parse(daNode?.SelectSingleNode("IDK")?.InnerText ?? "0");

                // Extraer clave privada
                var rsaskNode = doc.SelectSingleNode("//RSASK");
                var clavePrivadaPEM = rsaskNode?.InnerText ?? "";

                return new CAFData
                {
                    RutEmisor = rutEmisor,
                    RazonSocial = razonSocial,
                    TipoDTE = tipoDTE,
                    FolioInicial = folioInicial,
                    FolioFinal = folioFinal,
                    FechaAutorizacion = fechaAutorizacion,
                    ModuloRSA = moduloRSA,
                    ExponenteRSA = exponenteRSA,
                    IdK = idK,
                    ClavePrivadaPEM = clavePrivadaPEM,
                    XMLOriginal = xmlData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al parsear CAF desde XML");
                throw new InvalidOperationException("Error al parsear el CAF", ex);
            }
        }
    }
}
