namespace FacturacionElectronicaSII.Models.RCOF
{
    /// <summary>
    /// Modelo para el Reporte de Consumo de Folios (RCOF)
    /// Obligatorio para boletas electrónicas
    /// </summary>
    public class ConsumoFolios
    {
        public CaratulaRCOF Caratula { get; set; } = new CaratulaRCOF();
        public List<ResumenRCOF> Resumen { get; set; } = new List<ResumenRCOF>();
    }

    public class CaratulaRCOF
    {
        public string RutEmisor { get; set; } = string.Empty;
        public string RutEnvia { get; set; } = string.Empty;
        public string FchResol { get; set; } = string.Empty;
        public string NroResol { get; set; } = string.Empty;
        public string FchInicio { get; set; } = string.Empty;
        public string FchFinal { get; set; } = string.Empty;
        public int SecEnvio { get; set; }
        public string TmstFirmaEnv { get; set; } = string.Empty;
    }

    public class ResumenRCOF
    {
        public int TipoDocumento { get; set; }
        public int MntNeto { get; set; }
        public int MntIva { get; set; }
        public decimal TasaIVA { get; set; }
        public int MntExento { get; set; }
        public int MntTotal { get; set; }
        public int FoliosEmitidos { get; set; }
        public int FoliosAnulados { get; set; }
        public int FoliosUtilizados { get; set; }
        public List<RangoFoliosRCOF> RangoUtilizados { get; set; } = new List<RangoFoliosRCOF>();
    }

    public class RangoFoliosRCOF
    {
        public int Inicial { get; set; }
        public int Final { get; set; }
    }
}
