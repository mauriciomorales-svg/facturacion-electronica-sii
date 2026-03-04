namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Detalle de línea del DTE
    /// </summary>
    public class Detalle
    {
        public int NroLinDet { get; set; }
        public int? IndExe { get; set; }  // 1=Exento, null=Afecto
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = null!;
        public decimal Cantidad { get; set; }
        public string Unidad { get; set; } = "UN";
        public decimal PrecioUnitario { get; set; }
        public decimal? DescuentoMonto { get; set; }
        public decimal? DescuentoPct { get; set; }
        public decimal MontoItem { get; set; }
    }
}
