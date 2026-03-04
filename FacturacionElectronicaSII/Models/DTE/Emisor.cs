namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Datos del emisor del DTE
    /// </summary>
    public class Emisor
    {
        public string RUTEmisor { get; set; } = null!;
        public string RznSoc { get; set; } = null!;
        public string GiroEmis { get; set; } = null!;
        public string Acteco { get; set; } = null!;
        public string DirOrigen { get; set; } = null!;
        public string CmnaOrigen { get; set; } = null!;
        public string CiudadOrigen { get; set; } = null!;
    }
}
