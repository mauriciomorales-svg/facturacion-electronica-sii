namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio OPCIONAL para validaciones adicionales
    /// NO interfiere con el flujo normal - solo para verificaciones extra
    /// </summary>
    public interface IValidacionService
    {
        /// <summary>
        /// Valida un XML contra su Schema XSD del SII (opcional - para debugging)
        /// </summary>
        /// <param name="xmlContent">Contenido del XML a validar</param>
        /// <param name="schemaPath">Ruta al archivo .xsd del SII</param>
        /// <returns>Lista de errores (vacía si es válido)</returns>
        Task<List<string>> ValidarContraSchemaAsync(string xmlContent, string schemaPath);

        /// <summary>
        /// Verifica que un archivo CAF esté correctamente firmado (opcional - para debugging)
        /// </summary>
        /// <param name="xmlCAF">Contenido XML del CAF</param>
        /// <returns>true si la firma es válida, false si no</returns>
        Task<bool> VerificarFirmaCAFAsync(string xmlCAF);
    }
}
