using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.DTO;
using FacturacionElectronicaSII.Models;
using FacturacionElectronicaSII.Models.SII;

namespace FacturacionElectronicaSII.Services.Mock
{
    /// <summary>
    /// Implementación Mock del servicio SII para desarrollo sin conexión real
    /// </summary>
    public class MockSIIService : ISIIService
    {
        private static int _trackIdCounter = 1000000;
        private readonly ILogger<MockSIIService> _logger;

        public MockSIIService(ILogger<MockSIIService> logger)
        {
            _logger = logger;
        }

        public Task<string> ObtenerSemillaAsync()
        {
            _logger.LogInformation("Mock: Obteniendo semilla del SII (simulado)");
            // Simular delay de red
            Thread.Sleep(100);
            return Task.FromResult(DatosPrueba.SemillaMock);
        }

        public Task<string> ObtenerTokenAsync(string semillaFirmada)
        {
            _logger.LogInformation("Mock: Obteniendo token del SII (simulado)");
            Thread.Sleep(100);
            return Task.FromResult(DatosPrueba.TokenMock);
        }

        public Task<EnvioResponse> EnviarDTEAsync(string xmlEnvioDTE, string token)
        {
            _logger.LogInformation("Mock: Enviando DTE al SII (simulado)");
            Thread.Sleep(200);

            // Simular respuesta exitosa del SII
            var response = new EnvioResponse
            {
                Exito = true,
                TrackID = (++_trackIdCounter).ToString(),
                Mensaje = "DOCUMENTO TRIBUTARIO ELECTRONICO RECIBIDO",
                FechaRecepcion = DateTime.Now
            };

            _logger.LogInformation("Mock: DTE enviado exitosamente. TrackID: {TrackID}", response.TrackID);
            return Task.FromResult(response);
        }

        public Task<EnvioResponse> EnviarLibroAsync(string xmlLibro, string token)
        {
            _logger.LogInformation("Mock: Enviando Libro de Compras/Ventas al SII (simulado)");
            Thread.Sleep(200);

            // Simular respuesta exitosa del SII
            var response = new EnvioResponse
            {
                Exito = true,
                TrackID = (++_trackIdCounter).ToString(),
                Mensaje = "LIBRO ELECTRONICO RECIBIDO",
                FechaRecepcion = DateTime.Now
            };

            _logger.LogInformation("Mock: Libro enviado exitosamente. TrackID: {TrackID}", response.TrackID);
            return Task.FromResult(response);
        }

        public async Task<string> ObtenerTokenAsync()
        {
            var semilla = await ObtenerSemillaAsync();
            return await ObtenerTokenAsync(semilla);
        }

        public Task<EnvioResponse> EnviarRCOFAsync(string xmlRCOF, string token)
        {
            _logger.LogInformation("Mock: Enviando RCOF al SII (simulado)");
            Thread.Sleep(200);

            // Simular respuesta exitosa del SII
            var response = new EnvioResponse
            {
                Exito = true,
                TrackID = (++_trackIdCounter).ToString(),
                Mensaje = "REPORTE DE CONSUMO DE FOLIOS RECIBIDO",
                FechaRecepcion = DateTime.Now
            };

            _logger.LogInformation("Mock: RCOF enviado exitosamente. TrackID: {TrackID}", response.TrackID);
            return Task.FromResult(response);
        }

        public Task<EstadoEnvioResponse> ConsultarEstadoEnvioAsync(string trackId, string token)
        {
            _logger.LogInformation("Mock: Consultando estado del envío {TrackID} (simulado)", trackId);
            Thread.Sleep(100);

            // Simular documento aceptado
            var response = new EstadoEnvioResponse
            {
                TrackID = trackId,
                Estado = "EPR",  // Envío Procesado
                GlosaEstado = "Envio Procesado",
                Aceptados = 1,
                Rechazados = 0,
                Reparos = 0,
                FechaConsulta = DateTime.Now
            };

            _logger.LogInformation("Mock: Estado consultado. Estado: {Estado}", response.Estado);
            return Task.FromResult(response);
        }
    }
}
