using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.DTE;
using System.Text;
using System.Xml.Linq;
using System.Xml;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio para construcción de XMLs según formato SII
    /// </summary>
    public class XMLBuilderService : IXMLBuilderService
    {
        private readonly ILogger<XMLBuilderService> _logger;
        private const string SII_NAMESPACE = "http://www.sii.cl/SiiDte";

        public XMLBuilderService(ILogger<XMLBuilderService> logger)
        {
            _logger = logger;
        }

        public string ConstruirXMLDTE(DocumentoTributario documento, string ted)
        {
            _logger.LogInformation("Construyendo XML DTE para tipo {TipoDTE}, folio {Folio}", documento.TipoDTE, documento.Folio);

            try
            {
                // Usar XmlDocument como en el código funcional
                var doc = new XmlDocument();
                var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "ISO-8859-1", null);
                doc.AppendChild(xmlDeclaration);

                // Crear elemento DTE sin namespace inicialmente (se agregará después)
                var dte = doc.CreateElement("DTE");
                dte.SetAttribute("version", "1.0");
                doc.AppendChild(dte);

                // CRÍTICO: El ID debe ser "F{folio}T{tipoDTE}" como en código funcional
                var documentoId = $"F{documento.Folio}T{documento.TipoDTE}";
                var documentoElement = doc.CreateElement("Documento");
                documentoElement.SetAttribute("ID", documentoId);
                dte.AppendChild(documentoElement);

                // Construir elementos usando XmlDocument
                var encabezado = ConstruirEncabezadoXml(doc, documento);
                documentoElement.AppendChild(encabezado);

                var detalles = ConstruirDetallesXml(doc, documento);
                foreach (var detalle in detalles)
                {
                    documentoElement.AppendChild(detalle);
                }

                // Descuentos/Recargos globales (después de detalles)
                if (documento.DescuentosGlobales != null && documento.DescuentosGlobales.Any())
                {
                    var dscRcgGlobal = ConstruirDescuentosGlobalesXml(doc, documento);
                    documentoElement.AppendChild(dscRcgGlobal);
                }

                // Nota: Totales ya está dentro de Encabezado (según esquema SII)

                // Referencias van directamente bajo Documento (sin wrapper <Referencias>)
                if (documento.Referencias != null && documento.Referencias.Any())
                {
                    foreach (var refElement in ConstruirReferenciasXml(doc, documento))
                    {
                        documentoElement.AppendChild(refElement);
                    }
                }

                // Insertar TED como fragmento (como en código funcional)
                // Validar que el TED sea XML válido antes de insertarlo
                try
                {
                    // Verificar que el TED sea XML válido parseándolo primero
                    var tedDoc = new XmlDocument();
                    tedDoc.LoadXml(ted);
                    _logger.LogInformation("TED validado correctamente antes de insertar");
                    
                    var tedFragment = doc.CreateDocumentFragment();
                    tedFragment.InnerXml = ted;
                    documentoElement.AppendChild(tedFragment);
                    _logger.LogInformation("TED insertado correctamente en el documento");

                    // TmstFirma requerido por schema SII (después del TED)
                    var tmstFirma = doc.CreateElement("TmstFirma");
                    tmstFirma.InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    documentoElement.AppendChild(tmstFirma);
                }
                catch (XmlException xmlEx)
                {
                    _logger.LogError(xmlEx, "Error al parsear TED. Línea {Line}, Posición {Pos}. Primeros 1000 caracteres: {TED}", 
                        xmlEx.LineNumber, xmlEx.LinePosition,
                        ted.Length > 1000 ? ted.Substring(0, 1000) : ted);
                    throw new InvalidOperationException($"El TED generado no es XML válido en línea {xmlEx.LineNumber}, posición {xmlEx.LinePosition}: {xmlEx.Message}", xmlEx);
                }

                // Serializar con formato
                var sb = new StringBuilder();
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = "\r\n",
                    Encoding = Encoding.GetEncoding("ISO-8859-1"),
                    OmitXmlDeclaration = false
                };

                using (var writer = XmlWriter.Create(sb, settings))
                {
                    doc.Save(writer);
                }

                var xmlString = sb.ToString();

                // Agregar xmlns al elemento DTE después de serializar
                var dteStart = xmlString.IndexOf("<DTE");
                if (dteStart >= 0)
                {
                    var dteEnd = xmlString.IndexOf(">", dteStart);
                    if (dteEnd >= 0)
                    {
                        var dteTag = xmlString.Substring(dteStart, dteEnd - dteStart);
                        if (!dteTag.Contains("xmlns"))
                        {
                            var versionIndex = dteTag.IndexOf("version");
                            string dteTagConXmlns;
                            if (versionIndex >= 0)
                            {
                                dteTagConXmlns = dteTag.Insert(versionIndex, "xmlns=\"http://www.sii.cl/SiiDte\" ");
                            }
                            else
                            {
                                dteTagConXmlns = dteTag + " xmlns=\"http://www.sii.cl/SiiDte\"";
                            }
                            xmlString = xmlString.Substring(0, dteStart) + dteTagConXmlns + xmlString.Substring(dteEnd);
                        }
                    }
                }

                _logger.LogInformation("XML DTE construido exitosamente");
                return xmlString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al construir XML DTE");
                throw;
            }
        }

        public string ConstruirXMLEnvioDTE(string xmlDTE, string rutEmisor, string rutEnvia, string rutReceptor, string fechaResol = "2021-03-12", string nroResol = "0", int cantidadDTE = 1)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [GENERACION_XML] INICIO: Construyendo estructura del sobre <EnvioDTE> con formato...", timestamp);
            _logger.LogInformation("Construyendo XML EnvioDTE para emisor {RutEmisor} con formato (FchResol={FchResol}, NroResol={NroResol})", rutEmisor, fechaResol, nroResol);

            // Construir el string del EnvioDTE manualmente con saltos de línea (misma lógica que EnvioSII.GenerarEnvioCompleto)
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
            // Namespace clásico SiiDte
            sb.AppendLine("<EnvioDTE xmlns=\"http://www.sii.cl/SiiDte\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.sii.cl/SiiDte EnvioDTE_v10.xsd\" version=\"1.0\">");
            
            // SetDTE con saltos
            sb.AppendLine("  <SetDTE ID=\"SetDoc\">");
            sb.AppendLine("    <Caratula version=\"1.0\">");
            sb.AppendLine($"      <RutEmisor>{rutEmisor}</RutEmisor>");
            sb.AppendLine($"      <RutEnvia>{rutEnvia}</RutEnvia>");
            sb.AppendLine($"      <RutReceptor>{rutReceptor}</RutReceptor>");
            sb.AppendLine($"      <FchResol>{fechaResol}</FchResol>");
            sb.AppendLine($"      <NroResol>{nroResol}</NroResol>");
            
            // CORRECCIÓN DE DESFASE TEMPORAL: Fecha Timestamp usando zona horaria de Chile
            DateTime fechaChile;
            try
            {
                fechaChile = TimeZoneInfo.ConvertTime(
                    DateTime.Now, 
                    TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")
                );
                _logger.LogInformation("[{Timestamp}] [GENERACION_XML] Carátula creada con fecha Chile: {FechaChile}", timestamp, fechaChile.ToString("yyyy-MM-ddTHH:mm:ss"));
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback: usar offset manual si la zona horaria no está disponible
                fechaChile = DateTime.UtcNow.AddHours(-3);
            }
            string timestampFirmaEnv = fechaChile.ToString("yyyy-MM-ddTHH:mm:ss");
            sb.AppendLine($"      <TmstFirmaEnv>{timestampFirmaEnv}</TmstFirmaEnv>");
            _logger.LogInformation("Carátula creada con fecha Chile: {Timestamp}", timestampFirmaEnv);
            
            // SubTotDTE
            sb.AppendLine("      <SubTotDTE>");
            sb.AppendLine("        <TpoDTE>33</TpoDTE>");
            sb.AppendLine($"        <NroDTE>{cantidadDTE}</NroDTE>");
            sb.AppendLine("      </SubTotDTE>");
            sb.AppendLine("    </Caratula>");
            
            // Inyectar el DTE ya firmado (que ya trae sus propios saltos de línea)
            // Remover declaración XML si existe (ya está en el sobre)
            string dteSinDeclaracion = xmlDTE;
            if (dteSinDeclaracion.TrimStart().StartsWith("<?xml"))
            {
                int finDeclaracion = dteSinDeclaracion.IndexOf("?>") + 2;
                dteSinDeclaracion = dteSinDeclaracion.Substring(finDeclaracion).TrimStart();
            }
            
            // Indentar cada línea del DTE con 4 espacios (dentro de SetDTE)
            string[] dteLines = dteSinDeclaracion.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in dteLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine("    " + line.TrimStart()); // Indentar el DTE
                }
            }
            
            sb.AppendLine("  </SetDTE>");
            sb.AppendLine("</EnvioDTE>");

            string xmlFormateado = sb.ToString();
            _logger.LogInformation("[{Timestamp}] [GENERACION_XML] [GUARDADO] EnvioDTE.xml guardado ({Length} bytes)", timestamp, Encoding.GetEncoding("ISO-8859-1").GetByteCount(xmlFormateado));
            _logger.LogInformation("[{Timestamp}] [GENERACION_XML] FIN: Estructura <EnvioDTE> creada exitosamente con formato. Retornando XML.", timestamp);
            return xmlFormateado;
        }

        public string ConstruirXMLEnvioDTEMultiple(List<(string xmlDTE, int tipoDTE)> documentos, string rutEmisor, string rutEnvia, string rutReceptor, string fechaResol, string nroResol)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
            sb.AppendLine("<EnvioDTE xmlns=\"http://www.sii.cl/SiiDte\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.sii.cl/SiiDte EnvioDTE_v10.xsd\" version=\"1.0\">");
            sb.AppendLine("  <SetDTE ID=\"SetDoc\">");
            sb.AppendLine("    <Caratula version=\"1.0\">");
            sb.AppendLine($"      <RutEmisor>{rutEmisor}</RutEmisor>");
            sb.AppendLine($"      <RutEnvia>{rutEnvia}</RutEnvia>");
            sb.AppendLine($"      <RutReceptor>{rutReceptor}</RutReceptor>");
            sb.AppendLine($"      <FchResol>{fechaResol}</FchResol>");
            sb.AppendLine($"      <NroResol>{nroResol}</NroResol>");

            DateTime fechaChile;
            try { fechaChile = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time")); }
            catch { fechaChile = DateTime.UtcNow.AddHours(-3); }
            sb.AppendLine($"      <TmstFirmaEnv>{fechaChile:yyyy-MM-ddTHH:mm:ss}</TmstFirmaEnv>");

            // SubTotDTE por tipo de documento
            var porTipo = documentos.GroupBy(d => d.tipoDTE).OrderBy(g => g.Key);
            foreach (var grupo in porTipo)
            {
                sb.AppendLine("      <SubTotDTE>");
                sb.AppendLine($"        <TpoDTE>{grupo.Key}</TpoDTE>");
                sb.AppendLine($"        <NroDTE>{grupo.Count()}</NroDTE>");
                sb.AppendLine("      </SubTotDTE>");
            }
            sb.AppendLine("    </Caratula>");

            // Insertar todos los DTEs firmados
            foreach (var (xmlDTE, _) in documentos)
            {
                string dteSinDeclaracion = xmlDTE;
                if (dteSinDeclaracion.TrimStart().StartsWith("<?xml"))
                {
                    int fin = dteSinDeclaracion.IndexOf("?>") + 2;
                    dteSinDeclaracion = dteSinDeclaracion.Substring(fin).TrimStart();
                }
                foreach (var line in dteSinDeclaracion.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine("    " + line.TrimStart());
                }
            }

            sb.AppendLine("  </SetDTE>");
            sb.AppendLine("</EnvioDTE>");
            return sb.ToString();
        }

        private XElement ConstruirEncabezado(DocumentoTributario documento)
        {
            return new XElement(XName.Get("Encabezado", SII_NAMESPACE),
                new XElement(XName.Get("IdDoc", SII_NAMESPACE),
                    new XElement(XName.Get("TipoDTE", SII_NAMESPACE), documento.TipoDTE),
                    new XElement(XName.Get("Folio", SII_NAMESPACE), documento.Folio),
                    new XElement(XName.Get("FchEmis", SII_NAMESPACE), documento.FechaEmision.ToString("yyyy-MM-dd")),
                    documento.Encabezado.FormaPago.HasValue ? new XElement(XName.Get("FmaPago", SII_NAMESPACE), documento.Encabezado.FormaPago.Value) : null
                ),
                ConstruirEmisor(documento.Encabezado.Emisor),
                ConstruirReceptor(documento.Encabezado.Receptor)
            );
        }

        private XElement ConstruirEmisor(Emisor emisor)
        {
            return new XElement(XName.Get("Emisor", SII_NAMESPACE),
                new XElement(XName.Get("RUTEmisor", SII_NAMESPACE), emisor.RUTEmisor),
                new XElement(XName.Get("RznSoc", SII_NAMESPACE), emisor.RznSoc),
                new XElement(XName.Get("GiroEmis", SII_NAMESPACE), emisor.GiroEmis),
                new XElement(XName.Get("Acteco", SII_NAMESPACE), emisor.Acteco),
                new XElement(XName.Get("DirOrigen", SII_NAMESPACE), emisor.DirOrigen),
                new XElement(XName.Get("CmnaOrigen", SII_NAMESPACE), emisor.CmnaOrigen),
                new XElement(XName.Get("CiudadOrigen", SII_NAMESPACE), emisor.CiudadOrigen)
            );
        }

        private XElement ConstruirReceptor(Receptor receptor)
        {
            return new XElement(XName.Get("Receptor", SII_NAMESPACE),
                new XElement(XName.Get("RUTRecep", SII_NAMESPACE), receptor.RUTRecep),
                new XElement(XName.Get("RznSocRecep", SII_NAMESPACE), receptor.RznSocRecep),
                !string.IsNullOrEmpty(receptor.GiroRecep) ? new XElement(XName.Get("GiroRecep", SII_NAMESPACE), receptor.GiroRecep) : null,
                new XElement(XName.Get("DirRecep", SII_NAMESPACE), receptor.DirRecep),
                new XElement(XName.Get("CmnaRecep", SII_NAMESPACE), receptor.CmnaRecep),
                new XElement(XName.Get("CiudadRecep", SII_NAMESPACE), receptor.CiudadRecep)
            );
        }

        private XElement ConstruirDetalles(DocumentoTributario documento)
        {
            var detallesElement = new XElement(XName.Get("Detalle", SII_NAMESPACE));

            for (int i = 0; i < documento.Detalles.Count; i++)
            {
                var detalle = documento.Detalles[i];
                var detalleElement = new XElement(XName.Get("Detalle", SII_NAMESPACE),
                    new XElement(XName.Get("NroLinDet", SII_NAMESPACE), i + 1),
                    !string.IsNullOrEmpty(detalle.Codigo) ? new XElement(XName.Get("CdgItem", SII_NAMESPACE),
                        new XElement(XName.Get("TpoCodigo", SII_NAMESPACE), "INT1"),
                        new XElement(XName.Get("VlrCodigo", SII_NAMESPACE), detalle.Codigo)
                    ) : null,
                    new XElement(XName.Get("NmbItem", SII_NAMESPACE), detalle.Nombre),
                    new XElement(XName.Get("QtyItem", SII_NAMESPACE), detalle.Cantidad),
                    new XElement(XName.Get("UnmdItem", SII_NAMESPACE), detalle.Unidad),
                    new XElement(XName.Get("PrcItem", SII_NAMESPACE), (int)detalle.PrecioUnitario),
                    detalle.DescuentoMonto.HasValue ? new XElement(XName.Get("DescuentoMonto", SII_NAMESPACE), (int)detalle.DescuentoMonto.Value) : null,
                    detalle.DescuentoPct.HasValue ? new XElement(XName.Get("DescuentoPct", SII_NAMESPACE), (int)detalle.DescuentoPct.Value) : null,
                    new XElement(XName.Get("MontoItem", SII_NAMESPACE), (int)detalle.MontoItem)
                );

                detallesElement.Add(detalleElement);
            }

            return detallesElement;
        }

        private XElement ConstruirTotales(DocumentoTributario documento)
        {
            return new XElement(XName.Get("Totales", SII_NAMESPACE),
                new XElement(XName.Get("MntNeto", SII_NAMESPACE), documento.Totales.MntNeto),
                new XElement(XName.Get("IVA", SII_NAMESPACE), documento.Totales.IVA),
                new XElement(XName.Get("MntTotal", SII_NAMESPACE), documento.Totales.MntTotal)
            );
        }

        private XElement? ConstruirReferencias(DocumentoTributario documento)
        {
            if (documento.Referencias == null || !documento.Referencias.Any())
                return null;

            var referenciasElement = new XElement(XName.Get("Referencias", SII_NAMESPACE));

            for (int i = 0; i < documento.Referencias.Count; i++)
            {
                var referencia = documento.Referencias[i];
                var referenciaElement = new XElement(XName.Get("Referencia", SII_NAMESPACE),
                    new XElement(XName.Get("NroLinRef", SII_NAMESPACE), i + 1),
                    new XElement(XName.Get("TpoDocRef", SII_NAMESPACE), referencia.TpoDocRef),
                    new XElement(XName.Get("FolioRef", SII_NAMESPACE), referencia.FolioRef),
                    new XElement(XName.Get("FchaRef", SII_NAMESPACE), referencia.FchaRef.ToString("yyyy-MM-dd")),
                    new XElement(XName.Get("CodRef", SII_NAMESPACE), referencia.CodRef),
                    !string.IsNullOrEmpty(referencia.RazonRef) ? new XElement(XName.Get("RazonRef", SII_NAMESPACE), referencia.RazonRef) : null
                );

                referenciasElement.Add(referenciaElement);
            }

            return referenciasElement;
        }

        // Métodos auxiliares para construir con XmlDocument (como código funcional)
        private XmlElement ConstruirEncabezadoXml(XmlDocument doc, DocumentoTributario documento)
        {
            var encabezado = doc.CreateElement("Encabezado");

            var idDoc = doc.CreateElement("IdDoc");
            idDoc.AppendChild(CrearElemento(doc, "TipoDTE", documento.TipoDTE.ToString()));
            idDoc.AppendChild(CrearElemento(doc, "Folio", documento.Folio.ToString()));
            idDoc.AppendChild(CrearElemento(doc, "FchEmis", documento.FechaEmision.ToString("yyyy-MM-dd")));
            if (documento.Encabezado.FormaPago.HasValue)
            {
                idDoc.AppendChild(CrearElemento(doc, "FmaPago", documento.Encabezado.FormaPago.Value.ToString()));
            }
            encabezado.AppendChild(idDoc);

            encabezado.AppendChild(ConstruirEmisorXml(doc, documento.Encabezado.Emisor));
            encabezado.AppendChild(ConstruirReceptorXml(doc, documento.Encabezado.Receptor));

            // Totales debe ir DENTRO de Encabezado según esquema SII
            encabezado.AppendChild(ConstruirTotalesXml(doc, documento));

            return encabezado;
        }

        private XmlElement ConstruirEmisorXml(XmlDocument doc, Emisor emisor)
        {
            var emisorElement = doc.CreateElement("Emisor");
            emisorElement.AppendChild(CrearElemento(doc, "RUTEmisor", emisor.RUTEmisor));
            emisorElement.AppendChild(CrearElemento(doc, "RznSoc", emisor.RznSoc));
            emisorElement.AppendChild(CrearElemento(doc, "GiroEmis", emisor.GiroEmis));
            emisorElement.AppendChild(CrearElemento(doc, "Acteco", emisor.Acteco));
            emisorElement.AppendChild(CrearElemento(doc, "DirOrigen", emisor.DirOrigen));
            emisorElement.AppendChild(CrearElemento(doc, "CmnaOrigen", emisor.CmnaOrigen));
            emisorElement.AppendChild(CrearElemento(doc, "CiudadOrigen", emisor.CiudadOrigen));
            return emisorElement;
        }

        private XmlElement ConstruirReceptorXml(XmlDocument doc, Receptor receptor)
        {
            var receptorElement = doc.CreateElement("Receptor");
            receptorElement.AppendChild(CrearElemento(doc, "RUTRecep", receptor.RUTRecep));
            receptorElement.AppendChild(CrearElemento(doc, "RznSocRecep", receptor.RznSocRecep));
            if (!string.IsNullOrEmpty(receptor.GiroRecep))
            {
                receptorElement.AppendChild(CrearElemento(doc, "GiroRecep", receptor.GiroRecep));
            }
            receptorElement.AppendChild(CrearElemento(doc, "DirRecep", receptor.DirRecep));
            receptorElement.AppendChild(CrearElemento(doc, "CmnaRecep", receptor.CmnaRecep));
            receptorElement.AppendChild(CrearElemento(doc, "CiudadRecep", receptor.CiudadRecep));
            return receptorElement;
        }

        private List<XmlElement> ConstruirDetallesXml(XmlDocument doc, DocumentoTributario documento)
        {
            var detalles = new List<XmlElement>();

            for (int i = 0; i < documento.Detalles.Count; i++)
            {
                var detalle = documento.Detalles[i];
                var detalleElement = doc.CreateElement("Detalle");
                detalleElement.AppendChild(CrearElemento(doc, "NroLinDet", (i + 1).ToString()));

                // IndExe: 1=Exento (debe ir antes de CdgItem según esquema SII)
                if (detalle.IndExe.HasValue)
                {
                    detalleElement.AppendChild(CrearElemento(doc, "IndExe", detalle.IndExe.Value.ToString()));
                }

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

                if (detalle.DescuentoPct.HasValue)
                {
                    detalleElement.AppendChild(CrearElemento(doc, "DescuentoPct", ((int)detalle.DescuentoPct.Value).ToString()));
                }
                if (detalle.DescuentoMonto.HasValue)
                {
                    detalleElement.AppendChild(CrearElemento(doc, "DescuentoMonto", ((int)detalle.DescuentoMonto.Value).ToString()));
                }

                detalleElement.AppendChild(CrearElemento(doc, "MontoItem", ((int)detalle.MontoItem).ToString()));
                detalles.Add(detalleElement);
            }

            return detalles;
        }

        private XmlElement ConstruirTotalesXml(XmlDocument doc, DocumentoTributario documento)
        {
            var totales = doc.CreateElement("Totales");

            // MntNeto solo si hay items afectos
            if (documento.Totales.MntNeto > 0)
            {
                totales.AppendChild(CrearElemento(doc, "MntNeto", documento.Totales.MntNeto.ToString()));
            }

            // MntExe si hay items exentos
            if (documento.Totales.MntExe.HasValue && documento.Totales.MntExe.Value > 0)
            {
                totales.AppendChild(CrearElemento(doc, "MntExe", documento.Totales.MntExe.Value.ToString()));
            }

            // TasaIVA e IVA solo si hay items afectos
            if (documento.Totales.IVA > 0)
            {
                totales.AppendChild(CrearElemento(doc, "TasaIVA", "19"));
                totales.AppendChild(CrearElemento(doc, "IVA", documento.Totales.IVA.ToString()));
            }

            totales.AppendChild(CrearElemento(doc, "MntTotal", documento.Totales.MntTotal.ToString()));
            return totales;
        }

        private List<XmlElement> ConstruirReferenciasXml(XmlDocument doc, DocumentoTributario documento)
        {
            var lista = new List<XmlElement>();
            if (documento.Referencias == null || !documento.Referencias.Any())
                return lista;

            for (int i = 0; i < documento.Referencias.Count; i++)
            {
                var referencia = documento.Referencias[i];
                var referenciaElement = doc.CreateElement("Referencia");
                referenciaElement.AppendChild(CrearElemento(doc, "NroLinRef", (i + 1).ToString()));
                referenciaElement.AppendChild(CrearElemento(doc, "TpoDocRef", referencia.TpoDocRef.ToString()));
                referenciaElement.AppendChild(CrearElemento(doc, "FolioRef", referencia.FolioRef.ToString()));
                referenciaElement.AppendChild(CrearElemento(doc, "FchRef", referencia.FchaRef.ToString("yyyy-MM-dd")));
                referenciaElement.AppendChild(CrearElemento(doc, "CodRef", referencia.CodRef.ToString()));
                if (!string.IsNullOrEmpty(referencia.RazonRef))
                {
                    referenciaElement.AppendChild(CrearElemento(doc, "RazonRef", referencia.RazonRef));
                }
                lista.Add(referenciaElement);
            }

            return lista;
        }

        private XmlElement ConstruirDescuentosGlobalesXml(XmlDocument doc, DocumentoTributario documento)
        {
            var descuento = documento.DescuentosGlobales![0];
            var dscRcgGlobalElement = doc.CreateElement("DscRcgGlobal");
            dscRcgGlobalElement.AppendChild(CrearElemento(doc, "NroLinDR", descuento.NroLinDR.ToString()));
            dscRcgGlobalElement.AppendChild(CrearElemento(doc, "TpoMov", descuento.TpoMov));
            if (!string.IsNullOrEmpty(descuento.GlosaDR))
            {
                dscRcgGlobalElement.AppendChild(CrearElemento(doc, "GlosaDR", descuento.GlosaDR));
            }
            dscRcgGlobalElement.AppendChild(CrearElemento(doc, "TpoValor", descuento.TpoValor));
            dscRcgGlobalElement.AppendChild(CrearElemento(doc, "ValorDR", descuento.ValorDR.ToString("0.##")));
            if (descuento.IndExeDR.HasValue)
            {
                dscRcgGlobalElement.AppendChild(CrearElemento(doc, "IndExeDR", descuento.IndExeDR.Value.ToString()));
            }
            return dscRcgGlobalElement;
        }

        private XmlElement CrearElemento(XmlDocument doc, string nombre, string valor)
        {
            var elemento = doc.CreateElement(nombre);
            if (valor != null)
            {
                elemento.AppendChild(doc.CreateTextNode(valor));
            }
            return elemento;
        }
    }
}
