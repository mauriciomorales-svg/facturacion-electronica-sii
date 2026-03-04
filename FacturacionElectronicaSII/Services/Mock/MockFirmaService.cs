using FacturacionElectronicaSII.Interfaces;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace FacturacionElectronicaSII.Services.Mock
{
    /// <summary>
    /// Implementación Mock del servicio de firma para desarrollo
    /// Simula la firma sin necesidad de certificados reales
    /// </summary>
    public class MockFirmaService : IFirmaService
    {
        private readonly ILogger<MockFirmaService> _logger;

        public MockFirmaService(ILogger<MockFirmaService> logger)
        {
            _logger = logger;
        }

        public string FirmarDTE(string xmlDTE, string idDocumento)
        {
            _logger.LogInformation("Mock: Firmando DTE {IdDocumento} (simulado)", idDocumento);

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.LoadXml(xmlDTE);

                // En modo Mock, simplemente agregamos un nodo de firma simulado
                // En producción, aquí se haría la firma real con certificado digital
                var signatureElement = xmlDoc.CreateElement("Signature", "http://www.w3.org/2000/09/xmldsig#");
                signatureElement.SetAttribute("xmlns", "http://www.w3.org/2000/09/xmldsig#");

                var signedInfo = xmlDoc.CreateElement("SignedInfo", "http://www.w3.org/2000/09/xmldsig#");
                var signatureValue = xmlDoc.CreateElement("SignatureValue", "http://www.w3.org/2000/09/xmldsig#");
                signatureValue.InnerText = "MOCK_SIGNATURE_VALUE_BASE64";

                signatureElement.AppendChild(signedInfo);
                signatureElement.AppendChild(signatureValue);

                // Agregar la firma al documento
                var root = xmlDoc.DocumentElement;
                if (root != null)
                {
                    root.AppendChild(signatureElement);
                }

                _logger.LogInformation("Mock: DTE firmado exitosamente (simulado)");
                return xmlDoc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar DTE en modo Mock");
                throw;
            }
        }

        public string FirmarEnvioDTE(string xmlEnvioDTE)
        {
            _logger.LogInformation("Mock: Firmando EnvioDTE (simulado)");

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.LoadXml(xmlEnvioDTE);

                // Similar a FirmarDTE, agregamos firma simulada
                var signatureElement = xmlDoc.CreateElement("Signature", "http://www.w3.org/2000/09/xmldsig#");
                signatureElement.SetAttribute("xmlns", "http://www.w3.org/2000/09/xmldsig#");

                var signedInfo = xmlDoc.CreateElement("SignedInfo", "http://www.w3.org/2000/09/xmldsig#");
                var signatureValue = xmlDoc.CreateElement("SignatureValue", "http://www.w3.org/2000/09/xmldsig#");
                signatureValue.InnerText = "MOCK_SIGNATURE_VALUE_BASE64_ENVIO";

                signatureElement.AppendChild(signedInfo);
                signatureElement.AppendChild(signatureValue);

                var root = xmlDoc.DocumentElement;
                if (root != null)
                {
                    root.AppendChild(signatureElement);
                }

                _logger.LogInformation("Mock: EnvioDTE firmado exitosamente (simulado)");
                return xmlDoc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar EnvioDTE en modo Mock");
                throw;
            }
        }

        public string FirmarLibro(string xmlLibro)
        {
            _logger.LogInformation("Mock: Firmando Libro (simulado)");

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.LoadXml(xmlLibro);

                // Agregar firma simulada
                var signatureElement = xmlDoc.CreateElement("Signature", "http://www.w3.org/2000/09/xmldsig#");
                signatureElement.SetAttribute("xmlns", "http://www.w3.org/2000/09/xmldsig#");

                var signedInfo = xmlDoc.CreateElement("SignedInfo", "http://www.w3.org/2000/09/xmldsig#");
                var signatureValue = xmlDoc.CreateElement("SignatureValue", "http://www.w3.org/2000/09/xmldsig#");
                signatureValue.InnerText = "MOCK_SIGNATURE_VALUE_BASE64_LIBRO";

                signatureElement.AppendChild(signedInfo);
                signatureElement.AppendChild(signatureValue);

                var root = xmlDoc.DocumentElement;
                if (root != null)
                {
                    root.AppendChild(signatureElement);
                }

                _logger.LogInformation("Mock: Libro firmado exitosamente (simulado)");
                return xmlDoc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar Libro en modo Mock");
                throw;
            }
        }

        public string FirmarRCOF(string xmlRCOF)
        {
            _logger.LogInformation("Mock: Firmando RCOF (simulado)");

            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                xmlDoc.LoadXml(xmlRCOF);

                // Agregar firma simulada
                var signatureElement = xmlDoc.CreateElement("Signature", "http://www.w3.org/2000/09/xmldsig#");
                signatureElement.SetAttribute("xmlns", "http://www.w3.org/2000/09/xmldsig#");

                var signedInfo = xmlDoc.CreateElement("SignedInfo", "http://www.w3.org/2000/09/xmldsig#");
                var signatureValue = xmlDoc.CreateElement("SignatureValue", "http://www.w3.org/2000/09/xmldsig#");
                signatureValue.InnerText = "MOCK_SIGNATURE_VALUE_BASE64_RCOF";

                signatureElement.AppendChild(signedInfo);
                signatureElement.AppendChild(signatureValue);

                var root = xmlDoc.DocumentElement;
                if (root != null)
                {
                    root.AppendChild(signatureElement);
                }

                _logger.LogInformation("Mock: RCOF firmado exitosamente (simulado)");
                return xmlDoc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar RCOF en modo Mock");
                throw;
            }
        }

        public bool ValidarFirma(string xmlFirmado)
        {
            _logger.LogInformation("Mock: Validando firma (simulado - siempre retorna true)");
            // En modo Mock, siempre retornamos true
            return true;
        }
    }
}
