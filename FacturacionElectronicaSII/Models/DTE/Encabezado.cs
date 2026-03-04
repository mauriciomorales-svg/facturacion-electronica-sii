namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Encabezado del DTE
    /// </summary>
    public class Encabezado
    {
        public Emisor Emisor { get; set; } = null!;
        public Receptor Receptor { get; set; } = null!;
        public int? FormaPago { get; set; }
    }

    public class IdDoc
    {
        public int TipoDTE { get; set; }
        public int Folio { get; set; }
        public DateTime FchEmis { get; set; }
        public int? FmaPago { get; set; }
    }
}
