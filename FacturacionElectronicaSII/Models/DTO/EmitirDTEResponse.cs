namespace FacturacionElectronicaSII.Models.DTO
{
    /// <summary>
    /// Response de emisión de DTE
    /// </summary>
    public class EmitirDTEResponse
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        
        // Datos del documento
        public int TipoDTE { get; set; }
        public int Folio { get; set; }
        public DateTime FechaEmision { get; set; }
        
        // Totales
        public decimal MontoNeto { get; set; }
        public decimal IVA { get; set; }
        public decimal MontoTotal { get; set; }
        
        // SII
        public string? TrackID { get; set; }
        public string? EstadoSII { get; set; }
        
        // Para impresión
        public string? TimbreBase64 { get; set; }  // Imagen PDF417
        public string? XMLBase64 { get; set; }     // XML del DTE
        
        public List<string> Errores { get; set; } = new();
    }
}
