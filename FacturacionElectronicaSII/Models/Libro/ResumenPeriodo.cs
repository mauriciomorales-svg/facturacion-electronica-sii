namespace FacturacionElectronicaSII.Models.Libro
{
    /// <summary>
    /// Resumen de un período para Libro de Compras o Ventas
    /// </summary>
    public class ResumenPeriodo
    {
        /// <summary>
        /// Tipo de documento (33=Factura, 61=NC, 56=ND, etc.)
        /// </summary>
        public int TpoDoc { get; set; }

        /// <summary>
        /// Número total de documentos del tipo
        /// </summary>
        public int TotDoc { get; set; }

        /// <summary>
        /// Total Neto (monto afecto sin IVA)
        /// </summary>
        public int? TotMntNeto { get; set; }

        /// <summary>
        /// Total Exento
        /// </summary>
        public int? TotMntExe { get; set; }

        /// <summary>
        /// Total IVA
        /// </summary>
        public int? TotMntIVA { get; set; }

        /// <summary>
        /// Monto Total (Neto + Exento + IVA)
        /// </summary>
        public int TotMntTotal { get; set; }
    }
}
