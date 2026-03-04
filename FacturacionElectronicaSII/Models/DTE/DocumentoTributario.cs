namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Representa un Documento Tributario Electrónico completo
    /// </summary>
    public class DocumentoTributario
    {
        public int TipoDTE { get; set; }
        public int Folio { get; set; }
        public DateTime FechaEmision { get; set; }
        public Encabezado Encabezado { get; set; } = null!;
        public List<Detalle> Detalles { get; set; } = new();
        public List<DescuentoGlobal>? DescuentosGlobales { get; set; }
        public Totales Totales { get; set; } = null!;
        public List<Referencia>? Referencias { get; set; }
    }

    /// <summary>
    /// Descuento o recargo global
    /// </summary>
    public class DescuentoGlobal
    {
        public int NroLinDR { get; set; }
        public string TpoMov { get; set; } = "D";  // D=Descuento, R=Recargo
        public string? GlosaDR { get; set; }
        public string TpoValor { get; set; } = "%";  // %=Porcentaje, $=Monto
        public decimal ValorDR { get; set; }
        public int? IndExeDR { get; set; }  // 1=Exento, null=Afecto
    }
}
