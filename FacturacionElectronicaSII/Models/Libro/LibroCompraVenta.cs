namespace FacturacionElectronicaSII.Models.Libro
{
    /// <summary>
    /// Libro de Compras o Ventas
    /// </summary>
    public class LibroCompraVenta
    {
        /// <summary>
        /// Carátula del libro
        /// </summary>
        public CaratulaLibro Caratula { get; set; } = new CaratulaLibro();

        /// <summary>
        /// Lista de resúmenes por tipo de documento
        /// </summary>
        public List<ResumenPeriodo> ResumenPeriodo { get; set; } = new List<ResumenPeriodo>();

        /// <summary>
        /// Lista de documentos detallados
        /// </summary>
        public List<DetalleDocumento> Detalle { get; set; } = new List<DetalleDocumento>();

        /// <summary>
        /// Totales finales del período
        /// </summary>
        public TotalesPeriodo TotalesPeriodo { get; set; } = new TotalesPeriodo();
    }

    /// <summary>
    /// Carátula del Libro
    /// </summary>
    public class CaratulaLibro
    {
        /// <summary>
        /// RUT del contribuyente emisor
        /// </summary>
        public string RutEmisorLibro { get; set; } = string.Empty;

        /// <summary>
        /// Razón social del emisor
        /// </summary>
        public string RznSoc { get; set; } = string.Empty;

        /// <summary>
        /// Período del libro (AAAA-MM)
        /// </summary>
        public string PeriodoTributario { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de resolución autorización SII (AAAA-MM-DD)
        /// </summary>
        public string FchResol { get; set; } = string.Empty;

        /// <summary>
        /// Número de resolución autorización SII
        /// </summary>
        public int NroResol { get; set; }

        /// <summary>
        /// Tipo de libro: COMPRA o VENTA
        /// </summary>
        public string TipoLibro { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de envío: PARCIAL, FINAL, TOTAL, etc.
        /// </summary>
        public string TipoEnvio { get; set; } = "TOTAL";

        /// <summary>
        /// Fecha de resolución (timestamp)
        /// </summary>
        public string TmstFirmaEnv { get; set; } = string.Empty;
    }

    /// <summary>
    /// Totales finales del período
    /// </summary>
    public class TotalesPeriodo
    {
        /// <summary>
        /// Total de documentos en el período
        /// </summary>
        public int TotDoc { get; set; }

        /// <summary>
        /// Total Monto Neto
        /// </summary>
        public int? TotMntNeto { get; set; }

        /// <summary>
        /// Total Monto Exento
        /// </summary>
        public int? TotMntExe { get; set; }

        /// <summary>
        /// Total IVA
        /// </summary>
        public int? TotMntIVA { get; set; }

        /// <summary>
        /// Total general
        /// </summary>
        public int TotMntTotal { get; set; }
    }
}
