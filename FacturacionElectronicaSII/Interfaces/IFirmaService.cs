namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para firma digital de documentos
    /// </summary>
    public interface IFirmaService
    {
        string FirmarDTE(string xmlDTE, string idDocumento);
        string FirmarEnvioDTE(string xmlEnvioDTE);
        string FirmarLibro(string xmlLibro);
        string FirmarRCOF(string xmlRCOF);
        bool ValidarFirma(string xmlFirmado);
    }
}
