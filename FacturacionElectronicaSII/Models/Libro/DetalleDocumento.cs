namespace FacturacionElectronicaSII.Models.Libro
{
    /// <summary>
    /// Detalle de un documento para Libro de Ventas o Compras
    /// </summary>
    public class DetalleDocumento
    {
        /// <summary>
        /// Tipo de documento (33=Factura, 61=NC, 56=ND, etc.)
        /// </summary>
        public int TpoDoc { get; set; }

        /// <summary>
        /// Número de folio del documento
        /// </summary>
        public int Folio { get; set; }

        /// <summary>
        /// Fecha de emisión del documento (AAAA-MM-DD)
        /// </summary>
        public string FchDoc { get; set; } = string.Empty;

        /// <summary>
        /// RUT del receptor (para ventas) o emisor (para compras)
        /// </summary>
        public string RUTDoc { get; set; } = string.Empty;

        /// <summary>
        /// Razón social del receptor/emisor
        /// </summary>
        public string RznSoc { get; set; } = string.Empty;

        /// <summary>
        /// Monto Neto (afecto sin IVA)
        /// </summary>
        public int? MntNeto { get; set; }

        /// <summary>
        /// Monto Exento
        /// </summary>
        public int? MntExe { get; set; }

        /// <summary>
        /// Monto IVA
        /// </summary>
        public int? MntIVA { get; set; }

        /// <summary>
        /// Monto Total
        /// </summary>
        public int MntTotal { get; set; }

        /// <summary>
        /// Tasa de IVA (normalmente 19)
        /// </summary>
        public decimal? TasaIVA { get; set; }
    }
}
