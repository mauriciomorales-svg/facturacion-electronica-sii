using FacturacionElectronicaSII.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FacturacionElectronicaSII.Controllers
{
    /// <summary>
    /// Controlador OPCIONAL para validaciones y debugging
    /// NO afecta el flujo normal del sistema
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ValidacionController : ControllerBase
    {
        private readonly IValidacionService _validacionService;
        private readonly ILogger<ValidacionController> _logger;

        public ValidacionController(
            IValidacionService validacionService,
            ILogger<ValidacionController> logger)
        {
            _validacionService = validacionService;
            _logger = logger;
        }

        /// <summary>
        /// Valida un archivo XML contra el schema del SII
        /// OPCIONAL - solo para debugging
        /// </summary>
        /// <param name="nombreArchivo">Nombre del archivo XML en Data/XMLs/</param>
        [HttpGet("validar-xml/{nombreArchivo}")]
        public async Task<IActionResult> ValidarXML(string nombreArchivo)
        {
            try
            {
                _logger.LogInformation("Validando XML: {Archivo}", nombreArchivo);

                // Leer el archivo XML
                var rutaXmls = Path.Combine(Directory.GetCurrentDirectory(), "Data", "XMLs", nombreArchivo);
                
                if (!System.IO.File.Exists(rutaXmls))
                {
                    return NotFound(new { 
                        exito = false, 
                        mensaje = $"Archivo no encontrado: {nombreArchivo}" 
                    });
                }

                var xmlContent = await System.IO.File.ReadAllTextAsync(rutaXmls);

                // Validar contra schema EnvioDTE
                var errores = await _validacionService.ValidarContraSchemaAsync(xmlContent, "EnvioDTE_v10.xsd");

                if (errores.Any())
                {
                    return Ok(new
                    {
                        exito = false,
                        mensaje = "XML no cumple con el schema del SII",
                        archivo = nombreArchivo,
                        erroresValidacion = errores
                    });
                }

                return Ok(new
                {
                    exito = true,
                    mensaje = "XML válido según schema del SII",
                    archivo = nombreArchivo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar XML");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al validar XML",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lista todos los XMLs disponibles para validar
        /// </summary>
        [HttpGet("listar-xmls")]
        public IActionResult ListarXMLs()
        {
            try
            {
                var rutaXmls = Path.Combine(Directory.GetCurrentDirectory(), "Data", "XMLs");
                
                if (!Directory.Exists(rutaXmls))
                {
                    return Ok(new { 
                        exito = true, 
                        mensaje = "No hay XMLs disponibles",
                        archivos = new string[0]
                    });
                }

                var archivos = Directory.GetFiles(rutaXmls, "*.xml")
                    .Select(f => Path.GetFileName(f))
                    .OrderByDescending(f => f)
                    .ToList();

                return Ok(new
                {
                    exito = true,
                    mensaje = $"Se encontraron {archivos.Count} archivos XML",
                    archivos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar XMLs");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al listar XMLs",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Verifica el estado de los schemas del SII
        /// </summary>
        [HttpGet("estado-schemas")]
        public IActionResult EstadoSchemas()
        {
            try
            {
                var rutaSchemas = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Schemas");
                
                var schemasRequeridos = new[]
                {
                    "EnvioDTE_v10.xsd",
                    "DTE_v10.xsd",
                    "SiiTypes_v10.xsd",
                    "xmldsignature_v10.xsd"
                };

                var estadoSchemas = schemasRequeridos.Select(schema => new
                {
                    nombre = schema,
                    existe = System.IO.File.Exists(Path.Combine(rutaSchemas, schema)),
                    ruta = Path.Combine(rutaSchemas, schema)
                }).ToList();

                var todosPresentes = estadoSchemas.All(s => s.existe);

                return Ok(new
                {
                    exito = true,
                    todosPresentes,
                    mensaje = todosPresentes 
                        ? "Todos los schemas están disponibles" 
                        : "Faltan algunos schemas",
                    schemas = estadoSchemas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar schemas");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al verificar schemas",
                    error = ex.Message
                });
            }
        }
    }
}
