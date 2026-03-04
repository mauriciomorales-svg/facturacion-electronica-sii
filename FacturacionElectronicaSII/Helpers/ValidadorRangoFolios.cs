/*
 * ============================================================================
 * VALIDADOR DE RANGO DE FOLIOS CAF
 * ============================================================================
 * 
 * Valida que un folio esté dentro del rango autorizado por el CAF.
 * 
 * FECHA: 2025-01-XX
 * OBJETIVO: Validar folios antes de timbrar para evitar errores del SII
 * 
 * ============================================================================
 */

using System;
using System.Xml;

namespace FacturacionElectronicaSII.Helpers
{
    /// <summary>
    /// Valida que un folio esté dentro del rango autorizado por el CAF.
    /// </summary>
    public static class ValidadorRangoFolios
    {
        /// <summary>
        /// Valida si un folio está dentro del rango autorizado por el CAF.
        /// </summary>
        /// <param name="cafXml">XML completo del CAF (incluyendo nodo AUTORIZACION)</param>
        /// <param name="folio">Folio que se desea validar</param>
        /// <param name="tipoDocumento">Tipo de documento (33 para Factura Electrónica)</param>
        /// <param name="rangoDesde">OUT: Folio desde (D) del CAF</param>
        /// <param name="rangoHasta">OUT: Folio hasta (H) del CAF</param>
        /// <returns>true si el folio está dentro del rango, false en caso contrario</returns>
        public static bool ValidarFolioEnRango(
            string cafXml,
            int folio,
            int tipoDocumento,
            out int rangoDesde,
            out int rangoHasta)
        {
            rangoDesde = -1;
            rangoHasta = -1;

            try
            {
                if (string.IsNullOrWhiteSpace(cafXml))
                {
                    throw new ArgumentException("El XML del CAF no puede estar vacío.", nameof(cafXml));
                }

                // Parsear el XML del CAF
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cafXml);

                // Buscar el nodo DA (Datos de Autorización)
                XmlNode? daNode = xmlDoc.SelectSingleNode("//DA");
                if (daNode == null)
                {
                    throw new XmlException("No se encontró el nodo <DA> en el XML del CAF.");
                }

                // Validar que el Tipo de Documento coincida
                XmlNode? tdNode = daNode.SelectSingleNode("TD");
                if (tdNode == null)
                {
                    throw new XmlException("No se encontró el nodo <TD> dentro de <DA>.");
                }

                int tdCAF = Convert.ToInt32(tdNode.InnerText);
                if (tdCAF != tipoDocumento)
                {
                    throw new ArgumentException(
                        $"El Tipo de Documento del CAF ({tdCAF}) no coincide con el solicitado ({tipoDocumento}).",
                        nameof(tipoDocumento));
                }

                // Buscar el nodo RNG (Rango)
                XmlNode? rngNode = daNode.SelectSingleNode("RNG");
                if (rngNode == null)
                {
                    throw new XmlException("No se encontró el nodo <RNG> dentro de <DA>.");
                }

                // Leer D (Desde) y H (Hasta)
                XmlNode? dNode = rngNode.SelectSingleNode("D");
                XmlNode? hNode = rngNode.SelectSingleNode("H");

                if (dNode == null || hNode == null)
                {
                    throw new XmlException("No se encontraron los nodos <D> o <H> dentro de <RNG>.");
                }

                rangoDesde = Convert.ToInt32(dNode.InnerText);
                rangoHasta = Convert.ToInt32(hNode.InnerText);

                // Validar que el rango sea válido
                if (rangoDesde < 1 || rangoHasta < rangoDesde)
                {
                    throw new XmlException(
                        $"El rango del CAF es inválido: Desde={rangoDesde}, Hasta={rangoHasta}");
                }

                // Validar que el folio esté dentro del rango
                bool esValido = folio >= rangoDesde && folio <= rangoHasta;

                return esValido;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ ERROR en ValidarFolioEnRango: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Versión simplificada que solo retorna true/false sin los valores de rango.
        /// </summary>
        public static bool EsFolioValido(string cafXml, int folio, int tipoDocumento)
        {
            return ValidarFolioEnRango(cafXml, folio, tipoDocumento, out _, out _);
        }

        /// <summary>
        /// Obtiene el rango de folios autorizado por el CAF.
        /// </summary>
        public static (int desde, int hasta, int tipoDocumento) ObtenerRangoCAF(string cafXml)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cafXml))
                {
                    throw new ArgumentException("El XML del CAF no puede estar vacío.", nameof(cafXml));
                }

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(cafXml);

                XmlNode? daNode = xmlDoc.SelectSingleNode("//DA");
                if (daNode == null)
                {
                    throw new XmlException("No se encontró el nodo <DA> en el XML del CAF.");
                }

                int tipoDocumento = Convert.ToInt32(daNode.SelectSingleNode("TD")?.InnerText ?? "0");
                XmlNode? rngNode = daNode.SelectSingleNode("RNG");
                if (rngNode == null)
                {
                    throw new XmlException("No se encontró el nodo <RNG> dentro de <DA>.");
                }

                int desde = Convert.ToInt32(rngNode.SelectSingleNode("D")?.InnerText ?? "0");
                int hasta = Convert.ToInt32(rngNode.SelectSingleNode("H")?.InnerText ?? "0");

                return (desde, hasta, tipoDocumento);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ ERROR en ObtenerRangoCAF: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
