using FacturacionElectronicaSII.Models.DTO;
using FacturacionElectronicaSII.Models.SII;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para comunicación con el SII
    /// </summary>
    public interface ISIIService
    {
        Task<string> ObtenerSemillaAsync();
        Task<string> ObtenerTokenAsync(string semilla);
        Task<string> ObtenerTokenAsync();
        Task<EnvioResponse> EnviarDTEAsync(string xmlEnvioDTE, string token);
        Task<EnvioResponse> EnviarLibroAsync(string xmlLibro, string token);
        Task<EnvioResponse> EnviarRCOFAsync(string xmlRCOF, string token);
        Task<EstadoEnvioResponse> ConsultarEstadoEnvioAsync(string trackId, string token);
    }
}
