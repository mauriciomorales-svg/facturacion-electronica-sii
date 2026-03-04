namespace FacturacionElectronicaSII.Models.DTE
{
    /// <summary>
    /// Referencia a otro DTE (para NC/ND)
    /// </summary>
    public class Referencia
    {
        public int NroLinRef { get; set; }
        public int TpoDocRef { get; set; }
        public int FolioRef { get; set; }
        public DateTime FchaRef { get; set; }
        public int CodRef { get; set; }
        public string? RazonRef { get; set; }
    }
}
