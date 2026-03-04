using FacturacionElectronicaSII.Models.Libro;

namespace FacturacionElectronicaSII.Interfaces
{
    /// <summary>
    /// Servicio para generación de Libros de Compra, Venta y Boletas
    /// </summary>
    public interface ILibroService
    {
        /// <summary>
        /// Genera XML del Libro de Ventas para un período
        /// </summary>
        /// <param name="periodoTributario">Período en formato AAAA-MM</param>
        /// <returns>XML firmado del libro</returns>
        Task<string> GenerarLibroVentasAsync(string periodoTributario);

        /// <summary>
        /// Genera XML del Libro de Compras para un período
        /// </summary>
        /// <param name="periodoTributario">Período en formato AAAA-MM</param>
        /// <returns>XML firmado del libro</returns>
        Task<string> GenerarLibroComprasAsync(string periodoTributario);

        /// <summary>
        /// Genera XML del Libro de Boletas para un período
        /// </summary>
        /// <param name="periodoTributario">Período en formato AAAA-MM</param>
        /// <returns>XML firmado del libro</returns>
        Task<string> GenerarLibroBoletasAsync(string periodoTributario);

        /// <summary>
        /// Construye el objeto LibroCompraVenta para ventas basado en documentos emitidos
        /// </summary>
        Task<LibroCompraVenta> ConstruirLibroVentasAsync(string periodoTributario);

        /// <summary>
        /// Construye el objeto LibroCompraVenta para compras
        /// </summary>
        Task<LibroCompraVenta> ConstruirLibroComprasAsync(string periodoTributario);

        /// <summary>
        /// Construye el objeto LibroCompraVenta para boletas
        /// </summary>
        Task<LibroCompraVenta> ConstruirLibroBoletasAsync(string periodoTributario);

        /// <summary>
        /// Construye XML del Libro
        /// </summary>
        string ConstruirXMLLibro(LibroCompraVenta libro);
    }
}
