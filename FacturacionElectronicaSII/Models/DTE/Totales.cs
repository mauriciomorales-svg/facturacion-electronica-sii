namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Totales del DTE
    /// </summary>
    public class Totales
    {
        public int MntNeto { get; set; }
        public int? MntExe { get; set; }  // Monto exento
        public int IVA { get; set; }
        public int MntTotal { get; set; }
        public int? MntDsctoGlobal { get; set; }  // Monto descuento global
    }
}
