using FacturacionElectronicaSII.Models.DTE;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para construcción de XMLs según formato SII
    /// </summary>
    public interface IXMLBuilderService
    {
        string ConstruirXMLDTE(DocumentoTributario documento, string ted);
        string ConstruirXMLEnvioDTE(string xmlDTE, string rutEmisor, string rutEnvia, string rutReceptor, string fechaResol = "2021-03-12", string nroResol = "0", int cantidadDTE = 1);
    }
}
