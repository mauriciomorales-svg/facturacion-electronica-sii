using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.DTO;
using Microsoft.AspNetCore.Mvc;

namespace FacturacionElectronicaSII.Controllers
{
    /// <summary>
    /// Controlador para consulta de estados de envíos al SII
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EstadoController : ControllerBase
    {
        private readonly IDTEService _dteService;
        private readonly ILogger<EstadoController> _logger;

        public EstadoController(IDTEService dteService, ILogger<EstadoController> logger)
        {
            _dteService = dteService;
            _logger = logger;
        }

        /// <summary>
        /// Consulta el estado de un envío al SII por TrackID
        /// </summary>
        /// <param name="trackId">TrackID del envío</param>
        /// <returns>Estado del envío</returns>
        [HttpGet("{trackId}")]
        [ProducesResponseType(typeof(EstadoEnvioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EstadoEnvioResponse>> ConsultarEstado(string trackId)
        {
            _logger.LogInformation("Consultando estado para TrackID: {TrackID}", trackId);

            if (string.IsNullOrWhiteSpace(trackId))
            {
                return BadRequest("TrackID es requerido");
            }

            try
            {
                var estado = await _dteService.ConsultarEstadoAsync(trackId);
                return Ok(estado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar estado");
                return StatusCode(500, new { error = "Error al consultar estado", mensaje = ex.Message });
            }
        }
    }
}
