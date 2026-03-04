using FacturacionElectronicaSII.Models.CAF;
using FacturacionElectronicaSII.Models.DTE;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para generación del Timbre Electrónico (TED)
    /// </summary>
    public interface ITEDService
    {
        string GenerarTED(DocumentoTributario documento, CAFData caf);
        string GenerarDD(DocumentoTributario documento, CAFData caf);
        string FirmarDD(string dd, string clavePrivadaPEM);
    }
}
