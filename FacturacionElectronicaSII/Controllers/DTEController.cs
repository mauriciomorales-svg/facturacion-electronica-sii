using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text;
using System.Linq;

namespace FacturacionElectronicaSII.Controllers
{
    /// <summary>
    /// Controlador para emisión y gestión de DTEs
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DTEController : ControllerBase
    {
        private readonly IDTEService _dteService;
        private readonly ILogger<DTEController> _logger;

        public DTEController(IDTEService dteService, ILogger<DTEController> logger)
        {
            _dteService = dteService;
            _logger = logger;
        }

        /// <summary>
        /// FACTURA SIMPLE - Solo necesitas RUT, nombre del cliente y monto
        /// </summary>
        /// <remarks>
        /// Ejemplo de uso:
        ///
        ///     POST /api/dte/factura-simple
        ///     {
        ///         "rutCliente": "66666666-6",
        ///         "nombreCliente": "JUAN PEREZ",
        ///         "descripcion": "SERVICIO DE CONSULTORIA",
        ///         "montoNeto": 100000
        ///     }
        ///
        /// </remarks>
        [HttpPost("factura-simple")]
        [ProducesResponseType(typeof(EmitirDTEResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EmitirDTEResponse>> EmitirFacturaSimple([FromBody] FacturaSimpleRequest request)
        {
            var requestCompleto = new EmitirDTERequest
            {
                TipoDTE = 33,
                FormaPago = 1,
                Receptor = new ReceptorDTO
                {
                    RUT = request.RutCliente,
                    RazonSocial = request.NombreCliente,
                    Giro = request.Giro ?? "ACTIVIDADES VARIAS",
                    Direccion = request.Direccion ?? "DIRECCION",
                    Comuna = request.Comuna ?? "SANTIAGO",
                    Ciudad = request.Ciudad ?? "SANTIAGO"
                },
                Detalles = new List<DetalleDTO>
                {
                    new DetalleDTO
                    {
                        Nombre = request.Descripcion,
                        Cantidad = 1,
                        PrecioUnitario = request.MontoNeto
                    }
                }
            };

            var response = await _dteService.EmitirDocumentoAsync(requestCompleto);
            return response.Exito ? Ok(response) : BadRequest(response);
        }

        /// <summary>
        /// Emite un DTE completo (todos los campos disponibles)
        /// </summary>
        /// <remarks>
        /// Ejemplo mínimo para Factura (TipoDTE=33):
        ///
        ///     {
        ///         "tipoDTE": 33,
        ///         "receptor": {
        ///             "rut": "66666666-6",
        ///             "razonSocial": "EMPRESA PRUEBA",
        ///             "giro": "COMERCIO",
        ///             "direccion": "CALLE 123",
        ///             "comuna": "SANTIAGO",
        ///             "ciudad": "SANTIAGO"
        ///         },
        ///         "detalles": [{
        ///             "nombre": "PRODUCTO",
        ///             "cantidad": 1,
        ///             "precioUnitario": 10000
        ///         }]
        ///     }
        ///
        /// </remarks>
        [HttpPost("emitir")]
        [ProducesResponseType(typeof(EmitirDTEResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EmitirDTEResponse>> EmitirDTE([FromBody] EmitirDTERequest request)
        {
            _logger.LogInformation("Recibida solicitud de emisión de DTE tipo {TipoDTE}", request.TipoDTE);

            if (request == null)
            {
                return BadRequest("Request no puede ser nulo");
            }

            var response = await _dteService.EmitirDocumentoAsync(request);

            if (!response.Exito)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Emite un SET completo de DTEs en un único EnvioDTE al SII
        /// </summary>
        [HttpPost("emitir-set")]
        [ProducesResponseType(typeof(EmitirSetResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EmitirSetResponse>> EmitirSet([FromBody] List<EmitirDTERequest> requests)
        {
            if (requests == null || !requests.Any())
                return BadRequest("Se requiere al menos un DTE");

            var response = await _dteService.EmitirSetAsync(requests);

            if (!response.Exito)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Consulta el estado de un envío al SII
        /// </summary>
        /// <param name="trackId">TrackID del envío</param>
        /// <returns>Estado del envío</returns>
        [HttpGet("estado/{trackId}")]
        [ProducesResponseType(typeof(EstadoEnvioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EstadoEnvioResponse>> ConsultarEstado(string trackId)
        {
            _logger.LogInformation("Consultando estado para TrackID: {TrackID}", trackId);

            if (string.IsNullOrWhiteSpace(trackId))
            {
                return BadRequest("TrackID es requerido");
            }

            var estado = await _dteService.ConsultarEstadoAsync(trackId);
            return Ok(estado);
        }

        /// <summary>
        /// Genera el PDF de un DTE
        /// </summary>
        /// <param name="tipoDTE">Tipo de DTE</param>
        /// <param name="folio">Folio del DTE</param>
        /// <returns>PDF del DTE</returns>
        [HttpGet("pdf/{tipoDTE}/{folio}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GenerarPDF(int tipoDTE, int folio)
        {
            _logger.LogInformation("Generando PDF para DTE {TipoDTE} folio {Folio}", tipoDTE, folio);

            var pdf = await _dteService.GenerarPDFAsync(tipoDTE, folio);

            if (pdf == null)
            {
                return NotFound("PDF no disponible");
            }

            return File(pdf, "application/pdf", $"DTE_{tipoDTE}_{folio}.pdf");
        }

        /// <summary>
        /// Obtiene el último documento enviado al SII
        /// </summary>
        /// <returns>XML del último documento enviado</returns>
        [HttpGet("ultimo-documento-enviado")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult ObtenerUltimoDocumentoEnviado()
        {
            var documentosPath = Path.Combine(Directory.GetCurrentDirectory(), "DocumentosEnviadosSII");
            
            if (!Directory.Exists(documentosPath))
            {
                return NotFound(new { mensaje = "No hay documentos enviados aún" });
            }

            var archivos = Directory.GetFiles(documentosPath, "*.xml")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            if (archivos.Count == 0)
            {
                return NotFound(new { mensaje = "No hay documentos enviados aún" });
            }

            var ultimoArchivo = archivos[0];
            var contenido = System.IO.File.ReadAllText(ultimoArchivo.FullName, Encoding.GetEncoding("ISO-8859-1"));

            return Ok(new
            {
                nombreArchivo = ultimoArchivo.Name,
                fechaEnvio = ultimoArchivo.LastWriteTime,
                tamano = ultimoArchivo.Length,
                contenido = contenido
            });
        }
    }
}
