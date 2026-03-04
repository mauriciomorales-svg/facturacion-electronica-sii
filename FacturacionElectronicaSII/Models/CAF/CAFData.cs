namespace FacturacionElectronicaSII.Models.CAF
{
    /// <summary>
    /// Datos del CAF (Código de Autorización de Folios)
    /// </summary>
    public class CAFData
    {
        public string RutEmisor { get; set; } = null!;
        public string RazonSocial { get; set; } = null!;
        public int TipoDTE { get; set; }
        public int FolioInicial { get; set; }
        public int FolioFinal { get; set; }
        public DateTime FechaAutorizacion { get; set; }
        public string ModuloRSA { get; set; } = null!;
        public string ExponenteRSA { get; set; } = null!;
        public int IdK { get; set; }
        public string ClavePrivadaPEM { get; set; } = null!;
        public string XMLOriginal { get; set; } = null!;
    }
}
