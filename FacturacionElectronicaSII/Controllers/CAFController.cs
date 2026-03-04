using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;

namespace FacturacionElectronicaSII.Controllers
{
    /// <summary>
    /// Controlador para gestión de CAFs
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CAFController : ControllerBase
    {
        private readonly ICAFService _cafService;
        private readonly ILogger<CAFController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISIIService _siiService;

        public CAFController(ICAFService cafService, ILogger<CAFController> logger, IConfiguration configuration, ISIIService siiService)
        {
            _cafService = cafService;
            _logger = logger;
            _configuration = configuration;
            _siiService = siiService;
        }

        /// <summary>
        /// Diagnóstico de conexión con el SII
        /// </summary>
        [HttpGet("diagnostico-sii")]
        public async Task<ActionResult> DiagnosticoSII()
        {
            var resultado = new
            {
                timestamp = DateTime.Now,
                configuracion = new
                {
                    rutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"],
                    razonSocial = _configuration["FacturacionElectronica:Emisor:RazonSocial"],
                    certificado = _configuration["FacturacionElectronica:Certificado:Nombre"],
                    ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Certificacion",
                    urlSemilla = _configuration["FacturacionElectronica:Ambiente"] == "Produccion"
                        ? _configuration["SII:Produccion:UrlSemilla"] ?? "https://palena.sii.cl/DTEWS/CrSeed.jws"
                        : _configuration["SII:Certificacion:UrlSemilla"] ?? "https://maullin.sii.cl/DTEWS/CrSeed.jws",
                    urlUpload = _configuration["FacturacionElectronica:Ambiente"] == "Produccion"
                        ? _configuration["SII:Produccion:UrlUpload"] ?? "https://palena.sii.cl/cgi_dte/UPL/DTEUpload"
                        : _configuration["SII:Certificacion:UrlUpload"] ?? "https://maullin.sii.cl/cgi_dte/UPL/DTEUpload"
                },
                pruebas = new List<object>()
            };

            try
            {
                // Prueba 1: Obtener semilla
                try
                {
                    var semilla = await _siiService.ObtenerSemillaAsync();
                    resultado.pruebas.Add(new { 
                        prueba = "Obtener Semilla", 
                        exito = true, 
                        mensaje = "Semilla obtenida correctamente", 
                        semillaPreview = semilla.Length > 50 ? semilla.Substring(0, 50) + "..." : semilla 
                    });
                }
                catch (Exception ex)
                {
                    resultado.pruebas.Add(new { 
                        prueba = "Obtener Semilla", 
                        exito = false, 
                        mensaje = ex.Message,
                        tipoError = ex.GetType().Name
                    });
                }

                // Prueba 2: Obtener token (requiere certificado)
                try
                {
                    var semilla = await _siiService.ObtenerSemillaAsync();
                    var token = await _siiService.ObtenerTokenAsync(semilla);
                    resultado.pruebas.Add(new { 
                        prueba = "Obtener Token", 
                        exito = true, 
                        mensaje = "Token obtenido correctamente. El certificado es válido y está autorizado para autenticarse con el SII.",
                        tokenPreview = token.Length > 50 ? token.Substring(0, 50) + "..." : token 
                    });
                }
                catch (Exception ex)
                {
                    resultado.pruebas.Add(new { 
                        prueba = "Obtener Token", 
                        exito = false, 
                        mensaje = ex.Message,
                        tipoError = ex.GetType().Name,
                        nota = "Si falla aquí, puede ser problema con el certificado o su instalación"
                    });
                }

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error en diagnóstico", mensaje = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene la cantidad de folios disponibles para un tipo de DTE
        /// </summary>
        /// <param name="tipoDTE">Tipo de DTE (33, 39, 61, 56)</param>
        /// <returns>Cantidad de folios disponibles</returns>
        [HttpGet("folios-disponibles/{tipoDTE}")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        public async Task<ActionResult<int>> FoliosDisponibles(int tipoDTE)
        {
            _logger.LogInformation("Consultando folios disponibles para tipo DTE {TipoDTE}", tipoDTE);
            var disponibles = await _cafService.FoliosDisponiblesAsync(tipoDTE);
            return Ok(disponibles);
        }

        /// <summary>
        /// Obtiene información del CAF para un tipo de DTE
        /// </summary>
        /// <param name="tipoDTE">Tipo de DTE</param>
        /// <returns>Información del CAF</returns>
        [HttpGet("{tipoDTE}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ObtenerCAF(int tipoDTE)
        {
            _logger.LogInformation("Obteniendo CAF para tipo DTE {TipoDTE}", tipoDTE);
            var caf = await _cafService.ObtenerCAFAsync(tipoDTE);

            if (caf == null)
            {
                return NotFound($"No hay CAF disponible para tipo DTE {tipoDTE}");
            }

            // No retornar la clave privada por seguridad
            return Ok(new
            {
                caf.RutEmisor,
                caf.RazonSocial,
                caf.TipoDTE,
                caf.FolioInicial,
                caf.FolioFinal,
                caf.FechaAutorizacion
            });
        }

        /// <summary>
        /// Endpoint temporal de diagnóstico para verificar el contenido del CAF en la BD
        /// </summary>
        [HttpGet("diagnostico/{tipoDTE}")]
        public async Task<ActionResult> DiagnosticoCAF(int tipoDTE)
        {
            _logger.LogInformation("Diagnóstico del CAF para tipo DTE {TipoDTE}", tipoDTE);
            
            try
            {
                using var connection = new MySqlConnector.MySqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        ID,
                        TD,
                        Estado,
                        LENGTH(CAFContenido) as Longitud,
                        SUBSTRING(CAFContenido, 1, 300) as Inicio,
                        SUBSTRING(CAFContenido, GREATEST(1, LENGTH(CAFContenido) - 200), 200) as Final,
                        CASE 
                            WHEN CAFContenido LIKE '%<td%' THEN 'TIENE <td>'
                            WHEN CAFContenido LIKE '%<hr%' THEN 'TIENE <hr>'
                            WHEN CAFContenido LIKE '%<html%' THEN 'TIENE <html>'
                            WHEN CAFContenido LIKE '%<AUTORIZACION%' THEN 'OK - TIENE AUTORIZACION'
                            ELSE 'REVISAR'
                        END as EstadoHTML,
                        CAFContenido as ContenidoCompleto
                    FROM CAF 
                    WHERE TD = @TipoDTE
                    ORDER BY FechaCarga DESC";

                using var command = new MySqlConnector.MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@TipoDTE", tipoDTE);

                var registros = new List<object>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var contenido = reader.GetString("ContenidoCompleto");
                    var tieneTd = contenido.Contains("<td");
                    var tieneHr = contenido.Contains("<hr");
                    var posTd = tieneTd ? contenido.IndexOf("<td") : -1;
                    var posHr = tieneHr ? contenido.IndexOf("<hr") : -1;

                    registros.Add(new
                    {
                        ID = reader.GetInt32("ID"),
                        TD = reader.GetInt32("TD"),
                        Estado = reader.GetString("Estado"),
                        Longitud = reader.GetInt64("Longitud"),
                        EstadoHTML = reader.GetString("EstadoHTML"),
                        Inicio = reader.GetString("Inicio"),
                        Final = reader.GetString("Final"),
                        TieneTD = tieneTd,
                        TieneHR = tieneHr,
                        PosicionTD = posTd,
                        PosicionHR = posHr,
                        ContenidoCompleto = contenido
                    });
                }

                return Ok(new
                {
                    TotalRegistros = registros.Count,
                    Registros = registros
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Verifica el CAF actual: estructura XML, rango de folios y validaciones
        /// </summary>
        /// <param name="tipoDTE">Tipo de DTE (33, 39, 61, 56)</param>
        /// <returns>Información detallada del CAF y validaciones</returns>
        [HttpGet("verificar/{tipoDTE}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> VerificarCAF(int tipoDTE)
        {
            _logger.LogInformation("Verificando CAF para tipo DTE {TipoDTE}", tipoDTE);
            
            try
            {
                var caf = await _cafService.ObtenerCAFAsync(tipoDTE);
                
                if (caf == null)
                {
                    return NotFound(new { 
                        Error = $"No hay CAF disponible para tipo DTE {tipoDTE}",
                        TipoDTE = tipoDTE
                    });
                }

                var verificaciones = new List<object>();
                var errores = new List<string>();
                var advertencias = new List<string>();

                // 1. Verificar estructura básica
                verificaciones.Add(new { 
                    Categoria = "Estructura Básica",
                    Item = "RUT Emisor",
                    Estado = string.IsNullOrEmpty(caf.RutEmisor) ? "❌ Faltante" : "✅ OK",
                    Valor = caf.RutEmisor ?? "N/A"
                });

                verificaciones.Add(new { 
                    Categoria = "Estructura Básica",
                    Item = "Razón Social",
                    Estado = string.IsNullOrEmpty(caf.RazonSocial) ? "⚠️ Faltante" : "✅ OK",
                    Valor = caf.RazonSocial ?? "N/A"
                });

                verificaciones.Add(new { 
                    Categoria = "Estructura Básica",
                    Item = "Tipo DTE",
                    Estado = caf.TipoDTE == tipoDTE ? "✅ OK" : "❌ No coincide",
                    Valor = caf.TipoDTE.ToString()
                });

                // 2. Verificar rango de folios
                verificaciones.Add(new { 
                    Categoria = "Rango de Folios",
                    Item = "Folio Inicial",
                    Estado = caf.FolioInicial > 0 ? "✅ OK" : "❌ Inválido",
                    Valor = caf.FolioInicial.ToString()
                });

                verificaciones.Add(new { 
                    Categoria = "Rango de Folios",
                    Item = "Folio Final",
                    Estado = caf.FolioFinal > 0 ? "✅ OK" : "❌ Inválido",
                    Valor = caf.FolioFinal.ToString()
                });

                verificaciones.Add(new { 
                    Categoria = "Rango de Folios",
                    Item = "Rango Válido",
                    Estado = caf.FolioFinal >= caf.FolioInicial ? "✅ OK" : "❌ Inválido",
                    Valor = $"{caf.FolioInicial} - {caf.FolioFinal}",
                    TotalFolios = caf.FolioFinal - caf.FolioInicial + 1
                });

                // 3. Validar XML usando ValidadorRangoFolios
                try
                {
                    var (desde, hasta, tipoDoc) = ValidadorRangoFolios.ObtenerRangoCAF(caf.XMLOriginal);
                    
                    verificaciones.Add(new { 
                        Categoria = "Validación XML",
                        Item = "Rango desde XML",
                        Estado = desde == caf.FolioInicial && hasta == caf.FolioFinal ? "✅ Coincide" : "⚠️ Diferencia",
                        Valor = $"XML: {desde}-{hasta}, BD: {caf.FolioInicial}-{caf.FolioFinal}",
                        TipoDocumentoXML = tipoDoc
                    });

                    // Validar algunos folios de ejemplo
                    var foliosPrueba = new[] 
                    { 
                        caf.FolioInicial, 
                        caf.FolioInicial + 1,
                        (caf.FolioInicial + caf.FolioFinal) / 2,
                        caf.FolioFinal - 1,
                        caf.FolioFinal 
                    };

                    var validacionesFolios = new List<object>();
                    bool todosValidos = true;
                    foreach (var folio in foliosPrueba)
                    {
                        bool esValido = ValidadorRangoFolios.EsFolioValido(caf.XMLOriginal, folio, tipoDTE);
                        if (!esValido) todosValidos = false;
                        validacionesFolios.Add(new
                        {
                            Folio = folio,
                            Estado = esValido ? "✅ Válido" : "❌ Inválido",
                            DentroRango = esValido
                        });
                    }

                    verificaciones.Add(new { 
                        Categoria = "Validación XML",
                        Item = "Validación de Folios",
                        Estado = todosValidos ? "✅ OK" : "❌ Error",
                        Detalles = validacionesFolios
                    });
                }
                catch (Exception ex)
                {
                    errores.Add($"Error al validar XML: {ex.Message}");
                    verificaciones.Add(new { 
                        Categoria = "Validación XML",
                        Item = "Validación de Rango",
                        Estado = "❌ Error",
                        Error = ex.Message
                    });
                }

                // 4. Verificar estructura XML
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.PreserveWhitespace = true;
                    xmlDoc.LoadXml(caf.XMLOriginal);

                    var tieneCAF = xmlDoc.SelectSingleNode("//CAF") != null;
                    var tieneDA = xmlDoc.SelectSingleNode("//DA") != null;
                    var tieneRNG = xmlDoc.SelectSingleNode("//RNG") != null;
                    var tieneRSASK = xmlDoc.SelectSingleNode("//RSASK") != null;

                    verificaciones.Add(new { 
                        Categoria = "Estructura XML",
                        Item = "Nodo CAF",
                        Estado = tieneCAF ? "✅ Presente" : "❌ Faltante"
                    });

                    verificaciones.Add(new { 
                        Categoria = "Estructura XML",
                        Item = "Nodo DA",
                        Estado = tieneDA ? "✅ Presente" : "❌ Faltante"
                    });

                    verificaciones.Add(new { 
                        Categoria = "Estructura XML",
                        Item = "Nodo RNG",
                        Estado = tieneRNG ? "✅ Presente" : "❌ Faltante"
                    });

                    verificaciones.Add(new { 
                        Categoria = "Estructura XML",
                        Item = "Nodo RSASK (Clave Privada)",
                        Estado = tieneRSASK ? "✅ Presente" : "❌ Faltante",
                        Advertencia = tieneRSASK ? null : "La clave privada es necesaria para firmar el TED"
                    });

                    if (tieneRSASK)
                    {
                        var rsaskNode = xmlDoc.SelectSingleNode("//RSASK");
                        var tieneClave = !string.IsNullOrWhiteSpace(rsaskNode?.InnerText);
                        verificaciones.Add(new { 
                            Categoria = "Estructura XML",
                            Item = "Contenido RSASK",
                            Estado = tieneClave ? "✅ Tiene contenido" : "⚠️ Vacío",
                            Longitud = rsaskNode?.InnerText?.Length ?? 0
                        });
                    }
                }
                catch (XmlException xmlEx)
                {
                    errores.Add($"XML inválido: {xmlEx.Message}");
                    verificaciones.Add(new { 
                        Categoria = "Estructura XML",
                        Item = "Validez XML",
                        Estado = "❌ Inválido",
                        Error = xmlEx.Message,
                        Linea = xmlEx.LineNumber,
                        Posicion = xmlEx.LinePosition
                    });
                }

                // 5. Verificar fecha de autorización
                verificaciones.Add(new { 
                    Categoria = "Fechas",
                    Item = "Fecha Autorización",
                    Estado = caf.FechaAutorizacion != default ? "✅ OK" : "⚠️ No especificada",
                    Valor = caf.FechaAutorizacion.ToString("yyyy-MM-dd HH:mm:ss")
                });

                var diasDesdeAutorizacion = (DateTime.Now - caf.FechaAutorizacion).Days;
                if (diasDesdeAutorizacion > 365)
                {
                    advertencias.Add($"El CAF tiene más de un año de antigüedad ({diasDesdeAutorizacion} días)");
                }

                // 6. Verificar folios disponibles
                var foliosDisponibles = await _cafService.FoliosDisponiblesAsync(tipoDTE);
                verificaciones.Add(new { 
                    Categoria = "Folios",
                    Item = "Folios Disponibles",
                    Estado = foliosDisponibles > 0 ? "✅ OK" : "⚠️ Sin folios disponibles",
                    Valor = foliosDisponibles,
                    TotalFolios = caf.FolioFinal - caf.FolioInicial + 1,
                    FoliosUsados = (caf.FolioFinal - caf.FolioInicial + 1) - foliosDisponibles
                });

                return Ok(new
                {
                    TipoDTE = tipoDTE,
                    Resumen = new
                    {
                        RutEmisor = caf.RutEmisor,
                        RazonSocial = caf.RazonSocial,
                        FolioInicial = caf.FolioInicial,
                        FolioFinal = caf.FolioFinal,
                        TotalFolios = caf.FolioFinal - caf.FolioInicial + 1,
                        FoliosDisponibles = foliosDisponibles,
                        FechaAutorizacion = caf.FechaAutorizacion,
                        DiasDesdeAutorizacion = diasDesdeAutorizacion
                    },
                    Verificaciones = verificaciones,
                    Errores = errores,
                    Advertencias = advertencias,
                    EstadoGeneral = errores.Count == 0 ? (advertencias.Count == 0 ? "✅ OK" : "⚠️ Con advertencias") : "❌ Con errores"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar CAF");
                return StatusCode(500, new { 
                    Error = ex.Message, 
                    StackTrace = ex.StackTrace,
                    TipoDTE = tipoDTE
                });
            }
        }

        /// <summary>
        /// Endpoint temporal para recrear la tabla CAF limpia e insertar el nuevo CAF
        /// ⚠️ SOLO PARA DESARROLLO - ELIMINAR EN PRODUCCIÓN
        /// </summary>
        [HttpPost("recrear-tabla")]
        public async Task<ActionResult> RecrearTablaCAF()
        {
            _logger.LogWarning("⚠️ RECREANDO TABLA CAF - SOLO PARA DESARROLLO");
            
            try
            {
                using var connection = new MySqlConnector.MySqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();

                // Leer el script SQL
                string scriptPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "CREAR_TABLA_CAF_LIMPIA.sql");
                if (!System.IO.File.Exists(scriptPath))
                {
                    return NotFound(new { Error = $"No se encontró el archivo: {scriptPath}" });
                }

                string sqlScript = await System.IO.File.ReadAllTextAsync(scriptPath);
                
                // Dividir en comandos (separados por ;)
                string[] commands = sqlScript.Split(';', StringSplitOptions.RemoveEmptyEntries);
                
                var resultados = new List<object>();
                
                foreach (string cmd in commands)
                {
                    string command = cmd.Trim();
                    if (string.IsNullOrEmpty(command) || command.StartsWith("--"))
                        continue;
                    
                    try
                    {
                        using var mysqlCommand = new MySqlConnector.MySqlCommand(command, connection);
                        int rowsAffected = await mysqlCommand.ExecuteNonQueryAsync();
                        resultados.Add(new { 
                            Comando = command.Substring(0, Math.Min(50, command.Length)) + "...",
                            FilasAfectadas = rowsAffected,
                            Estado = "OK"
                        });
                    }
                    catch (Exception ex)
                    {
                        resultados.Add(new { 
                            Comando = command.Substring(0, Math.Min(50, command.Length)) + "...",
                            Error = ex.Message,
                            Estado = "Error"
                        });
                    }
                }

                // Verificar que se insertó correctamente
                using var verifyCommand = new MySqlConnector.MySqlCommand(
                    "SELECT ID, TD, RangoInicio, RangoFin, FechaCarga, Estado FROM CAF WHERE TD = 33", 
                    connection);
                using var reader = await verifyCommand.ExecuteReaderAsync();
                
                var cafs = new List<object>();
                while (await reader.ReadAsync())
                {
                    cafs.Add(new
                    {
                        ID = reader.GetInt32("ID"),
                        TipoDTE = reader.GetInt32("TD"),
                        RangoInicio = reader.GetInt32("RangoInicio"),
                        RangoFin = reader.GetInt32("RangoFin"),
                        FechaCarga = reader.GetDateTime("FechaCarga"),
                        Estado = reader.GetString("Estado")
                    });
                }

                return Ok(new
                {
                    Mensaje = "Tabla CAF recreada e insertado nuevo CAF",
                    ComandosEjecutados = resultados.Count,
                    Resultados = resultados,
                    CAFsInsertados = cafs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recrear tabla CAF");
                return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }
    }
}
