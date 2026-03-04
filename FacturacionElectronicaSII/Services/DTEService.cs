using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models;
using FacturacionElectronicaSII.Models.DTO;
using FacturacionElectronicaSII.Models.DTE;
using System.Text;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio principal para emisión de DTEs
    /// </summary>
    public class DTEService : IDTEService
    {
        private readonly ICAFService _cafService;
        private readonly ITEDService _tedService;
        private readonly IXMLBuilderService _xmlBuilderService;
        private readonly IFirmaService _firmaService;
        private readonly ISIIService _siiService;
        private readonly ILogger<DTEService> _logger;
        private readonly IConfiguration _configuration;

        public DTEService(
            ICAFService cafService,
            ITEDService tedService,
            IXMLBuilderService xmlBuilderService,
            IFirmaService firmaService,
            ISIIService siiService,
            ILogger<DTEService> logger,
            IConfiguration configuration)
        {
            _cafService = cafService;
            _tedService = tedService;
            _xmlBuilderService = xmlBuilderService;
            _firmaService = firmaService;
            _siiService = siiService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<EmitirDTEResponse> EmitirDocumentoAsync(EmitirDTERequest request)
        {
            _logger.LogInformation("Iniciando emisión de DTE tipo {TipoDTE}", request.TipoDTE);

            var response = new EmitirDTEResponse
            {
                TipoDTE = request.TipoDTE,
                Exito = false
            };

            try
            {
                // 1. Validar request
                var errores = ValidarRequest(request);
                if (errores.Any())
                {
                    response.Errores = errores;
                    response.Mensaje = "Errores de validación";
                    return response;
                }

                // 2. Obtener folio disponible
                var folio = await _cafService.ObtenerFolioDisponibleAsync(request.TipoDTE);
                response.Folio = folio;
                _logger.LogInformation("Folio obtenido: {Folio}", folio);

                // 3. Obtener CAF
                var caf = await _cafService.ObtenerCAFAsync(request.TipoDTE);
                if (caf == null)
                {
                    response.Errores.Add($"No hay CAF disponible para tipo DTE {request.TipoDTE}");
                    response.Mensaje = "Error al obtener CAF";
                    return response;
                }

                // 4. Calcular totales
                var totales = CalcularTotales(request.Detalles, request.DescuentoGlobalPorcentaje);
                response.MontoNeto = totales.MntNeto;
                response.IVA = totales.IVA;
                response.MontoTotal = totales.MntTotal;

                // 5. Construir documento tributario
                var documento = ConstruirDocumentoTributario(request, folio, totales);

                // 6. Generar TED
                var ted = _tedService.GenerarTED(documento, caf);
                _logger.LogInformation("TED generado exitosamente");

                // 7. Construir XML del DTE
                var xmlDTE = _xmlBuilderService.ConstruirXMLDTE(documento, ted);
                _logger.LogInformation("XML DTE construido");

                // 8. Firmar DTE (ID debe ser "F{folio}T{tipoDTE}" como en código funcional)
                var documentoId = $"F{folio}T{request.TipoDTE}";
                var xmlDTEFirmado = _firmaService.FirmarDTE(xmlDTE, documentoId);
                _logger.LogInformation("DTE firmado con ID: {DocumentoId}", documentoId);

                // 9. Construir EnvioDTE
                var rutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"] ?? DatosPrueba.RutEmisor;
                var rutEnvia = _configuration["FacturacionElectronica:Certificado:RUT"] ?? rutEmisor; // RUT del representante
                var rutReceptor = _configuration["SII:Certificacion:RutReceptor"] ?? DatosPrueba.RutReceptorSII;
                // Usar fecha de resolución de configuración
                string fechaResol = _configuration["FacturacionElectronica:Resolucion:Fecha"] ?? "2014-08-22";
                string nroResol = _configuration["FacturacionElectronica:Resolucion:Numero"] ?? "80";
                var xmlEnvioDTE = _xmlBuilderService.ConstruirXMLEnvioDTE(xmlDTEFirmado, rutEmisor, rutEnvia, rutReceptor, fechaResol, nroResol, 1);
                _logger.LogInformation("XML EnvioDTE construido (RutEnvia={RutEnvia}, FchResol={FchResol}, NroResol={NroResol})", rutEnvia, fechaResol, nroResol);

                // 10. Firmar EnvioDTE
                var xmlEnvioDTEFirmado = _firmaService.FirmarEnvioDTE(xmlEnvioDTE);
                _logger.LogInformation("EnvioDTE firmado");

                // 11. Guardar XML localmente ANTES de enviar (para certificación manual)
                var ambiente = _configuration["FacturacionElectronica:Ambiente"] ?? "Mock";
                var rutasXmls = _configuration["FacturacionElectronica:Rutas:XMLs"] ?? "./Data/XMLs";
                var carpetaXmls = Path.Combine(Directory.GetCurrentDirectory(), rutasXmls);
                Directory.CreateDirectory(carpetaXmls);
                var timestampArchivo = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var nombreArchivo = $"EnvioDTE_{timestampArchivo}.xml";
                var rutaArchivo = Path.Combine(carpetaXmls, nombreArchivo);
                await File.WriteAllTextAsync(rutaArchivo, xmlEnvioDTEFirmado, new UTF8Encoding(false));
                _logger.LogInformation("XML guardado en: {Ruta}", rutaArchivo);

                string? trackID = null;
                
                // 12. Enviar al SII (SOLO si NO estamos en CertificacionManual)
                if (ambiente != "CertificacionManual")
                {
                    var semilla = await _siiService.ObtenerSemillaAsync();
                    // La semilla se firma dentro del SIIService
                    var token = await _siiService.ObtenerTokenAsync(semilla);
                    var envioResponse = await _siiService.EnviarDTEAsync(xmlEnvioDTEFirmado, token);

                    if (!envioResponse.Exito)
                    {
                        response.Errores.AddRange(envioResponse.Errores);
                        response.Mensaje = "Error al enviar al SII";
                        return response;
                    }
                    
                    trackID = envioResponse.TrackID;
                }
                else
                {
                    _logger.LogWarning("Modo CertificacionManual: XML generado pero NO enviado al SII");
                    trackID = $"MANUAL_{timestampArchivo}"; // TrackID local para modo manual
                }

                // 13. Marcar folio como usado
                await _cafService.MarcarFolioUsadoAsync(request.TipoDTE, folio);

                // 14. Preparar respuesta
                response.Exito = true;
                response.Mensaje = ambiente == "CertificacionManual" 
                    ? "DTE generado exitosamente (NO enviado al SII - Modo Manual)" 
                    : "DTE emitido exitosamente";
                response.FechaEmision = documento.FechaEmision;
                response.TrackID = trackID;
                response.EstadoSII = ambiente == "CertificacionManual" ? "Generado localmente" : "Enviado";
                response.XMLBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlDTEFirmado));
                // TODO: Generar timbre PDF417 en base64
                response.TimbreBase64 = "MOCK_TIMBRE_BASE64";

                _logger.LogInformation("DTE emitido exitosamente. Folio: {Folio}, TrackID: {TrackID}", folio, trackID);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al emitir DTE");
                response.Errores.Add($"Error interno: {ex.Message}");
                response.Mensaje = "Error al procesar la emisión";
                return response;
            }
        }

        public async Task<EmitirSetResponse> EmitirSetAsync(List<EmitirDTERequest> requests)
        {
            var response = new EmitirSetResponse();
            var documentosFirmados = new List<(string xmlDTE, int tipoDTE)>();

            try
            {
                _logger.LogInformation("Iniciando emisión de SET con {Count} DTEs", requests.Count);

                var rutEmisor = _configuration["FacturacionElectronica:Emisor:RUT"] ?? "";
                var rutEnvia = _configuration["FacturacionElectronica:Certificado:RUT"] ?? rutEmisor;
                var rutReceptor = _configuration["SII:Certificacion:RutReceptor"] ?? "60803000-K";
                var fechaResol = _configuration["FacturacionElectronica:Resolucion:Fecha"] ?? "2025-12-03";
                var nroResol = _configuration["FacturacionElectronica:Resolucion:Numero"] ?? "0";

                // Generar cada DTE individualmente (sin enviar al SII)
                foreach (var request in requests)
                {
                    var errores = ValidarRequest(request);
                    if (errores.Any())
                    {
                        response.Errores.AddRange(errores);
                        response.Mensaje = "Errores de validación";
                        return response;
                    }

                    var folio = await _cafService.ObtenerFolioDisponibleAsync(request.TipoDTE);
                    var caf = await _cafService.ObtenerCAFAsync(request.TipoDTE);
                    if (caf == null) { response.Errores.Add($"No hay CAF para tipo {request.TipoDTE}"); return response; }

                    var totales = CalcularTotales(request.Detalles, request.DescuentoGlobalPorcentaje);
                    var documento = ConstruirDocumentoTributario(request, folio, totales);
                    var ted = _tedService.GenerarTED(documento, caf);
                    var xmlDTE = _xmlBuilderService.ConstruirXMLDTE(documento, ted);
                    var xmlDTEFirmado = _firmaService.FirmarDTE(xmlDTE, $"F{folio}T{request.TipoDTE}");

                    documentosFirmados.Add((xmlDTEFirmado, request.TipoDTE));
                    response.Folios.Add($"T{request.TipoDTE}F{folio}");
                    await _cafService.MarcarFolioUsadoAsync(request.TipoDTE, folio);
                    _logger.LogInformation("DTE tipo {Tipo} folio {Folio} generado", request.TipoDTE, folio);
                }

                // Construir UN solo EnvioDTE con todos los DTEs
                var xmlEnvioDTE = _xmlBuilderService.ConstruirXMLEnvioDTEMultiple(documentosFirmados, rutEmisor, rutEnvia, rutReceptor, fechaResol, nroResol);
                var xmlEnvioDTEFirmado = _firmaService.FirmarEnvioDTE(xmlEnvioDTE);

                // Guardar XML del set
                var carpetaXmls = Path.Combine(Directory.GetCurrentDirectory(), _configuration["FacturacionElectronica:Rutas:XMLs"] ?? "./Data/XMLs");
                Directory.CreateDirectory(carpetaXmls);
                var rutaArchivo = Path.Combine(carpetaXmls, $"SetBasico_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
                await File.WriteAllTextAsync(rutaArchivo, xmlEnvioDTEFirmado, new System.Text.UTF8Encoding(false));

                // Enviar al SII
                var semilla = await _siiService.ObtenerSemillaAsync();
                var token = await _siiService.ObtenerTokenAsync(semilla);
                var envioResponse = await _siiService.EnviarDTEAsync(xmlEnvioDTEFirmado, token);

                if (!envioResponse.Exito)
                {
                    response.Errores.AddRange(envioResponse.Errores);
                    response.Mensaje = "Error al enviar al SII: " + envioResponse.Mensaje;
                    return response;
                }

                response.Exito = true;
                response.TrackID = envioResponse.TrackID;
                response.CantidadDTEs = documentosFirmados.Count;
                response.Mensaje = $"SET enviado exitosamente. TrackID: {envioResponse.TrackID}";
                _logger.LogInformation("SET enviado. TrackID: {TrackID}, DTEs: {Count}", envioResponse.TrackID, documentosFirmados.Count);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al emitir SET");
                response.Errores.Add($"Error interno: {ex.Message}");
                response.Mensaje = "Error al procesar el SET";
                return response;
            }
        }

        public async Task<EstadoEnvioResponse> ConsultarEstadoAsync(string trackId)
        {
            _logger.LogInformation("Consultando estado para TrackID: {TrackID}", trackId);

            try
            {
                var semilla = await _siiService.ObtenerSemillaAsync();
                var token = await _siiService.ObtenerTokenAsync(semilla);
                return await _siiService.ConsultarEstadoEnvioAsync(trackId, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar estado");
                throw;
            }
        }

        public Task<byte[]?> GenerarPDFAsync(int tipoDTE, int folio)
        {
            _logger.LogInformation("Generación de PDF no implementada aún para DTE {TipoDTE} folio {Folio}", tipoDTE, folio);
            // TODO: Implementar generación de PDF
            return Task.FromResult<byte[]?>(null);
        }

        private List<string> ValidarRequest(EmitirDTERequest request)
        {
            var errores = new List<string>();

            if (request.TipoDTE != 33 && request.TipoDTE != 39 && request.TipoDTE != 61 && request.TipoDTE != 56)
            {
                errores.Add("Tipo DTE inválido. Debe ser 33 (Factura), 39 (Boleta), 61 (NC) o 56 (ND)");
            }

            if (request.Receptor == null)
            {
                errores.Add("Receptor es requerido");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.Receptor.RUT))
                    errores.Add("RUT del receptor es requerido");
                if (string.IsNullOrWhiteSpace(request.Receptor.RazonSocial))
                    errores.Add("Razón social del receptor es requerida");
            }

            var esNCND = request.TipoDTE == 61 || request.TipoDTE == 56;
            var tieneReferencias = esNCND && request.Referencias != null && request.Referencias.Any();
            if ((request.Detalles == null || !request.Detalles.Any()) && !tieneReferencias)
            {
                errores.Add("Debe haber al menos un detalle");
            }
            else
            {
                foreach (var detalle in request.Detalles)
                {
                    if (string.IsNullOrWhiteSpace(detalle.Nombre))
                        errores.Add("Nombre del producto es requerido");
                    if (detalle.Cantidad <= 0)
                        errores.Add("La cantidad debe ser mayor a cero");
                    if (detalle.PrecioUnitario <= 0)
                        errores.Add("El precio unitario debe ser mayor a cero");
                }
            }

            return errores;
        }

        private Totales CalcularTotales(List<DetalleDTO> detalles, decimal? descuentoGlobalPorcentaje = null)
        {
            decimal mntAfecto = 0;
            decimal mntExento = 0;

            foreach (var detalle in detalles)
            {
                var precio = detalle.PrecioUnitario;
                var cantidad = detalle.Cantidad;
                var subtotal = precio * cantidad;

                if (detalle.DescuentoPorcentaje.HasValue && detalle.DescuentoPorcentaje.Value > 0)
                {
                    subtotal = subtotal * (1 - detalle.DescuentoPorcentaje.Value / 100);
                }

                if (detalle.Exento)
                {
                    mntExento += subtotal;
                }
                else
                {
                    mntAfecto += subtotal;
                }
            }

            // Aplicar descuento global solo a items afectos
            decimal mntDsctoGlobal = 0;
            if (descuentoGlobalPorcentaje.HasValue && descuentoGlobalPorcentaje.Value > 0)
            {
                mntDsctoGlobal = mntAfecto * descuentoGlobalPorcentaje.Value / 100;
                mntAfecto = mntAfecto - mntDsctoGlobal;
            }

            // Redondear a entero
            var mntNetoInt = (int)Math.Round(mntAfecto, 0);
            var mntExeInt = mntExento > 0 ? (int?)Math.Round(mntExento, 0) : null;
            var iva = (int)Math.Round(mntNetoInt * 0.19m, 0);
            var mntTotal = mntNetoInt + (mntExeInt ?? 0) + iva;

            return new Totales
            {
                MntNeto = mntNetoInt,
                MntExe = mntExeInt,
                IVA = iva,
                MntTotal = mntTotal,
                MntDsctoGlobal = mntDsctoGlobal > 0 ? (int?)Math.Round(mntDsctoGlobal, 0) : null
            };
        }

        private DocumentoTributario ConstruirDocumentoTributario(
            EmitirDTERequest request,
            int folio,
            Totales totales)
        {
            var config = _configuration.GetSection("FacturacionElectronica");
            var emisorConfig = config.GetSection("Emisor");

            var emisor = new Emisor
            {
                RUTEmisor = emisorConfig["RUT"] ?? DatosPrueba.RutEmisor,
                RznSoc = emisorConfig["RazonSocial"] ?? DatosPrueba.RazonSocialEmisor,
                GiroEmis = emisorConfig["Giro"] ?? DatosPrueba.GiroEmisor,
                Acteco = emisorConfig["Acteco"] ?? DatosPrueba.Acteco,
                DirOrigen = emisorConfig["Direccion"] ?? DatosPrueba.DireccionEmisor,
                CmnaOrigen = emisorConfig["Comuna"] ?? DatosPrueba.ComunaEmisor,
                CiudadOrigen = emisorConfig["Ciudad"] ?? DatosPrueba.CiudadEmisor
            };

            var receptor = new Receptor
            {
                RUTRecep = request.Receptor.RUT,
                RznSocRecep = request.Receptor.RazonSocial,
                GiroRecep = request.Receptor.Giro,
                DirRecep = request.Receptor.Direccion,
                CmnaRecep = request.Receptor.Comuna,
                CiudadRecep = request.Receptor.Ciudad
            };

            var encabezado = new Encabezado
            {
                Emisor = emisor,
                Receptor = receptor,
                FormaPago = request.FormaPago
            };

            var detalles = new List<Detalle>();
            for (int i = 0; i < request.Detalles.Count; i++)
            {
                var detalleDTO = request.Detalles[i];
                var subtotal = detalleDTO.PrecioUnitario * detalleDTO.Cantidad;
                var descuento = detalleDTO.DescuentoPorcentaje.HasValue
                    ? subtotal * detalleDTO.DescuentoPorcentaje.Value / 100
                    : 0;
                var montoItem = subtotal - descuento;

                detalles.Add(new Detalle
                {
                    NroLinDet = i + 1,
                    IndExe = detalleDTO.Exento ? 1 : null,  // 1=Exento, null=Afecto
                    Codigo = detalleDTO.Codigo,
                    Nombre = detalleDTO.Nombre,
                    Cantidad = detalleDTO.Cantidad,
                    Unidad = "UN",
                    PrecioUnitario = detalleDTO.PrecioUnitario,
                    DescuentoPct = detalleDTO.DescuentoPorcentaje.HasValue
                        ? (int)Math.Round(detalleDTO.DescuentoPorcentaje.Value, 0)
                        : null,
                    DescuentoMonto = descuento > 0 ? (int)Math.Round(descuento, 0) : null,
                    MontoItem = (int)Math.Round(montoItem, 0)
                });
            }

            // NC/ND sin ítems: agregar detalle sintético (SII exige Detalle antes de Referencias)
            var esNCND = request.TipoDTE == 61 || request.TipoDTE == 56;
            if (esNCND && !detalles.Any() && request.Referencias != null && request.Referencias.Any())
            {
                var razonRef = request.Referencias.FirstOrDefault()?.Razon ?? "CORRIGE TEXTO";
                detalles.Add(new Detalle
                {
                    NroLinDet = 1,
                    IndExe = 1,
                    Nombre = razonRef.Length > 80 ? razonRef.Substring(0, 80) : razonRef,
                    Cantidad = 1,
                    Unidad = "UN",
                    PrecioUnitario = 1,
                    MontoItem = 1
                });
            }

            var referencias = request.Referencias?.Select((r, i) => new Referencia
            {
                NroLinRef = i + 1,
                TpoDocRef = r.TipoDTE,
                FolioRef = r.Folio,
                FchaRef = r.Fecha,
                CodRef = r.CodigoReferencia,
                RazonRef = r.Razon
            }).ToList();

            // Crear descuento global si aplica
            List<DescuentoGlobal>? descuentosGlobales = null;
            if (request.DescuentoGlobalPorcentaje.HasValue && request.DescuentoGlobalPorcentaje.Value > 0)
            {
                descuentosGlobales = new List<DescuentoGlobal>
                {
                    new DescuentoGlobal
                    {
                        NroLinDR = 1,
                        TpoMov = "D",
                        GlosaDR = "Descuento Global",
                        TpoValor = "%",
                        ValorDR = request.DescuentoGlobalPorcentaje.Value
                    }
                };
            }

            return new DocumentoTributario
            {
                TipoDTE = request.TipoDTE,
                Folio = folio,
                FechaEmision = DateTime.Now,
                Encabezado = encabezado,
                Detalles = detalles,
                DescuentosGlobales = descuentosGlobales,
                Totales = totales,
                Referencias = referencias
            };
        }

    }
}
