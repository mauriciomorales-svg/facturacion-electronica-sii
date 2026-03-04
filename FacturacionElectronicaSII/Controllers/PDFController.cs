using FacturacionElectronicaSII.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FacturacionElectronicaSII.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PDFController : ControllerBase
    {
        private readonly IPDFService _pdfService;
        private readonly ILogger<PDFController> _logger;
        private readonly IConfiguration _configuration;

        public PDFController(
            IPDFService pdfService,
            ILogger<PDFController> logger,
            IConfiguration configuration)
        {
            _pdfService = pdfService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Genera PDF de un DTE desde su archivo XML
        /// </summary>
        /// <param name="nombreArchivo">Nombre del archivo XML en Data/XMLs/</param>
        [HttpGet("generar/{nombreArchivo}")]
        public async Task<IActionResult> GenerarPDF(string nombreArchivo)
        {
            try
            {
                _logger.LogInformation("Generando PDF para: {Archivo}", nombreArchivo);

                // Leer el archivo XML
                var rutaXmls = Path.Combine(Directory.GetCurrentDirectory(), "Data", "XMLs", nombreArchivo);

                if (!System.IO.File.Exists(rutaXmls))
                {
                    return NotFound(new
                    {
                        exito = false,
                        mensaje = $"Archivo XML no encontrado: {nombreArchivo}"
                    });
                }

                var xmlContent = await System.IO.File.ReadAllTextAsync(rutaXmls);

                // Generar PDF
                var (rutaPDF, pdfBytes) = await _pdfService.GenerarPDFAsync(xmlContent);

                return Ok(new
                {
                    exito = true,
                    mensaje = "PDF generado exitosamente",
                    archivoXML = nombreArchivo,
                    archivoPDF = Path.GetFileName(rutaPDF),
                    rutaPDF,
                    tamañoKB = pdfBytes.Length / 1024
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al generar PDF",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Descarga un PDF generado
        /// </summary>
        /// <param name="nombreArchivo">Nombre del archivo PDF</param>
        [HttpGet("descargar/{nombreArchivo}")]
        public async Task<IActionResult> DescargarPDF(string nombreArchivo)
        {
            try
            {
                var rutaPDFs = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    _configuration["FacturacionElectronica:Rutas:PDFs"] ?? "./Data/PDFs"
                );

                var rutaCompleta = Path.Combine(rutaPDFs, nombreArchivo);

                if (!System.IO.File.Exists(rutaCompleta))
                {
                    return NotFound(new
                    {
                        exito = false,
                        mensaje = $"PDF no encontrado: {nombreArchivo}"
                    });
                }

                var pdfBytes = await System.IO.File.ReadAllBytesAsync(rutaCompleta);

                return File(pdfBytes, "application/pdf", nombreArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar PDF");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al descargar PDF",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lista todos los PDFs generados
        /// </summary>
        [HttpGet("listar")]
        public IActionResult ListarPDFs()
        {
            try
            {
                var rutaPDFs = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    _configuration["FacturacionElectronica:Rutas:PDFs"] ?? "./Data/PDFs"
                );

                if (!Directory.Exists(rutaPDFs))
                {
                    return Ok(new
                    {
                        exito = true,
                        mensaje = "No hay PDFs generados",
                        archivos = new string[0]
                    });
                }

                var archivos = Directory.GetFiles(rutaPDFs, "*.pdf")
                    .Select(f => new
                    {
                        nombre = Path.GetFileName(f),
                        tamañoKB = new FileInfo(f).Length / 1024,
                        fechaCreacion = System.IO.File.GetCreationTime(f).ToString("yyyy-MM-dd HH:mm:ss")
                    })
                    .OrderByDescending(f => f.fechaCreacion)
                    .ToList();

                return Ok(new
                {
                    exito = true,
                    mensaje = $"Se encontraron {archivos.Count} PDFs",
                    archivos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar PDFs");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al listar PDFs",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Genera PDFs de todos los XMLs disponibles
        /// </summary>
        [HttpPost("generar-todos")]
        public async Task<IActionResult> GenerarTodosPDFs()
        {
            try
            {
                _logger.LogInformation("Generando PDFs de todos los XMLs");

                var rutaXmls = Path.Combine(Directory.GetCurrentDirectory(), "Data", "XMLs");

                if (!Directory.Exists(rutaXmls))
                {
                    return Ok(new
                    {
                        exito = false,
                        mensaje = "No hay XMLs disponibles"
                    });
                }

                var archivosXML = Directory.GetFiles(rutaXmls, "EnvioDTE_*.xml");
                var resultados = new List<object>();
                int exitosos = 0;
                int errores = 0;

                foreach (var archivoXML in archivosXML)
                {
                    try
                    {
                        var xmlContent = await System.IO.File.ReadAllTextAsync(archivoXML);
                        var (rutaPDF, pdfBytes) = await _pdfService.GenerarPDFAsync(xmlContent);

                        resultados.Add(new
                        {
                            archivoXML = Path.GetFileName(archivoXML),
                            archivoPDF = Path.GetFileName(rutaPDF),
                            exito = true
                        });

                        exitosos++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al generar PDF de {Archivo}", Path.GetFileName(archivoXML));

                        resultados.Add(new
                        {
                            archivoXML = Path.GetFileName(archivoXML),
                            exito = false,
                            error = ex.Message
                        });

                        errores++;
                    }
                }

                return Ok(new
                {
                    exito = true,
                    mensaje = $"Proceso completado: {exitosos} exitosos, {errores} errores",
                    totalProcesados = archivosXML.Length,
                    exitosos,
                    errores,
                    resultados
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar todos los PDFs");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al generar PDFs",
                    error = ex.Message
                });
            }
        }
    }
}
