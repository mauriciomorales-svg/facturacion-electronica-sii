using FacturacionElectronicaSII.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FacturacionElectronicaSII.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RCOFController : ControllerBase
    {
        private readonly IRCOFService _rcofService;
        private readonly ISIIService _siiService;
        private readonly ILogger<RCOFController> _logger;
        private readonly IConfiguration _configuration;

        public RCOFController(
            IRCOFService rcofService,
            ISIIService siiService,
            ILogger<RCOFController> logger,
            IConfiguration configuration)
        {
            _rcofService = rcofService;
            _siiService = siiService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Genera RCOF (Reporte de Consumo de Folios) para un período
        /// </summary>
        /// <param name="fechaInicio">Fecha inicio (AAAA-MM-DD)</param>
        /// <param name="fechaFinal">Fecha final (AAAA-MM-DD)</param>
        /// <remarks>
        /// Ejemplo:
        /// 
        ///     POST /api/rcof/generar?fechaInicio=2026-01-01&fechaFinal=2026-01-31
        /// 
        /// </remarks>
        [HttpPost("generar")]
        public async Task<IActionResult> GenerarRCOF(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFinal)
        {
            try
            {
                _logger.LogInformation("Generando RCOF para período {Inicio} - {Final}", fechaInicio, fechaFinal);

                // Validar fechas
                if (!DateTime.TryParse(fechaInicio, out var inicio))
                {
                    return BadRequest(new
                    {
                        exito = false,
                        mensaje = "Fecha inicio inválida. Use formato AAAA-MM-DD"
                    });
                }

                if (!DateTime.TryParse(fechaFinal, out var final))
                {
                    return BadRequest(new
                    {
                        exito = false,
                        mensaje = "Fecha final inválida. Use formato AAAA-MM-DD"
                    });
                }

                if (inicio > final)
                {
                    return BadRequest(new
                    {
                        exito = false,
                        mensaje = "La fecha inicio no puede ser mayor que la fecha final"
                    });
                }

                // Generar RCOF
                var rutaXML = await _rcofService.GenerarRCOFAsync(fechaInicio, fechaFinal);

                // Verificar ambiente
                var ambiente = _configuration["FacturacionElectronica:Ambiente"];
                var enviadoSII = false;
                string? trackId = null;
                string mensaje = "RCOF generado exitosamente";

                // Si NO es CertificacionManual, enviar al SII
                if (ambiente != "CertificacionManual")
                {
                    try
                    {
                        var xmlContent = await System.IO.File.ReadAllTextAsync(rutaXML);
                        var token = await _siiService.ObtenerTokenAsync();
                        var response = await _siiService.EnviarRCOFAsync(xmlContent, token);

                        enviadoSII = true;
                        trackId = response.TrackID;
                        mensaje = "RCOF generado y enviado al SII";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo enviar RCOF al SII");
                        mensaje = "RCOF generado pero no enviado al SII";
                    }
                }
                else
                {
                    mensaje = "RCOF generado localmente (modo CertificacionManual)";
                }

                return Ok(new
                {
                    exito = true,
                    mensaje,
                    fechaInicio,
                    fechaFinal,
                    rutaXML,
                    enviadoSII,
                    trackId,
                    ambiente
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar RCOF");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al generar RCOF",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Construye vista previa del RCOF sin guardarlo
        /// </summary>
        [HttpGet("preview")]
        public async Task<IActionResult> PreviewRCOF(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFinal)
        {
            try
            {
                _logger.LogInformation("Generando preview de RCOF");

                var rcof = await _rcofService.ConstruirRCOFAsync(fechaInicio, fechaFinal);

                return Ok(new
                {
                    exito = true,
                    fechaInicio,
                    fechaFinal,
                    caratula = rcof.Caratula,
                    resumen = rcof.Resumen,
                    mensaje = "Preview del RCOF generado"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar preview de RCOF");
                return StatusCode(500, new
                {
                    exito = false,
                    mensaje = "Error al generar preview",
                    error = ex.Message
                });
            }
        }
    }
}
