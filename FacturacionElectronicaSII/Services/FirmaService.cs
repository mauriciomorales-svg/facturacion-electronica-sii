using FacturacionElectronicaSII.Interfaces;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio real de firma digital usando certificados X.509
    /// Basado en código funcional probado
    /// </summary>
    public class FirmaService : IFirmaService
    {
        private readonly ILogger<FirmaService> _logger;
        private readonly IConfiguration _configuration;

        public FirmaService(ILogger<FirmaService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public string FirmarDTE(string xmlDTE, string idDocumento)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [FIRMA_DTE_INICIO] Referencia=#{Referencia}, Cert={Cert}", 
                timestamp, idDocumento, _configuration["FacturacionElectronica:Certificado:Nombre"] ?? "No configurado");

            try
            {
                var doc = new XmlDocument();
                doc.PreserveWhitespace = true;  // CRÍTICO según código funcional
                doc.LoadXml(xmlDTE);

                // Buscar nodo Documento por ID
                var documentoNode = doc.SelectNodes($"//*[@ID='{idDocumento}']")?[0];
                if (documentoNode == null)
                {
                    throw new InvalidOperationException($"No se encontró el nodo con ID '{idDocumento}'");
                }

                // Obtener certificado
                var nombreCertificado = _configuration["FacturacionElectronica:Certificado:Nombre"] ?? "";
                if (string.IsNullOrEmpty(nombreCertificado))
                {
                    throw new InvalidOperationException("No se ha configurado el nombre del certificado");
                }

                var certificado = RecuperarCertificado(nombreCertificado);
                if (certificado != null)
                {
                    _logger.LogInformation("[{Timestamp}] [FIRMA_DTE_CERT_OK] {Subject}", 
                        timestamp, certificado.Subject);
                }

                // Construir SignedXml
                var signedXml = new SignedXml(doc)
                {
                    SigningKey = certificado.GetRSAPrivateKey()
                };

                signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
                signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

                var reference = new Reference
                {
                    Uri = $"#{idDocumento}",
                    DigestMethod = SignedXml.XmlDsigSHA1Url
                };
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                signedXml.AddReference(reference);

                // KeyInfo
                var keyInfo = new KeyInfo();
                keyInfo.AddClause(new RSAKeyValue(certificado.GetRSAPublicKey()));
                keyInfo.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = keyInfo;

                var timestampAntesCompute = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logger.LogInformation("[{Timestamp}] [FIRMA_DTE_ANTES_COMPUTE] DigestMethod={DigestMethod}, SignatureMethod={SignatureMethod}", 
                    timestampAntesCompute, SignedXml.XmlDsigSHA1Url, SignedXml.XmlDsigRSASHA1Url);

                signedXml.ComputeSignature();
                var xmlSignature = signedXml.GetXml();

                // Limpiar firmas previas (misma lógica que código funcional)
                // Firmas dentro del Documento (incorrectas)
                var firmasPreviasEnDocumento = documentoNode.SelectNodes(".//*[local-name()='Signature']");
                int removidasEnDocumento = 0;
                foreach (XmlNode sig in firmasPreviasEnDocumento)
                {
                    documentoNode.RemoveChild(sig);
                    removidasEnDocumento++;
                }
                
                // Firmas hermanas del Documento (nivel DTE)
                var dteNode = doc.GetElementsByTagName("DTE")[0] as XmlElement;
                if (dteNode != null)
                {
                    var firmasPreviasEnDTE = dteNode.SelectNodes("*[local-name()='Signature']");
                    int removidasEnDTE = 0;
                    foreach (XmlNode sig in firmasPreviasEnDTE)
                    {
                        dteNode.RemoveChild(sig);
                        removidasEnDTE++;
                    }
                    var timestamp2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    _logger.LogInformation("[{Timestamp}] [FIRMA_DTE_CLEAN_SIG] Firmas previas eliminadas: {RemovidasEnDocumento} dentro de Documento, {RemovidasEnDTE} en nivel DTE", 
                        timestamp2, removidasEnDocumento, removidasEnDTE);
                    
                    // Verificar que documentoNode es hijo de dteNode
                    if (documentoNode.ParentNode != dteNode)
                    {
                        throw new Exception("El nodo Documento no es hijo directo del nodo DTE.");
                    }
                    
                    // Insertar la firma DESPUÉS del Documento (como siguiente hermano)
                    // Esto asegura que <Signature> sea hermana de <Documento>, no hija
                    var signatureNode = doc.ImportNode(xmlSignature, true);
                    dteNode.InsertAfter(signatureNode, documentoNode);
                    _logger.LogInformation("[{Timestamp}] [FIRMA_DTE_FIN] Firma DTE insertada como HERMANA del Documento (nivel DTE) - Estructura correcta según XSD", timestamp2);
                }

                // Retornar el XML firmado, manteniendo el formato original
                string xmlFirmado = doc.OuterXml;
                
                // PASO 2: Formateo Seguro de la Firma DTE (misma lógica que código funcional)
                // INYECTAR SALTOS DE LÍNEA AHORA, para que el EnvioDTE firme esto tal cual.
                // Solo rompemos líneas en el certificado/firma, donde es seguro.
                string xmlFinal = xmlFirmado
                    .Replace("</SignatureValue><KeyInfo>", "</SignatureValue>\r\n<KeyInfo>")
                    .Replace("</KeyInfo></Signature>", "</KeyInfo>\r\n</Signature>");
                
                var timestamp3 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logger.LogInformation("[{Timestamp}] [FIRMA_DTE_FORMATEO] Saltos de línea seguros aplicados en KeyInfo (sin afectar SignedInfo)", timestamp3);
                _logger.LogInformation("DTE firmado exitosamente con formato preservado");
                return xmlFinal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar DTE");
                throw;
            }
        }

        public string FirmarEnvioDTE(string xmlEnvioDTE)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_INICIO] Firmando EnvioDTE (preservando formato)", timestamp);

            try
            {
                var doc = new XmlDocument();
                doc.PreserveWhitespace = true;  // CRÍTICO según código funcional
                doc.LoadXml(xmlEnvioDTE);

                var root = doc.DocumentElement;
                if (root == null)
                {
                    throw new InvalidOperationException("El XML de EnvioDTE no tiene elemento raíz");
                }

                // CORRECCIÓN CRÍTICA DE CABECERAS (FIX SCH-00001) - misma lógica que código funcional
                root.RemoveAttribute("schemaLocation");
                root.RemoveAttribute("xsi:schemaLocation");
                if (root.HasAttribute("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance"))
                    root.RemoveAttribute("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance");

                root.SetAttribute("xmlns", "http://www.sii.cl/SiiDte");
                root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                root.SetAttribute(
                    "schemaLocation",
                    "http://www.w3.org/2001/XMLSchema-instance",
                    "http://www.sii.cl/SiiDte EnvioDTE_v10.xsd"
                );
                root.SetAttribute("version", "1.0");
                _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_SCHEMA_FIX] Cabeceras forzadas correctamente a EnvioDTE_v10.xsd", timestamp);

                // Obtener SetDTE y asegurar ID="SetDoc"
                var setDTE = root.SelectSingleNode("*[local-name()='SetDTE']") as XmlElement;
                if (setDTE == null)
                {
                    throw new Exception("No existe <SetDTE> en EnvioDTE.");
                }
                
                // Asegurar ID="SetDoc"
                var idAttr = setDTE.Attributes["ID"] ?? setDTE.Attributes["Id"];
                if (idAttr == null)
                {
                    idAttr = doc.CreateAttribute("ID");
                    setDTE.Attributes.Append(idAttr);
                }
                idAttr.Value = "SetDoc";
                _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_SETDTE_ID] SetDTE ID=\"SetDoc\" asegurado.", timestamp);
                
                // Eliminar firmas previas (misma lógica que código funcional)
                var sigsSet = setDTE.SelectNodes("./*[local-name()='Signature']");
                if (sigsSet != null)
                {
                    foreach (XmlNode s in sigsSet) setDTE.RemoveChild(s);
                }
                var sigsEnvio = root.SelectNodes("./*[local-name()='Signature']");
                if (sigsEnvio != null)
                {
                    foreach (XmlNode s in sigsEnvio) root.RemoveChild(s);
                }
                _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_CLEAN_SIG] Firmas previas eliminadas.", timestamp);

                // Obtener certificado
                var nombreCertificado = _configuration["FacturacionElectronica:Certificado:Nombre"] ?? "";
                if (string.IsNullOrEmpty(nombreCertificado))
                {
                    throw new InvalidOperationException("No se ha configurado el nombre del certificado");
                }

                var certificado = RecuperarCertificado(nombreCertificado);
                if (certificado != null)
                {
                    _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_CERT_OK] {Subject}", timestamp, certificado.Subject);
                }

                // Construir SignedXml
                var signedXml = new SignedXml(doc)
                {
                    SigningKey = certificado.GetRSAPrivateKey()
                };

                signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
                signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

                var reference = new Reference
                {
                    Uri = "#SetDoc",
                    DigestMethod = SignedXml.XmlDsigSHA1Url
                };
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                signedXml.AddReference(reference);

                var ki = new KeyInfo();
                ki.AddClause(new RSAKeyValue(certificado.GetRSAPublicKey()));
                ki.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = ki;

                _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_ANTES_COMPUTE] Computando firma del sobre...", timestamp);
                signedXml.ComputeSignature();
                var xmlSignature = signedXml.GetXml();

                // Agregar Signature al final de EnvioDTE
                root.AppendChild(doc.ImportNode(xmlSignature, true));
                _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_INSERT_OK] Signature del SOBRE agregada al final de <EnvioDTE>.", timestamp);

                _logger.LogInformation("[{Timestamp}] [FIRMA_ENVIO_FIN] Firma EnvioDTE terminada correctamente. Formato preservado.", timestamp);
                return doc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar EnvioDTE");
                throw;
            }
        }

        public string FirmarLibro(string xmlLibro)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [FIRMA_LIBRO_INICIO] Firmando Libro de Compras/Ventas", timestamp);

            try
            {
                var doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.LoadXml(xmlLibro);

                // Obtener certificado
                var nombreCertificado = _configuration["FacturacionElectronica:Certificado:Nombre"] ?? "";
                if (string.IsNullOrEmpty(nombreCertificado))
                {
                    throw new InvalidOperationException("No se ha configurado el nombre del certificado");
                }

                var certificado = RecuperarCertificado(nombreCertificado);
                if (certificado != null)
                {
                    _logger.LogInformation("[{Timestamp}] [FIRMA_LIBRO_CERT_OK] {Subject}", 
                        timestamp, certificado.Subject);
                }

                // Firmar el elemento EnvioLibro (ID="SetDoc")
                var envioLibro = doc.GetElementsByTagName("EnvioLibro")[0] as XmlElement;
                if (envioLibro == null)
                {
                    throw new InvalidOperationException("No se encontró elemento EnvioLibro en el XML");
                }

                var signedXml = new SignedXml(doc);
                signedXml.SigningKey = certificado.GetRSAPrivateKey();

                // Referencia al elemento EnvioLibro
                var reference = new Reference("#SetDoc");
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                signedXml.AddReference(reference);

                // Incluir información del certificado
                var keyInfo = new KeyInfo();
                keyInfo.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = keyInfo;

                // Computar firma
                signedXml.ComputeSignature();

                // Agregar firma como hermana de EnvioLibro (al final de LibroCompraVenta)
                var signature = signedXml.GetXml();
                doc.DocumentElement?.AppendChild(doc.ImportNode(signature, true));

                _logger.LogInformation("[{Timestamp}] [FIRMA_LIBRO_OK] Libro firmado exitosamente", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                using (var stringWriter = new StringWriter())
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                {
                    Encoding = Encoding.GetEncoding("ISO-8859-1"),
                    Indent = false,
                    OmitXmlDeclaration = false
                }))
                {
                    doc.Save(xmlWriter);
                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Timestamp}] [FIRMA_LIBRO_ERROR] Error al firmar libro", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                throw;
            }
        }

        public string FirmarRCOF(string xmlRCOF)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logger.LogInformation("[{Timestamp}] [FIRMA_RCOF_INICIO] Firmando RCOF", timestamp);

            try
            {
                var doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.LoadXml(xmlRCOF);

                // Obtener certificado
                var nombreCertificado = _configuration["FacturacionElectronica:Certificado:Nombre"] ?? "";
                if (string.IsNullOrEmpty(nombreCertificado))
                {
                    throw new InvalidOperationException("No se ha configurado el nombre del certificado");
                }

                var certificado = RecuperarCertificado(nombreCertificado);
                if (certificado != null)
                {
                    _logger.LogInformation("[{Timestamp}] [FIRMA_RCOF_CERT_OK] {Subject}", 
                        timestamp, certificado.Subject);
                }

                // Firmar el elemento DocumentoConsumoFolios (ID="CF")
                var docConsumo = doc.GetElementsByTagName("DocumentoConsumoFolios")[0] as XmlElement;
                if (docConsumo == null)
                {
                    throw new InvalidOperationException("No se encontró elemento DocumentoConsumoFolios en el XML");
                }

                var signedXml = new SignedXml(doc);
                signedXml.SigningKey = certificado.GetRSAPrivateKey();

                // Referencia al elemento DocumentoConsumoFolios
                var reference = new Reference("#CF");
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                signedXml.AddReference(reference);

                // Incluir información del certificado
                var keyInfo = new KeyInfo();
                keyInfo.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = keyInfo;

                // Computar firma
                signedXml.ComputeSignature();

                // Agregar firma como hermana de DocumentoConsumoFolios (dentro de ConsumoFolios)
                var signature = signedXml.GetXml();
                doc.DocumentElement?.AppendChild(doc.ImportNode(signature, true));

                _logger.LogInformation("[{Timestamp}] [FIRMA_RCOF_OK] RCOF firmado exitosamente", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                using (var stringWriter = new StringWriter())
                using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                {
                    Encoding = Encoding.GetEncoding("ISO-8859-1"),
                    Indent = false,
                    OmitXmlDeclaration = false
                }))
                {
                    doc.Save(xmlWriter);
                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Timestamp}] [FIRMA_RCOF_ERROR] Error al firmar RCOF", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                throw;
            }
        }

        public bool ValidarFirma(string xmlFirmado)
        {
            _logger.LogInformation("Validando firma del documento");

            try
            {
                var doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.LoadXml(xmlFirmado);

                var signedXml = new SignedXml(doc);
                var signatureNode = doc.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#")[0] as XmlElement;
                
                if (signatureNode == null)
                {
                    _logger.LogWarning("No se encontró nodo Signature en el documento");
                    return false;
                }

                signedXml.LoadXml(signatureNode);
                return signedXml.CheckSignature();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar firma");
                return false;
            }
        }

        /// <summary>
        /// Recupera un certificado X.509 del almacén de Windows
        /// </summary>
        private X509Certificate2 RecuperarCertificado(string nombreCertificado)
        {
            _logger.LogInformation("Buscando certificado: {NombreCertificado}", nombreCertificado);

            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                var collection = store.Certificates;
                var resultados = collection.Find(X509FindType.FindBySubjectName, nombreCertificado, false);

                if (resultados.Count > 0)
                {
                    _logger.LogInformation("Certificado encontrado");
                    return resultados[0];
                }

                throw new Exception($"Certificado no encontrado: {nombreCertificado}");
            }
            finally
            {
                store.Close();
            }
        }
    }
}
