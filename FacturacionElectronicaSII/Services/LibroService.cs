using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.Libro;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio para generación de Libros de Compra y Venta
    /// </summary>
    public class LibroService : ILibroService
    {
        private readonly ILogger<LibroService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IFirmaService _firmaService;
        private const string SII_NAMESPACE = "http://www.sii.cl/SiiDte";

        public LibroService(
            ILogger<LibroService> logger,
            IConfiguration configuration,
            IFirmaService firmaService)
        {
            _logger = logger;
            _configuration = configuration;
            _firmaService = firmaService;
        }

        public async Task<string> GenerarLibroVentasAsync(string periodoTributario)
        {
            _logger.LogInformation("Generando Libro de Ventas para período {Periodo}", periodoTributario);

            // 1. Construir libro con datos
            var libro = await ConstruirLibroVentasAsync(periodoTributario);

            // 2. Generar XML
            var xml = ConstruirXMLLibro(libro);

            // 3. Firmar XML
            var xmlFirmado = _firmaService.FirmarLibro(xml);

            // 4. Guardar archivo
            await GuardarLibroAsync(xmlFirmado, "LibroVentas", periodoTributario);

            _logger.LogInformation("Libro de Ventas generado exitosamente para período {Periodo}", periodoTributario);
            return xmlFirmado;
        }

        public async Task<string> GenerarLibroComprasAsync(string periodoTributario)
        {
            _logger.LogInformation("Generando Libro de Compras para período {Periodo}", periodoTributario);

            // 1. Construir libro con datos
            var libro = await ConstruirLibroComprasAsync(periodoTributario);

            // 2. Generar XML
            var xml = ConstruirXMLLibro(libro);

            // 3. Firmar XML
            var xmlFirmado = _firmaService.FirmarLibro(xml);

            // 4. Guardar archivo
            await GuardarLibroAsync(xmlFirmado, "LibroCompras", periodoTributario);

            _logger.LogInformation("Libro de Compras generado exitosamente para período {Periodo}", periodoTributario);
            return xmlFirmado;
        }

        public async Task<string> GenerarLibroBoletasAsync(string periodoTributario)
        {
            _logger.LogInformation("Generando Libro de Boletas para período {Periodo}", periodoTributario);

            // 1. Construir libro con datos
            var libro = await ConstruirLibroBoletasAsync(periodoTributario);

            // 2. Generar XML
            var xml = ConstruirXMLLibro(libro);

            // 3. Firmar XML
            var xmlFirmado = _firmaService.FirmarLibro(xml);

            // 4. Guardar archivo
            await GuardarLibroAsync(xmlFirmado, "LibroBoletas", periodoTributario);

            _logger.LogInformation("Libro de Boletas generado exitosamente para período {Periodo}", periodoTributario);
            return xmlFirmado;
        }

        public async Task<LibroCompraVenta> ConstruirLibroVentasAsync(string periodoTributario)
        {
            _logger.LogInformation("Construyendo Libro de Ventas para período {Periodo}", periodoTributario);

            var libro = new LibroCompraVenta();

            // Carátula
            libro.Caratula = new CaratulaLibro
            {
                RutEmisorLibro = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "78301789-K",
                RznSoc = _configuration["FacturacionElectronica:Emisor:RazonSocial"] ?? "SOFTWARE MAURICIO MORALES ENERGY SYSTEMS E.I.R.L.",
                PeriodoTributario = periodoTributario,
                FchResol = _configuration["FacturacionElectronica:Resolucion:Fecha"] ?? "2025-12-03",
                NroResol = int.Parse(_configuration["FacturacionElectronica:Resolucion:Numero"] ?? "0"),
                TipoLibro = "ESPECIAL", // Para certificación
                TipoEnvio = "TOTAL",
                TmstFirmaEnv = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            // Leer documentos emitidos del período
            var documentos = await LeerDocumentosDelPeriodoAsync(periodoTributario);

            // Construir detalle
            libro.Detalle = documentos.Select(doc => new DetalleDocumento
            {
                TpoDoc = doc.TipoDTE,
                Folio = doc.Folio,
                FchDoc = doc.FechaEmision,
                RUTDoc = doc.RutReceptor,
                RznSoc = doc.RazonSocialReceptor,
                MntNeto = doc.MontoNeto,
                MntExe = doc.MontoExento,
                MntIVA = doc.IVA,
                MntTotal = doc.MontoTotal,
                TasaIVA = doc.MontoNeto > 0 ? 19 : null
            }).ToList();

            // Construir resúmenes por tipo de documento
            var resumen = documentos
                .GroupBy(d => d.TipoDTE)
                .Select(g => new ResumenPeriodo
                {
                    TpoDoc = g.Key,
                    TotDoc = g.Count(),
                    TotMntNeto = g.Sum(d => d.MontoNeto) > 0 ? g.Sum(d => d.MontoNeto) : null,
                    TotMntExe = g.Sum(d => d.MontoExento) > 0 ? g.Sum(d => d.MontoExento) : null,
                    TotMntIVA = g.Sum(d => d.IVA) > 0 ? g.Sum(d => d.IVA) : null,
                    TotMntTotal = g.Sum(d => d.MontoTotal)
                })
                .ToList();

            libro.ResumenPeriodo = resumen;

            // Totales finales
            libro.TotalesPeriodo = new TotalesPeriodo
            {
                TotDoc = documentos.Count,
                TotMntNeto = documentos.Sum(d => d.MontoNeto) > 0 ? documentos.Sum(d => d.MontoNeto) : null,
                TotMntExe = documentos.Sum(d => d.MontoExento) > 0 ? documentos.Sum(d => d.MontoExento) : null,
                TotMntIVA = documentos.Sum(d => d.IVA) > 0 ? documentos.Sum(d => d.IVA) : null,
                TotMntTotal = documentos.Sum(d => d.MontoTotal)
            };

            return libro;
        }

        public async Task<LibroCompraVenta> ConstruirLibroComprasAsync(string periodoTributario)
        {
            _logger.LogInformation("Construyendo Libro de Compras para período {Periodo}", periodoTributario);

            var libro = new LibroCompraVenta();

            // Carátula
            libro.Caratula = new CaratulaLibro
            {
                RutEmisorLibro = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "78301789-K",
                RznSoc = _configuration["FacturacionElectronica:Emisor:RazonSocial"] ?? "SOFTWARE MAURICIO MORALES ENERGY SYSTEMS E.I.R.L.",
                PeriodoTributario = periodoTributario,
                FchResol = _configuration["FacturacionElectronica:Resolucion:Fecha"] ?? "2025-12-03",
                NroResol = int.Parse(_configuration["FacturacionElectronica:Resolucion:Numero"] ?? "0"),
                TipoLibro = "ESPECIAL", // Para certificación
                TipoEnvio = "TOTAL",
                TmstFirmaEnv = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            // Para certificación, libro de compras puede estar vacío o con datos simulados
            // Por ahora lo dejamos vacío
            libro.Detalle = new List<DetalleDocumento>();
            libro.ResumenPeriodo = new List<ResumenPeriodo>();
            libro.TotalesPeriodo = new TotalesPeriodo
            {
                TotDoc = 0,
                TotMntNeto = null,
                TotMntExe = null,
                TotMntIVA = null,
                TotMntTotal = 0
            };

            _logger.LogWarning("Libro de Compras generado vacío (sin compras en el período)");

            return libro;
        }

        public async Task<LibroCompraVenta> ConstruirLibroBoletasAsync(string periodoTributario)
        {
            _logger.LogInformation("Construyendo Libro de Boletas para período {Periodo}", periodoTributario);

            var libro = new LibroCompraVenta();

            // Carátula
            libro.Caratula = new CaratulaLibro
            {
                RutEmisorLibro = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "78301789-K",
                RznSoc = _configuration["FacturacionElectronica:Emisor:RazonSocial"] ?? "SOFTWARE MAURICIO MORALES ENERGY SYSTEMS E.I.R.L.",
                PeriodoTributario = periodoTributario,
                FchResol = _configuration["FacturacionElectronica:Resolucion:Fecha"] ?? "2025-12-03",
                NroResol = int.Parse(_configuration["FacturacionElectronica:Resolucion:Numero"] ?? "0"),
                TipoLibro = "ESPECIAL", // Para boletas
                TipoEnvio = "TOTAL",
                TmstFirmaEnv = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            var rutaXmls = Path.Combine(Directory.GetCurrentDirectory(), "Data", "XMLs");
            var archivosXML = Directory.GetFiles(rutaXmls, "EnvioDTE_*.xml");

            var listaDetalle = new List<DetalleDocumento>();
            var estadisticas = new Dictionary<int, (int cantidad, int neto, int exe, int iva, int total)>();

            // Leer cada archivo XML y extraer información solo de boletas (tipo 39)
            foreach (var archivoXML in archivosXML)
            {
                try
                {
                    var xmlContent = await File.ReadAllTextAsync(archivoXML);
                    var doc = new XmlDocument();
                    doc.LoadXml(xmlContent);

                    var nsmgr = new XmlNamespaceManager(doc.NameTable);
                    nsmgr.AddNamespace("sii", "http://www.sii.cl/SiiDte");

                    var tipoDTENode = doc.SelectSingleNode("//sii:TipoDTE", nsmgr);
                    if (tipoDTENode == null) continue;

                    var tipoDTE = int.Parse(tipoDTENode.InnerText);

                    // SOLO PROCESAR BOLETAS (39)
                    if (tipoDTE != 39) continue;

                    var fechaEmision = doc.SelectSingleNode("//sii:FchEmis", nsmgr)?.InnerText ?? "";

                    // Filtrar por período (AAAA-MM)
                    if (!fechaEmision.StartsWith(periodoTributario)) continue;

                    var folio = int.Parse(doc.SelectSingleNode("//sii:Folio", nsmgr)?.InnerText ?? "0");
                    var rutReceptor = doc.SelectSingleNode("//sii:RUTRecep", nsmgr)?.InnerText ?? "66666666-6";
                    var razonSocial = doc.SelectSingleNode("//sii:RznSocRecep", nsmgr)?.InnerText ?? "CLIENTE FINAL";

                    var mntNeto = int.Parse(doc.SelectSingleNode("//sii:MntNeto", nsmgr)?.InnerText ?? "0");
                    var mntExe = int.Parse(doc.SelectSingleNode("//sii:MntExe", nsmgr)?.InnerText ?? "0");
                    var mntIva = int.Parse(doc.SelectSingleNode("//sii:IVA", nsmgr)?.InnerText ?? "0");
                    var mntTotal = int.Parse(doc.SelectSingleNode("//sii:MntTotal", nsmgr)?.InnerText ?? "0");

                    // Agregar detalle
                    listaDetalle.Add(new DetalleDocumento
                    {
                        TpoDoc = tipoDTE,
                        Folio = folio,
                        FchDoc = fechaEmision,
                        RUTDoc = rutReceptor,
                        RznSoc = razonSocial,
                        MntNeto = mntNeto > 0 ? mntNeto : null,
                        MntExe = mntExe > 0 ? mntExe : null,
                        MntIVA = mntIva > 0 ? mntIva : null,
                        MntTotal = mntTotal,
                        TasaIVA = mntIva > 0 ? 19 : (decimal?)null
                    });

                    // Acumular estadísticas
                    if (!estadisticas.ContainsKey(tipoDTE))
                    {
                        estadisticas[tipoDTE] = (0, 0, 0, 0, 0);
                    }

                    var stats = estadisticas[tipoDTE];
                    estadisticas[tipoDTE] = (
                        stats.cantidad + 1,
                        stats.neto + mntNeto,
                        stats.exe + mntExe,
                        stats.iva + mntIva,
                        stats.total + mntTotal
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al procesar XML para Libro de Boletas: {Archivo}", Path.GetFileName(archivoXML));
                }
            }

            // Construir resumen por tipo de documento
            libro.ResumenPeriodo = estadisticas.Select(kvp => new ResumenPeriodo
            {
                TpoDoc = kvp.Key,
                TotDoc = kvp.Value.cantidad,
                TotMntNeto = kvp.Value.neto > 0 ? kvp.Value.neto : null,
                TotMntExe = kvp.Value.exe > 0 ? kvp.Value.exe : null,
                TotMntIVA = kvp.Value.iva > 0 ? kvp.Value.iva : null,
                TotMntTotal = kvp.Value.total
            }).ToList();

            libro.Detalle = listaDetalle;

            // Totales del período
            libro.TotalesPeriodo = new TotalesPeriodo
            {
                TotDoc = listaDetalle.Count,
                TotMntNeto = estadisticas.Values.Sum(v => v.neto) > 0 ? estadisticas.Values.Sum(v => v.neto) : null,
                TotMntExe = estadisticas.Values.Sum(v => v.exe) > 0 ? estadisticas.Values.Sum(v => v.exe) : null,
                TotMntIVA = estadisticas.Values.Sum(v => v.iva) > 0 ? estadisticas.Values.Sum(v => v.iva) : null,
                TotMntTotal = estadisticas.Values.Sum(v => v.total)
            };

            _logger.LogInformation("Libro de Boletas construido con {Cantidad} documentos", listaDetalle.Count);

            return libro;
        }

        public string ConstruirXMLLibro(LibroCompraVenta libro)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;

            // Declaración XML
            var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "ISO-8859-1", null);
            doc.AppendChild(xmlDeclaration);

            // Elemento raíz LibroCompraVenta
            var libroElement = doc.CreateElement("LibroCompraVenta");
            libroElement.SetAttribute("xmlns", SII_NAMESPACE);
            libroElement.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            libroElement.SetAttribute("xsi:schemaLocation", $"{SII_NAMESPACE} LibroCVS_v10.xsd");
            libroElement.SetAttribute("version", "1.0");
            doc.AppendChild(libroElement);

            // EnvioLibro con ID para firma
            var envioLibro = doc.CreateElement("EnvioLibro");
            envioLibro.SetAttribute("ID", "SetDoc");
            libroElement.AppendChild(envioLibro);

            // Caratula
            var caratula = doc.CreateElement("Caratula");
            caratula.AppendChild(CrearElemento(doc, "RutEmisorLibro", libro.Caratula.RutEmisorLibro));
            caratula.AppendChild(CrearElemento(doc, "RutEnvia", libro.Caratula.RutEmisorLibro)); // Mismo RUT
            caratula.AppendChild(CrearElemento(doc, "PeriodoTributario", libro.Caratula.PeriodoTributario));
            caratula.AppendChild(CrearElemento(doc, "FchResol", libro.Caratula.FchResol));
            caratula.AppendChild(CrearElemento(doc, "NroResol", libro.Caratula.NroResol.ToString()));
            caratula.AppendChild(CrearElemento(doc, "TipoLibro", libro.Caratula.TipoLibro));
            caratula.AppendChild(CrearElemento(doc, "TipoEnvio", libro.Caratula.TipoEnvio));
            caratula.AppendChild(CrearElemento(doc, "FolioNotificacion", "1")); // Folio de notificación
            caratula.AppendChild(CrearElemento(doc, "TmstFirmaEnv", libro.Caratula.TmstFirmaEnv));
            envioLibro.AppendChild(caratula);

            // ResumenPeriodo (solo si hay documentos)
            if (libro.ResumenPeriodo.Any())
            {
                foreach (var resumen in libro.ResumenPeriodo)
                {
                    var resumenElement = doc.CreateElement("ResumenPeriodo");
                    resumenElement.AppendChild(CrearElemento(doc, "TpoDoc", resumen.TpoDoc.ToString()));
                    resumenElement.AppendChild(CrearElemento(doc, "TotDoc", resumen.TotDoc.ToString()));
                    
                    if (resumen.TotMntNeto.HasValue && resumen.TotMntNeto.Value > 0)
                        resumenElement.AppendChild(CrearElemento(doc, "TotMntNeto", resumen.TotMntNeto.Value.ToString()));
                    
                    if (resumen.TotMntExe.HasValue && resumen.TotMntExe.Value > 0)
                        resumenElement.AppendChild(CrearElemento(doc, "TotMntExe", resumen.TotMntExe.Value.ToString()));
                    
                    if (resumen.TotMntIVA.HasValue && resumen.TotMntIVA.Value > 0)
                        resumenElement.AppendChild(CrearElemento(doc, "TotMntIVA", resumen.TotMntIVA.Value.ToString()));
                    
                    resumenElement.AppendChild(CrearElemento(doc, "TotMntTotal", resumen.TotMntTotal.ToString()));
                    
                    envioLibro.AppendChild(resumenElement);
                }
            }

            // Detalle (solo si hay documentos)
            if (libro.Detalle.Any())
            {
                foreach (var detalle in libro.Detalle)
                {
                    var detalleElement = doc.CreateElement("Detalle");
                    detalleElement.AppendChild(CrearElemento(doc, "TpoDoc", detalle.TpoDoc.ToString()));
                    detalleElement.AppendChild(CrearElemento(doc, "Folio", detalle.Folio.ToString()));
                    detalleElement.AppendChild(CrearElemento(doc, "FchDoc", detalle.FchDoc));
                    detalleElement.AppendChild(CrearElemento(doc, "RUTDoc", detalle.RUTDoc));
                    detalleElement.AppendChild(CrearElemento(doc, "RznSoc", detalle.RznSoc));
                    
                    if (detalle.MntNeto.HasValue && detalle.MntNeto.Value > 0)
                        detalleElement.AppendChild(CrearElemento(doc, "MntNeto", detalle.MntNeto.Value.ToString()));
                    
                    if (detalle.MntExe.HasValue && detalle.MntExe.Value > 0)
                        detalleElement.AppendChild(CrearElemento(doc, "MntExe", detalle.MntExe.Value.ToString()));
                    
                    if (detalle.MntIVA.HasValue && detalle.MntIVA.Value > 0)
                    {
                        detalleElement.AppendChild(CrearElemento(doc, "TasaIVA", detalle.TasaIVA.HasValue ? detalle.TasaIVA.Value.ToString("0.##") : "19"));
                        detalleElement.AppendChild(CrearElemento(doc, "MntIVA", detalle.MntIVA.Value.ToString()));
                    }
                    
                    detalleElement.AppendChild(CrearElemento(doc, "MntTotal", detalle.MntTotal.ToString()));
                    
                    envioLibro.AppendChild(detalleElement);
                }
            }

            // TotalesPeriodo
            var totalesElement = doc.CreateElement("TotalesPeriodo");
            totalesElement.AppendChild(CrearElemento(doc, "TotDoc", libro.TotalesPeriodo.TotDoc.ToString()));
            
            if (libro.TotalesPeriodo.TotMntNeto.HasValue && libro.TotalesPeriodo.TotMntNeto.Value > 0)
                totalesElement.AppendChild(CrearElemento(doc, "TotMntNeto", libro.TotalesPeriodo.TotMntNeto.Value.ToString()));
            
            if (libro.TotalesPeriodo.TotMntExe.HasValue && libro.TotalesPeriodo.TotMntExe.Value > 0)
                totalesElement.AppendChild(CrearElemento(doc, "TotMntExe", libro.TotalesPeriodo.TotMntExe.Value.ToString()));
            
            if (libro.TotalesPeriodo.TotMntIVA.HasValue && libro.TotalesPeriodo.TotMntIVA.Value > 0)
                totalesElement.AppendChild(CrearElemento(doc, "TotMntIVA", libro.TotalesPeriodo.TotMntIVA.Value.ToString()));
            
            totalesElement.AppendChild(CrearElemento(doc, "TotMntTotal", libro.TotalesPeriodo.TotMntTotal.ToString()));
            
            envioLibro.AppendChild(totalesElement);

            // Convertir a string con encoding correcto
            using (var stringWriter = new StringWriter())
            using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
            {
                Encoding = Encoding.GetEncoding("ISO-8859-1"),
                Indent = true,
                OmitXmlDeclaration = false
            }))
            {
                doc.Save(xmlWriter);
                return stringWriter.ToString();
            }
        }

        private XmlElement CrearElemento(XmlDocument doc, string nombre, string valor)
        {
            var elemento = doc.CreateElement(nombre);
            elemento.InnerText = valor;
            return elemento;
        }

        private async Task<List<DocumentoEmitido>> LeerDocumentosDelPeriodoAsync(string periodoTributario)
        {
            var documentos = new List<DocumentoEmitido>();
            var rutaXmls = _configuration["FacturacionElectronica:Rutas:XMLs"] ?? "./Data/XMLs";
            var directorioXmls = Path.GetFullPath(rutaXmls);

            if (!Directory.Exists(directorioXmls))
            {
                _logger.LogWarning("No existe el directorio de XMLs: {Directorio}", directorioXmls);
                return documentos;
            }

            var archivosXml = Directory.GetFiles(directorioXmls, "EnvioDTE_*.xml");

            foreach (var archivo in archivosXml)
            {
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(archivo);

                    var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                    nsmgr.AddNamespace("sii", SII_NAMESPACE);

                    // Leer datos del documento
                    var tipoDTENode = xmlDoc.SelectSingleNode("//sii:TipoDTE", nsmgr);
                    var folioNode = xmlDoc.SelectSingleNode("//sii:Folio", nsmgr);
                    var fechaNode = xmlDoc.SelectSingleNode("//sii:FchEmis", nsmgr);
                    var rutReceptorNode = xmlDoc.SelectSingleNode("//sii:RUTRecep", nsmgr);
                    var razonSocialNode = xmlDoc.SelectSingleNode("//sii:RznSocRecep", nsmgr);
                    var netoNode = xmlDoc.SelectSingleNode("//sii:MntNeto", nsmgr);
                    var exentoNode = xmlDoc.SelectSingleNode("//sii:MntExe", nsmgr);
                    var ivaNode = xmlDoc.SelectSingleNode("//sii:IVA", nsmgr);
                    var totalNode = xmlDoc.SelectSingleNode("//sii:MntTotal", nsmgr);

                    if (tipoDTENode != null && folioNode != null && fechaNode != null)
                    {
                        var fechaDoc = fechaNode.InnerText;
                        
                        // Filtrar por período (formato AAAA-MM)
                        if (fechaDoc.StartsWith(periodoTributario))
                        {
                            documentos.Add(new DocumentoEmitido
                            {
                                TipoDTE = int.Parse(tipoDTENode.InnerText),
                                Folio = int.Parse(folioNode.InnerText),
                                FechaEmision = fechaDoc,
                                RutReceptor = rutReceptorNode?.InnerText ?? "66666666-6",
                                RazonSocialReceptor = razonSocialNode?.InnerText ?? "Cliente",
                                MontoNeto = netoNode != null ? int.Parse(netoNode.InnerText) : 0,
                                MontoExento = exentoNode != null ? int.Parse(exentoNode.InnerText) : 0,
                                IVA = ivaNode != null ? int.Parse(ivaNode.InnerText) : 0,
                                MontoTotal = totalNode != null ? int.Parse(totalNode.InnerText) : 0
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al leer XML: {Archivo}", archivo);
                }
            }

            _logger.LogInformation("Documentos encontrados para período {Periodo}: {Cantidad}", periodoTributario, documentos.Count);
            return documentos;
        }

        private async Task GuardarLibroAsync(string xmlLibro, string tipoLibro, string periodoTributario)
        {
            var rutaXmls = _configuration["FacturacionElectronica:Rutas:XMLs"] ?? "./Data/XMLs";
            var directorioXmls = Path.GetFullPath(rutaXmls);
            Directory.CreateDirectory(directorioXmls);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var nombreArchivo = $"{tipoLibro}_{periodoTributario.Replace("-", "")}_{timestamp}.xml";
            var rutaArchivo = Path.Combine(directorioXmls, nombreArchivo);

            await File.WriteAllTextAsync(rutaArchivo, xmlLibro, new UTF8Encoding(false));
            _logger.LogInformation("Libro guardado en: {Ruta}", rutaArchivo);
        }

        // Clase auxiliar para documentos emitidos
        private class DocumentoEmitido
        {
            public int TipoDTE { get; set; }
            public int Folio { get; set; }
            public string FechaEmision { get; set; } = string.Empty;
            public string RutReceptor { get; set; } = string.Empty;
            public string RazonSocialReceptor { get; set; } = string.Empty;
            public int MontoNeto { get; set; }
            public int MontoExento { get; set; }
            public int IVA { get; set; }
            public int MontoTotal { get; set; }
        }
    }
}
