using FacturacionElectronicaSII.Models.CAF;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para gestión de CAFs y folios
    /// </summary>
    public interface ICAFService
    {
        Task<int> ObtenerFolioDisponibleAsync(int tipoDTE);
        Task<CAFData?> ObtenerCAFAsync(int tipoDTE);
        Task<bool> MarcarFolioUsadoAsync(int tipoDTE, int folio);
        Task<int> FoliosDisponiblesAsync(int tipoDTE);
    }
}
