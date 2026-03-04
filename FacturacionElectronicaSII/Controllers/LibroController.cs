using FacturacionElectronicaSII.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FacturacionElectronicaSII.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LibroController : ControllerBase
    {
        private readonly ILibroService _libroService;
        private readonly ISIIService _siiService;
        private readonly ILogger<LibroController> _logger;
        private readonly IConfiguration _configuration;

        public LibroController(
            ILibroService libroService,
            ISIIService siiService,
            ILogger<LibroController> logger,
            IConfiguration configuration)
        {
            _libroService = libroService;
            _siiService = siiService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Genera y envía Libro de Ventas al SII
        /// </summary>
        /// <param name="periodoTributario">Período en formato AAAA-MM (ej: 2026-01)</param>
        [HttpPost("ventas")]
        public async Task<IActionResult> GenerarLibroVentas([FromQuery] string periodoTributario)
        {
            try
            {
                _logger.LogInformation("Generando Libro de Ventas para período {Periodo}", periodoTributario);

                // Validar formato de período
                if (string.IsNullOrEmpty(periodoTributario) || periodoTributario.Length != 7)
                {
                    return BadRequest(new
                    {
                        exito = false,
                        mensaje = "Formato de período inválido. Use AAAA-MM (ej: 2026-01)"
                    });
                }

                // Generar libro
                var xmlLibro = await _libroService.GenerarLibroVentasAsync(periodoTributario);

                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Mock";
                string? trackID = null;

                // Enviar al SII (solo si NO es modo CertificacionManual)
                if (ambiente != "CertificacionManual")
                {
                    try
                    {
                        var semilla = await _siiService.ObtenerSemillaAsync();
                        var token = await _siiService.ObtenerTokenAsync(semilla);
                        var envioResponse = await _siiService.EnviarLibroAsync(xmlLibro, token);

                        if (!envioResponse.Exito)
                        {
                            return Ok(new
                            {
                                exito = false,
                                mensaje = "Error al enviar Libro de Ventas al SII",
                                errores = envioResponse.Errores,
                                xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                            });
                        }

                        trackID = envioResponse.TrackID;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar Libro de Ventas al SII");
                        return Ok(new
                        {
                            exito = false,
                            mensaje = "Error al enviar al SII",
                            error = ex.Message,
                            xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                        });
                    }
                }
                else
                {
                    trackID = $"MANUAL_LIBRO_VENTAS_{DateTime.Now:yyyyMMddHHmmss}";
                    _logger.LogWarning("Modo CertificacionManual: Libro generado pero NO enviado al SII");
                }

                return Ok(new
                {
                    exito = true,
                    mensaje = ambiente == "CertificacionManual" 
                        ? "Libro de Ventas generado (NO enviado - Modo Manual)" 
                        : "Libro de Ventas enviado exitosamente",
                    periodoTributario,
                    trackID,
                    xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar Libro de Ventas");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al procesar Libro de Ventas",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Genera y envía Libro de Compras al SII
        /// </summary>
        /// <param name="periodoTributario">Período en formato AAAA-MM (ej: 2026-01)</param>
        [HttpPost("compras")]
        public async Task<IActionResult> GenerarLibroCompras([FromQuery] string periodoTributario)
        {
            try
            {
                _logger.LogInformation("Generando Libro de Compras para período {Periodo}", periodoTributario);

                // Validar formato de período
                if (string.IsNullOrEmpty(periodoTributario) || periodoTributario.Length != 7)
                {
                    return BadRequest(new
                    {
                        exito = false,
                        mensaje = "Formato de período inválido. Use AAAA-MM (ej: 2026-01)"
                    });
                }

                // Generar libro
                var xmlLibro = await _libroService.GenerarLibroComprasAsync(periodoTributario);

                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Mock";
                string? trackID = null;

                // Enviar al SII (solo si NO es modo CertificacionManual)
                if (ambiente != "CertificacionManual")
                {
                    try
                    {
                        var semilla = await _siiService.ObtenerSemillaAsync();
                        var token = await _siiService.ObtenerTokenAsync(semilla);
                        var envioResponse = await _siiService.EnviarLibroAsync(xmlLibro, token);

                        if (!envioResponse.Exito)
                        {
                            return Ok(new
                            {
                                exito = false,
                                mensaje = "Error al enviar Libro de Compras al SII",
                                errores = envioResponse.Errores,
                                xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                            });
                        }

                        trackID = envioResponse.TrackID;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar Libro de Compras al SII");
                        return Ok(new
                        {
                            exito = false,
                            mensaje = "Error al enviar al SII",
                            error = ex.Message,
                            xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                        });
                    }
                }
                else
                {
                    trackID = $"MANUAL_LIBRO_COMPRAS_{DateTime.Now:yyyyMMddHHmmss}";
                    _logger.LogWarning("Modo CertificacionManual: Libro generado pero NO enviado al SII");
                }

                return Ok(new
                {
                    exito = true,
                    mensaje = ambiente == "CertificacionManual" 
                        ? "Libro de Compras generado (NO enviado - Modo Manual)" 
                        : "Libro de Compras enviado exitosamente",
                    periodoTributario,
                    trackID,
                    xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar Libro de Compras");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al procesar Libro de Compras",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Genera Libro de Boletas para un período tributario
        /// </summary>
        /// <param name="periodoTributario">Período en formato AAAA-MM (ej: 2026-01)</param>
        [HttpPost("boletas")]
        public async Task<IActionResult> GenerarLibroBoletas([FromQuery] string periodoTributario)
        {
            try
            {
                _logger.LogInformation("Generando Libro de Boletas para período {Periodo}", periodoTributario);

                // Validar formato de período
                if (string.IsNullOrEmpty(periodoTributario) || periodoTributario.Length != 7)
                {
                    return BadRequest(new
                    {
                        exito = false,
                        mensaje = "Formato de período inválido. Use AAAA-MM (ej: 2026-01)"
                    });
                }

                // Generar libro
                var xmlLibro = await _libroService.GenerarLibroBoletasAsync(periodoTributario);

                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Mock";
                string? trackID = null;

                // Enviar al SII (solo si NO es modo CertificacionManual)
                if (ambiente != "CertificacionManual")
                {
                    try
                    {
                        var semilla = await _siiService.ObtenerSemillaAsync();
                        var token = await _siiService.ObtenerTokenAsync(semilla);
                        var envioResponse = await _siiService.EnviarLibroAsync(xmlLibro, token);

                        if (!envioResponse.Exito)
                        {
                            return Ok(new
                            {
                                exito = false,
                                mensaje = "Error al enviar Libro de Boletas al SII",
                                errores = envioResponse.Errores,
                                xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                            });
                        }

                        trackID = envioResponse.TrackID;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar Libro de Boletas al SII");
                        return Ok(new
                        {
                            exito = false,
                            mensaje = "Error al enviar al SII",
                            error = ex.Message,
                            xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                        });
                    }
                }
                else
                {
                    trackID = $"MANUAL_LIBRO_BOLETAS_{DateTime.Now:yyyyMMddHHmmss}";
                    _logger.LogWarning("Modo CertificacionManual: Libro generado pero NO enviado al SII");
                }

                return Ok(new
                {
                    exito = true,
                    mensaje = ambiente == "CertificacionManual" 
                        ? "Libro de Boletas generado (NO enviado - Modo Manual)" 
                        : "Libro de Boletas enviado exitosamente",
                    periodoTributario,
                    trackID,
                    xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlLibro))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar Libro de Boletas");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al procesar Libro de Boletas",
                    error = ex.Message
                });
            }
        }
    }
}