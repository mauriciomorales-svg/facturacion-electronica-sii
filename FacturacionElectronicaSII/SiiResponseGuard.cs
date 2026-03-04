using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FacturacionElectronicaSII.Helpers;

namespace Comercial_isabel.FacturacionElectronica
{
    /// <summary>
    /// Clase para blindar métodos que procesan respuestas del SII contra HTML corrupto
    /// </summary>
    public static class SiiResponseGuard
    {
        private static string LogsDir => PathHelper.LogsDumpsDirectory;
        private static string DepuracionLog => PathHelper.DepuracionLogPath;

        /// <summary>
        /// Verifica si el string parece ser HTML revisando los primeros 1200 caracteres
        /// </summary>
        public static bool EsHtml(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return false;

            string sample = s.Length > 1200 ? s.Substring(0, 1200).ToLowerInvariant() : s.ToLowerInvariant();

            return sample.Contains("<html") ||
                   sample.Contains("<!doctype") ||
                   sample.Contains("<table") ||
                   sample.Contains("<td") ||
                   sample.Contains("<hr") ||
                   sample.Contains("text/html");
        }

        /// <summary>
        /// Si el contenido es HTML, guarda un dump y lanza una excepción clara
        /// </summary>
        public static void DumpYThrowSiHtml(string raw, string contexto)
        {
            if (!EsHtml(raw))
                return;

            string rutaCompleta = PathHelper.GetDumpPath(contexto);

            try
            {
                // Asegurar que el directorio existe
                if (!Directory.Exists(LogsDir))
                {
                    Directory.CreateDirectory(LogsDir);
                }

                // Guardar dump completo
                File.WriteAllText(rutaCompleta, raw, Encoding.UTF8);

                // Registrar en log de depuración
                string mensajeLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SiiResponseGuard] ❌ HTML detectado en {contexto}. Dump guardado en: {rutaCompleta}\n";
                File.AppendAllText(DepuracionLog, mensajeLog, Encoding.UTF8);

                throw new Exception($"El contenido recibido es HTML (página del SII), no XML válido. Contexto: {contexto}. Dump guardado en: {rutaCompleta}");
            }
            catch (DirectoryNotFoundException)
            {
                throw new Exception($"No se pudo crear el directorio de dumps: {LogsDir}. El contenido recibido es HTML, no XML válido. Contexto: {contexto}");
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("HTML")))
            {
                // Si falla el guardado pero ya sabemos que es HTML, lanzar excepción de HTML
                throw new Exception($"El contenido recibido es HTML (página del SII), no XML válido. Contexto: {contexto}. Error al guardar dump: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Normaliza el XML del CAF extrayendo solo el bloque válido
        /// </summary>
        public static string NormalizarCafXml(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new Exception("El contenido del CAF está vacío.");

            string rawTrimmed = raw.Trim();

            // Caso 1: Contiene bloque <AUTORIZACION> directo
            int inicioAutorizacion = rawTrimmed.IndexOf("<AUTORIZACION", StringComparison.OrdinalIgnoreCase);
            if (inicioAutorizacion >= 0)
            {
                int finAutorizacion = rawTrimmed.IndexOf("</AUTORIZACION>", inicioAutorizacion, StringComparison.OrdinalIgnoreCase);
                if (finAutorizacion >= 0)
                {
                    finAutorizacion += "</AUTORIZACION>".Length;
                    string bloque = rawTrimmed.Substring(inicioAutorizacion, finAutorizacion - inicioAutorizacion).Trim();
                    
                    // Registrar extracción exitosa
                    string logMsg1 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SiiResponseGuard] ✅ Bloque AUTORIZACION extraído (XML directo), longitud: {bloque.Length}\n";
                    File.AppendAllText(DepuracionLog, logMsg1, Encoding.UTF8);
                    
                    return bloque;
                }
            }

            // Caso 2: Contiene bloque &lt;AUTORIZACION&gt; escapado (HTML entities)
            int inicioAutorizacionEscapado = rawTrimmed.IndexOf("&lt;AUTORIZACION", StringComparison.OrdinalIgnoreCase);
            if (inicioAutorizacionEscapado >= 0)
            {
                int finAutorizacionEscapado = rawTrimmed.IndexOf("&lt;/AUTORIZACION&gt;", inicioAutorizacionEscapado, StringComparison.OrdinalIgnoreCase);
                if (finAutorizacionEscapado >= 0)
                {
                    finAutorizacionEscapado += "&lt;/AUTORIZACION&gt;".Length;
                    string bloqueEscapado = rawTrimmed.Substring(inicioAutorizacionEscapado, finAutorizacionEscapado - inicioAutorizacionEscapado).Trim();
                    
                    // Decodificar SOLO el bloque extraído
                    string bloqueDecodificado = WebUtility.HtmlDecode(bloqueEscapado);
                    
                    // Registrar extracción y decodificación
                    string logMsg2 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SiiResponseGuard] ✅ Bloque AUTORIZACION extraído (HTML escapado) y decodificado, longitud: {bloqueDecodificado.Length}\n";
                    File.AppendAllText(DepuracionLog, logMsg2, Encoding.UTF8);
                    
                    return bloqueDecodificado;
                }
            }

            // Caso 3: No hay AUTORIZACION pero sí contiene <CAF directamente
            if (rawTrimmed.IndexOf("<CAF", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Registrar que se devuelve raw tal cual
                string logMsg3 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SiiResponseGuard] ⚠️ No se encontró AUTORIZACION, pero contiene <CAF. Devolviendo raw tal cual, longitud: {rawTrimmed.Length}\n";
                File.AppendAllText(DepuracionLog, logMsg3, Encoding.UTF8);
                
                return rawTrimmed;
            }

            // Caso 4: No calza nada - guardar dump y lanzar excepción
            string rutaCompleta = PathHelper.GetDumpPath("CAF_INVALIDO").Replace(".html", ".txt");

            try
            {
                // Asegurar que el directorio existe
                if (!Directory.Exists(LogsDir))
                {
                    Directory.CreateDirectory(LogsDir);
                }

                // Guardar dump completo
                File.WriteAllText(rutaCompleta, rawTrimmed, Encoding.UTF8);

                // Registrar en log de depuración
                string logMsg4 = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SiiResponseGuard] ❌ No se encontró AUTORIZACION ni <CAF. Dump guardado en: {rutaCompleta}\n";
                File.AppendAllText(DepuracionLog, logMsg4, Encoding.UTF8);

                throw new Exception($"El contenido del CAF no contiene un bloque AUTORIZACION válido ni un nodo <CAF> directo. Dump guardado en: {rutaCompleta}");
            }
            catch (DirectoryNotFoundException)
            {
                throw new Exception($"No se pudo crear el directorio de dumps: {LogsDir}. El contenido del CAF no contiene un bloque AUTORIZACION válido ni un nodo <CAF> directo.");
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("AUTORIZACION")))
            {
                // Si falla el guardado pero ya sabemos que es inválido, lanzar excepción
                throw new Exception($"El contenido del CAF no contiene un bloque AUTORIZACION válido ni un nodo <CAF> directo. Error al guardar dump: {ex.Message}", ex);
            }
        }
    }
}
