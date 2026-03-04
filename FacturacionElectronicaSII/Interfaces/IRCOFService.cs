using FacturacionElectronicaSII.Models.RCOF;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para generación de RCOF (Reporte de Consumo de Folios)
    /// Obligatorio para boletas electrónicas
    /// </summary>
    public interface IRCOFService
    {
        /// <summary>
        /// Genera RCOF para un período específico
        /// </summary>
        /// <param name="fechaInicio">Fecha inicio del período (AAAA-MM-DD)</param>
        /// <param name="fechaFinal">Fecha final del período (AAAA-MM-DD)</param>
        /// <returns>Ruta del XML generado</returns>
        Task<string> GenerarRCOFAsync(string fechaInicio, string fechaFinal);

        /// <summary>
        /// Construye objeto RCOF desde DTEs emitidos
        /// </summary>
        Task<ConsumoFolios> ConstruirRCOFAsync(string fechaInicio, string fechaFinal);

        /// <summary>
        /// Construye XML del RCOF
        /// </summary>
        string ConstruirXMLRCOF(ConsumoFolios rcof);
    }
}
