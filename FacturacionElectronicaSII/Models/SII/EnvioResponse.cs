namespace FacturacionElectronicaSII.Models.SII
{
    /// <summary>
    /// Response del SII al enviar un DTE
    /// </summary>
    public class EnvioResponse
    {
        public bool Exito { get; set; }
        public string TrackID { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime? FechaRecepcion { get; set; }
        public List<string> Errores { get; set; } = new();
    }
}
