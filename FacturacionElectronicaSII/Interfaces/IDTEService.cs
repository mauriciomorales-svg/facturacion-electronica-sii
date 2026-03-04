using FacturacionElectronicaSII.Models.DTO;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio principal para emisión de DTEs
    /// </summary>
    public interface IDTEService
    {
        Task<EmitirDTEResponse> EmitirDocumentoAsync(EmitirDTERequest request);
        Task<EmitirSetResponse> EmitirSetAsync(List<EmitirDTERequest> requests);
        Task<EstadoEnvioResponse> ConsultarEstadoAsync(string trackId);
        Task<byte[]?> GenerarPDFAsync(int tipoDTE, int folio);
    }
}
