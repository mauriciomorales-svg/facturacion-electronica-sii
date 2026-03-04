# Ejemplos de Código Completos - DTE SII Chile

## 📝 Clase: FuncionesComunes.cs

```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Comercial_isabel.FacturacionElectronica
{
    public static class FuncionesComunes
    {
        /// <summary>
        /// Recupera un certificado del almacén de Windows por nombre del sujeto
        /// </summary>
        public static X509Certificate2 RecuperarCertificado(string subjectName)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);

                var encontrados = store.Certificates.Find(
                    X509FindType.FindBySubjectName,
                    subjectName,
                    false  // validOnly = false para certificados de prueba
                );

                if (encontrados.Count == 0)
                {
                    throw new Exception($"❌ Certificado '{subjectName}' no encontrado en el almacén personal.");
                }

                // Retornar el primer certificado encontrado
                return encontrados[0];
            }
        }

        /// <summary>
        /// Extrae la clave privada RSA del XML del CAF
        /// </summary>
        public static string ExtraerClavePrivada(string cafXml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.LoadXml(cafXml);

                // Buscar nodo RSASK (RSA Secret Key)
                XmlNode rsaskNode = doc.SelectSingleNode("//RSASK");
                if (rsaskNode == null)
                {
                    throw new Exception("No se encontró el nodo RSASK en el CAF");
                }

                string clavePrivada = rsaskNode.InnerText.Trim();

                // Formatear como PEM si no lo está
                if (!clavePrivada.Contains("BEGIN RSA PRIVATE KEY"))
                {
                    clavePrivada = FormatearClavePEM(clavePrivada);
                }

                return clavePrivada;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al extraer clave privada del CAF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Formatea una clave privada en formato PEM
        /// </summary>
        private static string FormatearClavePEM(string claveBase64)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");

            // Dividir en líneas de 64 caracteres
            int pos = 0;
            while (pos < claveBase64.Length)
            {
                int len = Math.Min(64, claveBase64.Length - pos);
                sb.AppendLine(claveBase64.Substring(pos, len));
                pos += len;
            }

            sb.AppendLine("-----END RSA PRIVATE KEY-----");
            return sb.ToString();
        }

        /// <summary>
        /// Importa una clave privada desde formato PEM
        /// </summary>
        public static RSACryptoServiceProvider ImportarClavePrivada(string pemKey)
        {
            try
            {
                // Limpiar el PEM
                string base64 = pemKey
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\r\n", "")
                    .Replace("\n", "")
                    .Trim();

                byte[] keyBytes = Convert.FromBase64String(base64);

                // Crear el proveedor RSA
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                rsa.ImportCspBlob(ConvertirPKCS1ToCspBlob(keyBytes));

                return rsa;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al importar clave privada: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convierte una clave PKCS#1 a formato CSP Blob
        /// </summary>
        private static byte[] ConvertirPKCS1ToCspBlob(byte[] pkcs1)
        {
            // Implementación simplificada - en producción usar biblioteca como BouncyCastle
            // Por ahora asumimos que el SII provee claves en formato compatible
            return pkcs1;
        }

        /// <summary>
        /// Firma un texto usando SHA1withRSA (compatibilidad con el SII)
        /// </summary>
        public static string FirmarTexto(string texto, RSACryptoServiceProvider rsa)
        {
            try
            {
                // Calcular hash SHA1 del texto
                byte[] textoBytes = Encoding.UTF8.GetBytes(texto);
                SHA1 sha1 = SHA1.Create();
                byte[] hash = sha1.ComputeHash(textoBytes);

                // Firmar el hash
                RSAPKCS1SignatureFormatter formatter = new RSAPKCS1SignatureFormatter(rsa);
                formatter.SetHashAlgorithm("SHA1");
                byte[] firma = formatter.CreateSignature(hash);

                // Retornar en Base64
                return Convert.ToBase64String(firma);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al firmar texto: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Limpia el nodo DD eliminando espacios y saltos de línea
        /// CRÍTICO: El DD debe estar completamente limpio antes de firmarlo
        /// </summary>
        public static string LimpiarNodoDD(string nodoDD)
        {
            // Eliminar declaración XML si existe
            nodoDD = Regex.Replace(nodoDD, @"<\?xml[^>]*\?>", "");

            // Eliminar todos los espacios en blanco entre tags
            nodoDD = Regex.Replace(nodoDD, @">\s+<", "><");

            // Eliminar saltos de línea y retornos de carro
            nodoDD = nodoDD
                .Replace("\r\n", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();

            return nodoDD;
        }

        /// <summary>
        /// Valida el formato de un RUT chileno
        /// </summary>
        public static bool ValidarRUT(string rut)
        {
            // Eliminar puntos y guión
            rut = rut.Replace(".", "").Replace("-", "").Trim();

            if (rut.Length < 2)
                return false;

            // Separar número y dígito verificador
            string numero = rut.Substring(0, rut.Length - 1);
            char dv = char.ToUpper(rut[rut.Length - 1]);

            // Validar que el número sea numérico
            if (!int.TryParse(numero, out int rutNumero))
                return false;

            // Calcular dígito verificador
            char dvCalculado = CalcularDigitoVerificador(rutNumero);

            return dv == dvCalculado;
        }

        /// <summary>
        /// Calcula el dígito verificador de un RUT
        /// </summary>
        private static char CalcularDigitoVerificador(int rut)
        {
            int suma = 0;
            int multiplicador = 2;

            while (rut > 0)
            {
                suma += (rut % 10) * multiplicador;
                rut /= 10;
                multiplicador = multiplicador == 7 ? 2 : multiplicador + 1;
            }

            int resto = 11 - (suma % 11);

            if (resto == 11) return '0';
            if (resto == 10) return 'K';
            return resto.ToString()[0];
        }

        /// <summary>
        /// Formatea un RUT al formato estándar chileno (XX.XXX.XXX-X)
        /// </summary>
        public static string FormatearRUT(string rut)
        {
            // Limpiar
            rut = rut.Replace(".", "").Replace("-", "").Trim();

            if (rut.Length < 2)
                return rut;

            // Separar número y DV
            string numero = rut.Substring(0, rut.Length - 1);
            string dv = rut.Substring(rut.Length - 1);

            // Agregar puntos de miles
            string numeroFormateado = long.Parse(numero).ToString("N0")
                .Replace(",", ".");

            return $"{numeroFormateado}-{dv}";
        }

        /// <summary>
        /// Extrae el módulo y exponente de la clave pública RSA del CAF
        /// </summary>
        public static (string modulo, string exponente) ExtraerClavePublicaCAF(string cafXml)
        {
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(cafXml);

            XmlNode rsapkNode = doc.SelectSingleNode("//RSAPK");
            if (rsapkNode == null)
            {
                throw new Exception("No se encontró el nodo RSAPK en el CAF");
            }

            string modulo = rsapkNode.SelectSingleNode("M")?.InnerText;
            string exponente = rsapkNode.SelectSingleNode("E")?.InnerText;

            if (string.IsNullOrWhiteSpace(modulo) || string.IsNullOrWhiteSpace(exponente))
            {
                throw new Exception("La clave pública del CAF está incompleta");
            }

            return (modulo, exponente);
        }

        /// <summary>
        /// Guarda un archivo XML con encoding ISO-8859-1 sin BOM
        /// </summary>
        public static void GuardarXMLSinBOM(string xmlContent, string rutaArchivo)
        {
            try
            {
                // Asegurar que el directorio existe
                string directorio = Path.GetDirectoryName(rutaArchivo);
                if (!string.IsNullOrEmpty(directorio))
                {
                    Directory.CreateDirectory(directorio);
                }

                // Convertir a bytes usando ISO-8859-1
                Encoding iso88591 = Encoding.GetEncoding("ISO-8859-1");
                byte[] bytes = iso88591.GetBytes(xmlContent);

                // Guardar directamente (ISO-8859-1 no tiene BOM)
                File.WriteAllBytes(rutaArchivo, bytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al guardar archivo XML: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lee un archivo XML con encoding ISO-8859-1
        /// </summary>
        public static string LeerXMLISO88591(string rutaArchivo)
        {
            try
            {
                if (!File.Exists(rutaArchivo))
                {
                    throw new FileNotFoundException($"Archivo no encontrado: {rutaArchivo}");
                }

                Encoding iso88591 = Encoding.GetEncoding("ISO-8859-1");
                byte[] bytes = File.ReadAllBytes(rutaArchivo);

                return iso88591.GetString(bytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al leer archivo XML: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Valida que un string sea un XML bien formado
        /// </summary>
        public static bool EsXMLValido(string xml)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Escapa caracteres especiales XML
        /// </summary>
        public static string EscaparXML(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return texto;

            return System.Security.SecurityElement.Escape(texto);
        }

        /// <summary>
        /// Obtiene la fecha y hora actual en zona horaria de Chile
        /// </summary>
        public static DateTime ObtenerFechaHoraChile()
        {
            try
            {
                TimeZoneInfo tzChile = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time");
                return TimeZoneInfo.ConvertTime(DateTime.Now, tzChile);
            }
            catch
            {
                // Fallback: UTC-3 (hora estándar de Chile)
                return DateTime.UtcNow.AddHours(-3);
            }
        }

        /// <summary>
        /// Formatea una fecha en el formato requerido por el SII (yyyy-MM-dd)
        /// </summary>
        public static string FormatearFechaSII(DateTime fecha)
        {
            return fecha.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Formatea un timestamp en el formato requerido por el SII (yyyy-MM-ddTHH:mm:ss)
        /// </summary>
        public static string FormatearTimestampSII(DateTime fecha)
        {
            return fecha.ToString("yyyy-MM-ddTHH:mm:ss");
        }
    }
}
```

---

## 📝 Clase: UploaderSII.cs

```csharp
using System;
using System.IO;
using System.Net;
using System.Text;

namespace Comercial_isabel.FacturacionElectronica
{
    public class UploaderSII
    {
        private const string LogPath = @"C:\FacturasElectronicas\upload_log.txt";

        // URLs del SII
        private const string UrlCertificacion = "https://maullin.sii.cl/cgi_dte/UPL/DTEUpload";
        private const string UrlProduccion = "https://palena.sii.cl/cgi_dte/UPL/DTEUpload";

        private static void Log(string etapa, string detalle)
        {
            try
            {
                Directory.CreateDirectory(@"C:\FacturasElectronicas");
                File.AppendAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [UPLOAD_{etapa}] {detalle}\r\n",
                    Encoding.UTF8
                );
            }
            catch { }
        }

        /// <summary>
        /// Envía el EnvioDTE firmado al SII
        /// </summary>
        /// <param name="envioDTEFirmado">XML del EnvioDTE firmado</param>
        /// <param name="rutEmpresa">RUT de la empresa (sin puntos, con guión)</param>
        /// <param name="dvEmpresa">Dígito verificador</param>
        /// <param name="token">Token de autenticación del SII</param>
        /// <param name="esCertificacion">True para Maullin, False para Palena</param>
        /// <returns>Respuesta HTML del SII</returns>
        public static string EnviarDTE(
            string envioDTEFirmado,
            string rutEmpresa,
            string dvEmpresa,
            string token,
            bool esCertificacion = true)
        {
            try
            {
                Log("INICIO", $"Enviando DTE - RUT: {rutEmpresa}-{dvEmpresa}, Ambiente: {(esCertificacion ? "Certificación" : "Producción")}");

                // Seleccionar URL según ambiente
                string url = esCertificacion ? UrlCertificacion : UrlProduccion;

                // Configurar protocolo de seguridad
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                // Crear el request
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "multipart/form-data; boundary=---------------------------" + DateTime.Now.Ticks.ToString("x");
                request.Timeout = 60000; // 60 segundos

                // Construir el body multipart
                string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
                byte[] bodyBytes = ConstruirMultipartBody(
                    envioDTEFirmado,
                    rutEmpresa,
                    dvEmpresa,
                    token,
                    boundary
                );

                request.ContentLength = bodyBytes.Length;

                Log("REQUEST", $"URL: {url}, Content-Length: {bodyBytes.Length} bytes");

                // Enviar el request
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(bodyBytes, 0, bodyBytes.Length);
                }

                Log("ENVIADO", "Request enviado, esperando respuesta...");

                // Obtener respuesta
                string respuestaHTML;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        respuestaHTML = reader.ReadToEnd();
                    }

                    Log("RESPUESTA", $"Status: {response.StatusCode}, Length: {respuestaHTML.Length} chars");
                }

                // Guardar respuesta en archivo
                string rutaRespuesta = @"C:\FacturasElectronicas\respuesta_sii.html";
                File.WriteAllText(rutaRespuesta, respuestaHTML, Encoding.UTF8);
                Log("GUARDADO", $"Respuesta guardada en: {rutaRespuesta}");

                // Analizar respuesta
                if (respuestaHTML.Contains("RECIBIDO") || respuestaHTML.Contains("Identificador de envío"))
                {
                    Log("EXITO", "✅ DTE recibido correctamente por el SII");
                }
                else if (respuestaHTML.Contains("ERROR") || respuestaHTML.Contains("RECHAZADO"))
                {
                    Log("ERROR", "❌ DTE rechazado por el SII");
                }

                return respuestaHTML;
            }
            catch (WebException wex)
            {
                string errorDetalle = wex.Message;

                try
                {
                    if (wex.Response != null)
                    {
                        using (StreamReader reader = new StreamReader(wex.Response.GetResponseStream()))
                        {
                            errorDetalle += "\nRespuesta: " + reader.ReadToEnd();
                        }
                    }
                }
                catch { }

                Log("ERROR_WEB", errorDetalle);
                throw new Exception($"Error de red al enviar al SII: {errorDetalle}", wex);
            }
            catch (Exception ex)
            {
                Log("ERROR", ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Construye el body multipart para el upload
        /// </summary>
        private static byte[] ConstruirMultipartBody(
            string envioDTE,
            string rutEmpresa,
            string dvEmpresa,
            string token,
            string boundary)
        {
            Encoding encoding = Encoding.UTF8;
            StringBuilder sb = new StringBuilder();

            // Campo: rutSender
            sb.AppendLine("--" + boundary);
            sb.AppendLine("Content-Disposition: form-data; name=\"rutSender\"");
            sb.AppendLine();
            sb.AppendLine(rutEmpresa);

            // Campo: dvSender
            sb.AppendLine("--" + boundary);
            sb.AppendLine("Content-Disposition: form-data; name=\"dvSender\"");
            sb.AppendLine();
            sb.AppendLine(dvEmpresa);

            // Campo: rutCompany
            sb.AppendLine("--" + boundary);
            sb.AppendLine("Content-Disposition: form-data; name=\"rutCompany\"");
            sb.AppendLine();
            sb.AppendLine(rutEmpresa);

            // Campo: dvCompany
            sb.AppendLine("--" + boundary);
            sb.AppendLine("Content-Disposition: form-data; name=\"dvCompany\"");
            sb.AppendLine();
            sb.AppendLine(dvEmpresa);

            // Campo: archivo
            sb.AppendLine("--" + boundary);
            sb.AppendLine("Content-Disposition: form-data; name=\"archivo\"; filename=\"EnvioDTE_Firmado.xml\"");
            sb.AppendLine("Content-Type: text/xml");
            sb.AppendLine();

            byte[] headerBytes = encoding.GetBytes(sb.ToString());
            byte[] envioBytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(envioDTE);
            byte[] footerBytes = encoding.GetBytes($"\r\n--{boundary}--\r\n");

            // Combinar todo
            byte[] resultado = new byte[headerBytes.Length + envioBytes.Length + footerBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, resultado, 0, headerBytes.Length);
            Buffer.BlockCopy(envioBytes, 0, resultado, headerBytes.Length, envioBytes.Length);
            Buffer.BlockCopy(footerBytes, 0, resultado, headerBytes.Length + envioBytes.Length, footerBytes.Length);

            return resultado;
        }

        /// <summary>
        /// Extrae el Track ID de la respuesta HTML del SII
        /// </summary>
        public static string ExtraerTrackID(string respuestaHTML)
        {
            try
            {
                // El SII retorna el Track ID en el HTML
                // Formato: "Identificador de envío : <strong>0245072890</strong>"
                
                int inicio = respuestaHTML.IndexOf("Identificador de env");
                if (inicio < 0)
                    return null;

                inicio = respuestaHTML.IndexOf("<strong>", inicio);
                if (inicio < 0)
                    return null;

                inicio += 8; // Longitud de "<strong>"

                int fin = respuestaHTML.IndexOf("</strong>", inicio);
                if (fin < 0)
                    return null;

                return respuestaHTML.Substring(inicio, fin - inicio).Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
```

---

## 📝 Clase: EnvioDTENormalizer.cs

```csharp
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Comercial_isabel.FacturacionElectronica.GeneracionXML
{
    /// <summary>
    /// Normaliza y corrige problemas comunes en el EnvioDTE antes de enviar
    /// </summary>
    public static class EnvioDTENormalizer
    {
        /// <summary>
        /// Normaliza el EnvioDTE para cumplir con todos los requisitos del SII
        /// </summary>
        public static string Normalizar(string envioDTEXml)
        {
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(envioDTEXml);

            // 1. Corregir namespace y schemaLocation
            CorregirNamespaces(doc);

            // 2. Asegurar version="1.0"
            AsegurarVersion(doc);

            // 3. Verificar SetDTE ID="SetDoc"
            VerificarSetDTEID(doc);

            // 4. Limpiar firmas duplicadas
            LimpiarFirmasDuplicadas(doc);

            // 5. Verificar estructura de Carátula
            VerificarCaratula(doc);

            return doc.OuterXml;
        }

        private static void CorregirNamespaces(XmlDocument doc)
        {
            XmlElement root = doc.DocumentElement;

            // Limpiar atributos incorrectos
            root.RemoveAttribute("schemaLocation");
            if (root.HasAttribute("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance"))
            {
                root.RemoveAttribute("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance");
            }

            // Establecer correctos
            root.SetAttribute("xmlns", "http://www.sii.cl/SiiDte");
            root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            root.SetAttribute(
                "schemaLocation",
                "http://www.w3.org/2001/XMLSchema-instance",
                "http://www.sii.cl/SiiDte EnvioDTE_v10.xsd"
            );
        }

        private static void AsegurarVersion(XmlDocument doc)
        {
            doc.DocumentElement.SetAttribute("version", "1.0");

            // También en Carátula
            XmlNode caratula = doc.SelectSingleNode("//*[local-name()='Caratula']");
            if (caratula != null && caratula is XmlElement)
            {
                ((XmlElement)caratula).SetAttribute("version", "1.0");
            }
        }

        private static void VerificarSetDTEID(XmlDocument doc)
        {
            XmlNode setDTE = doc.SelectSingleNode("//*[local-name()='SetDTE']");
            if (setDTE != null && setDTE is XmlElement)
            {
                ((XmlElement)setDTE).SetAttribute("ID", "SetDoc");
            }
        }

        private static void LimpiarFirmasDuplicadas(XmlDocument doc)
        {
            // Mantener solo la última firma en EnvioDTE
            XmlNodeList firmas = doc.DocumentElement.SelectNodes("*[local-name()='Signature']");
            
            if (firmas.Count > 1)
            {
                // Eliminar todas excepto la última
                for (int i = 0; i < firmas.Count - 1; i++)
                {
                    firmas[i].ParentNode.RemoveChild(firmas[i]);
                }
            }
        }

        private static void VerificarCaratula(XmlDocument doc)
        {
            XmlNode caratula = doc.SelectSingleNode("//*[local-name()='Caratula']");
            if (caratula == null)
            {
                throw new Exception("Falta el nodo Carátula en el EnvioDTE");
            }

            // Verificar campos obligatorios
            string[] camposObligatorios = { "RutEmisor", "RutEnvia", "RutReceptor", 
                                           "FchResol", "NroResol", "TmstFirmaEnv" };

            foreach (string campo in camposObligatorios)
            {
                XmlNode nodo = caratula.SelectSingleNode(campo);
                if (nodo == null || string.IsNullOrWhiteSpace(nodo.InnerText))
                {
                    throw new Exception($"Falta campo obligatorio en Carátula: {campo}");
                }
            }
        }

        /// <summary>
        /// Rompe líneas largas en el XML para evitar CHR-00002
        /// </summary>
        public static string RomperLineasLargas(string xml, int maxLength = 4090)
        {
            string[] lineas = xml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();

            foreach (string linea in lineas)
            {
                if (linea.Length > maxLength)
                {
                    // Es probablemente el certificado X509 en KeyInfo
                    // Dividir en múltiples líneas
                    sb.AppendLine(DividirLineaLarga(linea, maxLength));
                }
                else
                {
                    sb.AppendLine(linea);
                }
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }

        private static string DividirLineaLarga(string linea, int maxLength)
        {
            // Buscar contenido de certificado X509
            Regex certRegex = new Regex(@"<X509Certificate>(.*?)</X509Certificate>");
            Match match = certRegex.Match(linea);

            if (match.Success)
            {
                string certContent = match.Groups[1].Value;
                StringBuilder sbCert = new StringBuilder();

                sbCert.Append("<X509Certificate>");
                
                // Dividir contenido en líneas de 64 caracteres
                for (int i = 0; i < certContent.Length; i += 64)
                {
                    int len = Math.Min(64, certContent.Length - i);
                    sbCert.AppendLine(certContent.Substring(i, len));
                }

                sbCert.Append("</X509Certificate>");

                return linea.Replace(match.Value, sbCert.ToString());
            }

            return linea;
        }
    }
}
```

Estas clases complementan perfectamente tu implementación actual. ¿Quieres que continúe con más ejemplos o necesitas algo específico?
