namespace FacturacionElectronicaSII.Models.DTO
{
    /// <summary>
    /// Response de consulta de estado de envío al SII
    /// </summary>
    public class EstadoEnvioResponse
    {
        public string TrackID { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string GlosaEstado { get; set; } = string.Empty;
        public int Aceptados { get; set; }
        public int Rechazados { get; set; }
        public int Reparos { get; set; }
        public DateTime? FechaConsulta { get; set; }
    }
}
