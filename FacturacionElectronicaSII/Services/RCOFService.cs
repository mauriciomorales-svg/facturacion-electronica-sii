using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.RCOF;
using System.Text;
using System.Xml;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio para generación de RCOF (Reporte de Consumo de Folios)
    /// </summary>
    public class RCOFService : IRCOFService
    {
        private readonly ILogger<RCOFService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IFirmaService _firmaService;

        public RCOFService(
            ILogger<RCOFService> logger,
            IConfiguration configuration,
            IFirmaService firmaService)
        {
            _logger = logger;
            _configuration = configuration;
            _firmaService = firmaService;
        }

        public async Task<string> GenerarRCOFAsync(string fechaInicio, string fechaFinal)
        {
            try
            {
                _logger.LogInformation("Generando RCOF para período {Inicio} - {Final}", fechaInicio, fechaFinal);

                // Construir RCOF
                var rcof = await ConstruirRCOFAsync(fechaInicio, fechaFinal);

                // Construir XML
                var xmlRCOF = ConstruirXMLRCOF(rcof);

                // Firmar XML
                _logger.LogInformation("Firmando RCOF");
                var xmlFirmado = _firmaService.FirmarRCOF(xmlRCOF);

                // Guardar XML firmado
                var rutaXmls = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    _configuration["FacturacionElectronica:Rutas:XMLs"] ?? "./Data/XMLs"
                );

                Directory.CreateDirectory(rutaXmls);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var nombreArchivo = $"RCOF_{fechaInicio.Replace("-", "")}_{fechaFinal.Replace("-", "")}_{timestamp}.xml";
                var rutaCompleta = Path.Combine(rutaXmls, nombreArchivo);

                await File.WriteAllTextAsync(rutaCompleta, xmlFirmado, new UTF8Encoding(false));

                _logger.LogInformation("RCOF generado: {Ruta}", rutaCompleta);

                return rutaCompleta;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar RCOF");
                throw;
            }
        }

        public async Task<ConsumoFolios> ConstruirRCOFAsync(string fechaInicio, string fechaFinal)
        {
            try
            {
                _logger.LogInformation("Construyendo RCOF desde DTEs");

                var rutaXmls = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    _configuration["FacturacionElectronica:Rutas:XMLs"] ?? "./Data/XMLs"
                );

                // Leer todos los XMLs del período
                var archivosXML = Directory.GetFiles(rutaXmls, "EnvioDTE_*.xml");

                var rcof = new ConsumoFolios
                {
                    Caratula = new CaratulaRCOF
                    {
                        RutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "",
                        RutEnvia = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "",
                        FchResol = _configuration["FacturacionElectronica:Resolucion:Fecha"] ?? "",
                        NroResol = _configuration["FacturacionElectronica:Resolucion:Numero"] ?? "0",
                        FchInicio = fechaInicio,
                        FchFinal = fechaFinal,
                        SecEnvio = 1,
                        TmstFirmaEnv = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    }
                };

                // Contadores por tipo de documento
                var estadisticas = new Dictionary<int, EstadisticasDTE>();

                foreach (var archivoXML in archivosXML)
                {
                    try
                    {
                        var xmlContent = await File.ReadAllTextAsync(archivoXML);
                        var doc = new XmlDocument();
                        doc.LoadXml(xmlContent);

                        var nsmgr = new XmlNamespaceManager(doc.NameTable);
                        nsmgr.AddNamespace("sii", "http://www.sii.cl/SiiDte");

                        // Extraer datos del DTE
                        var tipoDTENode = doc.SelectSingleNode("//sii:TipoDTE", nsmgr);
                        var fechaNode = doc.SelectSingleNode("//sii:FchEmis", nsmgr);
                        var folioNode = doc.SelectSingleNode("//sii:Folio", nsmgr);

                        if (tipoDTENode == null || fechaNode == null || folioNode == null)
                            continue;

                        var tipoDTE = int.Parse(tipoDTENode.InnerText);
                        var fechaEmision = fechaNode.InnerText;
                        var folio = int.Parse(folioNode.InnerText);

                        // Solo procesar boletas (tipo 39)
                        if (tipoDTE != 39)
                            continue;

                        // Verificar si está en el período
                        if (string.Compare(fechaEmision, fechaInicio) < 0 || 
                            string.Compare(fechaEmision, fechaFinal) > 0)
                            continue;

                        // Extraer montos
                        var mntNeto = int.Parse(doc.SelectSingleNode("//sii:MntNeto", nsmgr)?.InnerText ?? "0");
                        var mntExe = int.Parse(doc.SelectSingleNode("//sii:MntExe", nsmgr)?.InnerText ?? "0");
                        var mntIva = int.Parse(doc.SelectSingleNode("//sii:IVA", nsmgr)?.InnerText ?? "0");
                        var mntTotal = int.Parse(doc.SelectSingleNode("//sii:MntTotal", nsmgr)?.InnerText ?? "0");

                        // Acumular estadísticas
                        if (!estadisticas.ContainsKey(tipoDTE))
                        {
                            estadisticas[tipoDTE] = new EstadisticasDTE
                            {
                                TipoDTE = tipoDTE
                            };
                        }

                        var stats = estadisticas[tipoDTE];
                        stats.MntNeto += mntNeto;
                        stats.MntExe += mntExe;
                        stats.MntIva += mntIva;
                        stats.MntTotal += mntTotal;
                        stats.FoliosUtilizados.Add(folio);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al procesar XML: {Archivo}", Path.GetFileName(archivoXML));
                    }
                }

                // Construir resúmenes
                foreach (var (tipoDTE, stats) in estadisticas)
                {
                    var foliosOrdenados = stats.FoliosUtilizados.OrderBy(f => f).ToList();
                    var rangos = ObtenerRangos(foliosOrdenados);

                    rcof.Resumen.Add(new ResumenRCOF
                    {
                        TipoDocumento = tipoDTE,
                        MntNeto = stats.MntNeto,
                        MntIva = stats.MntIva,
                        TasaIVA = 19,
                        MntExento = stats.MntExe,
                        MntTotal = stats.MntTotal,
                        FoliosEmitidos = stats.FoliosUtilizados.Count,
                        FoliosAnulados = 0,
                        FoliosUtilizados = stats.FoliosUtilizados.Count,
                        RangoUtilizados = rangos
                    });
                }

                return rcof;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al construir RCOF");
                throw;
            }
        }

        public string ConstruirXMLRCOF(ConsumoFolios rcof)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
                sb.AppendLine("<ConsumoFolios xmlns=\"http://www.sii.cl/SiiDte\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.sii.cl/SiiDte ConsumoFolio_v10.xsd\" version=\"1.0\">");
                sb.AppendLine("<DocumentoConsumoFolios ID=\"CF\">");

                // Carátula
                sb.AppendLine("<Caratula>");
                sb.AppendLine($"<RutEmisor>{rcof.Caratula.RutEmisor}</RutEmisor>");
                sb.AppendLine($"<RutEnvia>{rcof.Caratula.RutEnvia}</RutEnvia>");
                sb.AppendLine($"<FchResol>{rcof.Caratula.FchResol}</FchResol>");
                sb.AppendLine($"<NroResol>{rcof.Caratula.NroResol}</NroResol>");
                sb.AppendLine($"<FchInicio>{rcof.Caratula.FchInicio}</FchInicio>");
                sb.AppendLine($"<FchFinal>{rcof.Caratula.FchFinal}</FchFinal>");
                sb.AppendLine($"<SecEnvio>{rcof.Caratula.SecEnvio}</SecEnvio>");
                sb.AppendLine($"<TmstFirmaEnv>{rcof.Caratula.TmstFirmaEnv}</TmstFirmaEnv>");
                sb.AppendLine("</Caratula>");

                // Resúmenes
                foreach (var resumen in rcof.Resumen)
                {
                    sb.AppendLine("<Resumen>");
                    sb.AppendLine($"<TipoDocumento>{resumen.TipoDocumento}</TipoDocumento>");
                    sb.AppendLine($"<MntNeto>{resumen.MntNeto}</MntNeto>");
                    sb.AppendLine($"<MntIva>{resumen.MntIva}</MntIva>");
                    sb.AppendLine($"<TasaIVA>{resumen.TasaIVA:F2}</TasaIVA>");
                    sb.AppendLine($"<MntExento>{resumen.MntExento}</MntExento>");
                    sb.AppendLine($"<MntTotal>{resumen.MntTotal}</MntTotal>");
                    sb.AppendLine($"<FoliosEmitidos>{resumen.FoliosEmitidos}</FoliosEmitidos>");
                    sb.AppendLine($"<FoliosAnulados>{resumen.FoliosAnulados}</FoliosAnulados>");
                    sb.AppendLine($"<FoliosUtilizados>{resumen.FoliosUtilizados}</FoliosUtilizados>");

                    foreach (var rango in resumen.RangoUtilizados)
                    {
                        sb.AppendLine("<RangoUtilizados>");
                        sb.AppendLine($"<Inicial>{rango.Inicial}</Inicial>");
                        sb.AppendLine($"<Final>{rango.Final}</Final>");
                        sb.AppendLine("</RangoUtilizados>");
                    }

                    sb.AppendLine("</Resumen>");
                }

                sb.AppendLine("</DocumentoConsumoFolios>");
                sb.AppendLine("</ConsumoFolios>");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al construir XML RCOF");
                throw;
            }
        }

        private List<RangoFoliosRCOF> ObtenerRangos(List<int> folios)
        {
            var rangos = new List<RangoFoliosRCOF>();

            if (folios.Count == 0)
                return rangos;

            int inicio = folios[0];
            int fin = folios[0];

            for (int i = 1; i < folios.Count; i++)
            {
                if (folios[i] == fin + 1)
                {
                    fin = folios[i];
                }
                else
                {
                    rangos.Add(new RangoFoliosRCOF { Inicial = inicio, Final = fin });
                    inicio = folios[i];
                    fin = folios[i];
                }
            }

            rangos.Add(new RangoFoliosRCOF { Inicial = inicio, Final = fin });

            return rangos;
        }

        private class EstadisticasDTE
        {
            public int TipoDTE { get; set; }
            public int MntNeto { get; set; }
            public int MntExe { get; set; }
            public int MntIva { get; set; }
            public int MntTotal { get; set; }
            public List<int> FoliosUtilizados { get; set; } = new List<int>();
        }
    }
}
