namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Datos del receptor del DTE
    /// </summary>
    public class Receptor
    {
        public string RUTRecep { get; set; } = null!;
        public string RznSocRecep { get; set; } = null!;
        public string? GiroRecep { get; set; }
        public string DirRecep { get; set; } = null!;
        public string CmnaRecep { get; set; } = null!;
        public string CiudadRecep { get; set; } = null!;
    }
}
