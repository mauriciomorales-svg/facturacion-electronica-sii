using System;
using System.IO;

namespace FacturacionElectronicaSII.Helpers
{
    /// <summary>
    /// Helper para manejar rutas de archivos y directorios del servicio
    /// </summary>
    public static class PathHelper
    {
        private static readonly string BaseDir = GetBaseDirectory();
        
        /// <summary>
        /// Obtiene el directorio base para logs y archivos temporales
        /// </summary>
        private static string GetBaseDirectory()
        {
            // Intentar usar C:\FacturasElectronicas si existe o se puede crear
            string facturasDir = @"C:\FacturasElectronicas";
            try
            {
                if (!Directory.Exists(facturasDir))
                {
                    Directory.CreateDirectory(facturasDir);
                }
                // Verificar que tenemos permisos de escritura
                string testFile = Path.Combine(facturasDir, ".test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return facturasDir;
            }
            catch
            {
                // Si no se puede usar C:\FacturasElectronicas, usar el directorio de la aplicación
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>
        /// Directorio base para logs y archivos temporales
        /// </summary>
        public static string BaseDirectory => BaseDir;

        /// <summary>
        /// Directorio para dumps de HTML/XML corruptos
        /// </summary>
        public static string LogsDumpsDirectory
        {
            get
            {
                string dir = Path.Combine(BaseDir, "Logs", "Dumps");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        /// <summary>
        /// Ruta completa del archivo de log de depuración
        /// </summary>
        public static string DepuracionLogPath => Path.Combine(BaseDir, "depuracion_log.txt");

        /// <summary>
        /// Ruta completa del archivo de log general
        /// </summary>
        public static string DepuracionGeneralPath => Path.Combine(BaseDir, "depuracion_general.txt");

        /// <summary>
        /// Ruta completa para guardar respuesta del SII
        /// </summary>
        public static string RespuestaSIIPath => Path.Combine(BaseDir, "respuesta_sii.html");

        /// <summary>
        /// Ruta completa para guardar archivos de depuración específicos
        /// </summary>
        public static string GetDepuracionPath(string nombreArchivo)
        {
            return Path.Combine(BaseDir, nombreArchivo);
        }

        /// <summary>
        /// Ruta completa para guardar dumps con timestamp
        /// </summary>
        public static string GetDumpPath(string contexto)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string nombreArchivo = $"{contexto}_{timestamp}.html";
            return Path.Combine(LogsDumpsDirectory, nombreArchivo);
        }
    }
}
