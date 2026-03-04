namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para generación de representación impresa (PDF) de DTEs
    /// Según protocolo del SII
    /// </summary>
    public interface IPDFService
    {
        /// <summary>
        /// Genera PDF de un DTE con el timbre PDF417
        /// </summary>
        /// <param name="xmlDTE">XML del DTE firmado</param>
        /// <param name="folderOutput">Carpeta donde guardar el PDF (opcional)</param>
        /// <returns>Ruta del PDF generado y bytes del PDF</returns>
        Task<(string rutaPDF, byte[] pdfBytes)> GenerarPDFAsync(string xmlDTE, string? folderOutput = null);

        /// <summary>
        /// Genera código de barras PDF417 del TED (Timbre Electrónico Digital)
        /// </summary>
        /// <param name="tedXML">XML del TED</param>
        /// <returns>Bytes de la imagen PNG del código PDF417</returns>
        Task<byte[]> GenerarPDF417Async(string tedXML);
    }
}
