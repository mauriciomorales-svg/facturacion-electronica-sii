using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Comercial_isabel.FacturacionElectronica;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio para generar el Timbre Electrónico Digital (TED) para documentos tributarios electrónicos
    /// Refactorizado desde GeneradorTED legacy manteniendo la misma lógica de negocio
    /// </summary>
    public class TimbreElectronicoService
    {
        private readonly string _logPath;
        private readonly string _tedPath;
        private readonly string _clavePath;

        /// <summary>
        /// Constructor por defecto que usa las rutas estándar
        /// </summary>
        public TimbreElectronicoService()
            : this(@"C:\FacturasElectronicas\depuracion_log.txt",
                   @"C:\FacturasElectronicas\TED.xml",
                   @"C:\FacturasElectronicas\clavePrivada.pem")
        {
        }

        /// <summary>
        /// Constructor con rutas personalizadas para facilitar testing
        /// </summary>
        /// <param name="logPath">Ruta del archivo de log</param>
        /// <param name="tedPath">Ruta donde se guardará el TED generado</param>
        /// <param name="clavePath">Ruta del archivo de clave privada (opcional)</param>
        public TimbreElectronicoService(string logPath, string tedPath, string clavePath = null)
        {
            _logPath = logPath ?? @"C:\FacturasElectronicas\depuracion_log.txt";
            _tedPath = tedPath ?? @"C:\FacturasElectronicas\TED.xml";
            _clavePath = clavePath ?? @"C:\FacturasElectronicas\clavePrivada.pem";
        }

        /// <summary>
        /// Genera el Timbre Electrónico Digital (TED) para un documento tributario
        /// </summary>
        /// <param name="nodoDD">Nodo DD que contiene los datos del documento</param>
        /// <param name="folio">Número de folio del documento</param>
        /// <param name="tipoDoc">Tipo de documento tributario (33, 39, 61, 56)</param>
        /// <param name="fechaEmision">Fecha de emisión del documento</param>
        /// <param name="rutReceptor">RUT del receptor</param>
        /// <param name="razonSocialReceptor">Razón social del receptor</param>
        /// <param name="montoTotal">Monto total del documento</param>
        /// <param name="itemPrincipal">Descripción del ítem principal (máximo 40 caracteres)</param>
        /// <param name="cafXml">XML del CAF (Código de Autorización de Folios)</param>
        /// <param name="fechaTimbre">Fecha y hora del timbre</param>
        /// <returns>XML del TED generado</returns>
        /// <exception cref="Exception">Se lanza cuando hay un error en la generación del TED</exception>
        public string GenerarTimbre(
            string nodoDD, int folio, int tipoDoc, string fechaEmision,
            string rutReceptor, string razonSocialReceptor, int montoTotal,
            string itemPrincipal, string cafXml, string fechaTimbre)
        {
            try
            {
                LogMensaje("=== [1] INICIO GENERACIÓN DEL TIMBRE ===");

                // 🔹 Verificar que el nodo DD no esté vacío
                if (string.IsNullOrEmpty(nodoDD))
                    throw new Exception("El nodo DD está vacío.");

                // 🔹 Convertir el nodo DD en un string limpio antes de firmarlo
                nodoDD = FuncionesComunes.LimpiarNodoDD(nodoDD);
                LogMensaje($"=== [2] Nodo DD listo para firmar ===\n{nodoDD}\n");

                // 🔹 Extraer clave privada del CAF
                string clavePrivada = FuncionesComunes.ExtraerClavePrivada(cafXml);
                if (string.IsNullOrEmpty(clavePrivada))
                    throw new Exception("No se pudo extraer la clave privada del CAF.");

                LogMensaje("=== [3] Clave privada extraída correctamente ===\n");

                // 🔹 Importar clave privada
                RSACryptoServiceProvider claveRSA = FuncionesComunes.ImportarClavePrivada(clavePrivada);
                if (claveRSA == null)
                    throw new Exception("No se pudo importar la clave privada.");

                LogMensaje("=== [4] Clave privada importada correctamente ===\n");

                // 🔹 Firmar solo el nodo DD antes de construir el TED
                string firma = FuncionesComunes.FirmarTexto(nodoDD, claveRSA);
                if (string.IsNullOrEmpty(firma))
                    throw new Exception("La firma generada está vacía.");

                LogMensaje($"=== [5] Firma generada correctamente ===\n{firma}\n");

                // 🔹 Ahora que tenemos el DD firmado, construir el TED
                string ted = GenerarTEDCompleto(nodoDD, firma);

                // Guardar TED en archivo
                GuardarTEDEnArchivo(ted);
                LogMensaje($"=== [6] TED generado correctamente ===\n{ted}\n");
                LogMensaje("=== [7] FIN GENERACIÓN DEL TIMBRE ===\n");

                return ted;
            }
            catch (Exception ex)
            {
                LogError($"❌ ERROR en GenerarTimbre: {ex.Message}\n{ex.StackTrace}\n");
                throw new Exception($"Error al generar el Timbre Electrónico Digital: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Genera el XML completo del TED con el DD y la firma
        /// </summary>
        /// <param name="dd">Nodo DD limpio (sin etiquetas DD externas)</param>
        /// <param name="firma">Firma digital del DD en Base64</param>
        /// <returns>XML completo del TED</returns>
        public string GenerarTEDCompleto(string dd, string firma)
        {
            // 🔹 Asegurar que el nodo DD no esté anidado incorrectamente dentro del TED
            if (dd.StartsWith("<DD>") && dd.EndsWith("</DD>"))
            {
                dd = dd.Substring(4, dd.Length - 9); // Elimina <DD> y </DD>
            }

            string ted = $@"
<TED version=""1.0"">
    <DD>{dd}</DD>
    <FRMT algoritmo=""SHA1withRSA"">{firma}</FRMT>
</TED>";

            // 🔹 Guardar TED final antes de enviarlo para depuración
            GuardarTEDDepuracion(ted);

            return ted;
        }

        /// <summary>
        /// Registra un mensaje en el archivo de log
        /// </summary>
        /// <param name="mensaje">Mensaje a registrar</param>
        private void LogMensaje(string mensaje)
        {
            try
            {
                File.AppendAllText(_logPath, mensaje, Encoding.UTF8);
            }
            catch
            {
                // Si falla el logging, no interrumpir el flujo principal
            }
        }

        /// <summary>
        /// Registra un error en el archivo de log
        /// </summary>
        /// <param name="mensaje">Mensaje de error a registrar</param>
        private void LogError(string mensaje)
        {
            try
            {
                File.AppendAllText(_logPath, mensaje, Encoding.UTF8);
            }
            catch
            {
                // Si falla el logging, no interrumpir el flujo principal
            }
        }

        /// <summary>
        /// Guarda el TED generado en el archivo especificado
        /// </summary>
        /// <param name="ted">XML del TED a guardar</param>
        private void GuardarTEDEnArchivo(string ted)
        {
            try
            {
                File.WriteAllText(_tedPath, ted, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError($"❌ Error guardando archivo TED: {ex.Message}\n");
                // No lanzar excepción, solo loggear el error
            }
        }

        /// <summary>
        /// Guarda el TED final para depuración
        /// </summary>
        /// <param name="ted">XML del TED a guardar</param>
        private void GuardarTEDDepuracion(string ted)
        {
            try
            {
                string rutaArchivo = Path.Combine(Path.GetDirectoryName(_logPath), "Depuracion_TED_Final.txt");
                File.WriteAllText(rutaArchivo, ted, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogError($"❌ Error guardando archivo TED de depuración: {ex.Message}\n");
                // No lanzar excepción, solo loggear el error
            }
        }
    }
}
