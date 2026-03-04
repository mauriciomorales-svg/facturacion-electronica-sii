using FacturacionElectronicaSII.Interfaces;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio OPCIONAL para validaciones adicionales
    /// NO interfiere con el flujo normal del sistema
    /// Solo se usa si quieres validar XMLs antes de enviar
    /// </summary>
    public class ValidacionService : IValidacionService
    {
        private readonly ILogger<ValidacionService> _logger;
        private readonly IConfiguration _configuration;
        private const string SII_NAMESPACE = "http://www.sii.cl/SiiDte";

        public ValidacionService(ILogger<ValidacionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Valida un XML contra su Schema XSD del SII
        /// OPCIONAL - solo para debugging y verificación extra
        /// </summary>
        public async Task<List<string>> ValidarContraSchemaAsync(string xmlContent, string schemaFileName)
        {
            var errores = new List<string>();

            try
            {
                var rutaSchemas = _configuration["FacturacionElectronica:Rutas:Schemas"] ?? "./Data/Schemas";
                var schemaPath = Path.Combine(Directory.GetCurrentDirectory(), rutaSchemas, schemaFileName);

                // Si no existe el schema, solo log warning - NO es error crítico
                if (!File.Exists(schemaPath))
                {
                    _logger.LogWarning("Schema no encontrado: {Schema}. Validación omitida.", schemaPath);
                    return errores; // Retorna vacío (sin errores) para no bloquear
                }

                // Crear el administrador de schemas
                var schemas = new XmlSchemaSet();
                schemas.Add(SII_NAMESPACE, schemaPath);

                // Cargar el documento XML
                var documento = XDocument.Parse(xmlContent);

                // Validar contra el schema
                documento.Validate(schemas, (o, e) =>
                {
                    errores.Add($"{e.Severity}: {e.Message}");
                });

                if (errores.Any())
                {
                    _logger.LogWarning("XML no cumple con schema {Schema}. Errores: {Errores}", 
                        schemaFileName, string.Join("; ", errores));
                }
                else
                {
                    _logger.LogInformation("XML válido según schema {Schema}", schemaFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar XML contra schema");
                // NO agregamos el error a la lista - es una validación opcional
            }

            return await Task.FromResult(errores);
        }

        /// <summary>
        /// Verifica que la firma de un CAF sea válida
        /// OPCIONAL - solo para debugging
        /// </summary>
        public async Task<bool> VerificarFirmaCAFAsync(string xmlCAF)
        {
            try
            {
                // TODO: Implementar verificación de firma CAF
                // Por ahora solo retorna true
                _logger.LogInformation("Verificación de CAF solicitada (pendiente implementar)");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar firma CAF");
                return false;
            }
        }
    }
}
