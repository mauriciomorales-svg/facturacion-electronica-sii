using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Security;
using System.Security.Cryptography;
using System.Globalization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

namespace Comercial_isabel.FacturacionElectronica
{
    public static class FuncionesComunes
    {
        // =========================================================
        //  CERTIFICADOS
        // =========================================================
        public static X509Certificate2 RecuperarCertificado(string nombreCertificado)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            X509Certificate2Collection collection = store.Certificates;
            X509Certificate2Collection resultados =
                collection.Find(X509FindType.FindBySubjectName, nombreCertificado, false);

            store.Close();

            if (resultados.Count > 0)
                return resultados[0];

            throw new Exception($"Certificado no encontrado: {nombreCertificado}");
        }

        // =========================================================
        //  NODO DD / TED
        // =========================================================
        public static string ObtenerNodoDD()
        {
            try
            {
                string rutaXML = @"C:\FacturasElectronicas\NodoDD.xml";

                if (!File.Exists(rutaXML))
                {
                    Console.WriteLine("❌ Error: No se encontró el archivo NodoDD.xml.");
                    return string.Empty;
                }

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(rutaXML);

                XmlNode nodoDD = xmlDoc.SelectSingleNode("//DD");

                if (nodoDD == null)
                {
                    Console.WriteLine("❌ Error: No se encontró el nodo <DD> en el XML.");
                    return string.Empty;
                }

                string nodoDDStr = nodoDD.OuterXml.Trim();
                File.WriteAllText(@"C:\FacturasElectronicas\NodoDD_Debug.txt",
                                  nodoDDStr, Encoding.UTF8);

                return nodoDDStr;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error al obtener el nodo DD: " + ex.Message);
                return string.Empty;
            }
        }

        public static string FirmarTexto(string texto, RSACryptoServiceProvider clavePrivada)
        {
            try
            {
                File.WriteAllText(@"C:\FacturasElectronicas\Depuracion_NodoDD_After.txt", texto);

                byte[] datos = Encoding.UTF8.GetBytes(texto);
                using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                {
                    byte[] hash = sha1.ComputeHash(datos);
                    byte[] firma = clavePrivada.SignHash(hash, CryptoConfig.MapNameToOID("SHA1"));

                    if (firma == null || firma.Length == 0)
                        throw new Exception("Firma vacía generada.");

                    return Convert.ToBase64String(firma);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al firmar el texto: {ex.Message}");
                return string.Empty;
            }
        }

        public static bool VerificarEstructuraNodoDD(string nodoDD)
        {
            if (string.IsNullOrEmpty(nodoDD))
                return false;

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(nodoDD);

                string[] nodosObligatorios = { "RE", "TD", "F", "FE", "RR", "RSR", "MNT", "IT1", "CAF", "TSTED" };
                foreach (string nodo in nodosObligatorios)
                {
                    if (xmlDoc.SelectSingleNode($"//{nodo}") == null)
                    {
                        Console.WriteLine($"❌ Falta el nodo obligatorio: <{nodo}> en el DD.");
                        return false;
                    }
                }

                XmlNode cafNode = xmlDoc.SelectSingleNode("//CAF");
                if (cafNode == null || string.IsNullOrEmpty(cafNode.InnerXml))
                {
                    Console.WriteLine("❌ El nodo <CAF> está vacío o no es válido.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en VerificarEstructuraNodoDD: {ex.Message}");
                return false;
            }
        }

        public static string ExtraerCAF(string cafXml)
        {
            try
            {
                // Log snippet inicial de 300 caracteres
                string snippet = cafXml.Length > 300 ? cafXml.Substring(0, 300) : cafXml;
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_log.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ExtraerCAF] Snippet inicial (300 chars): {snippet}\n",
                    Encoding.UTF8);

                // Blindar contra HTML
                SiiResponseGuard.DumpYThrowSiHtml(cafXml, "CAF_CAF_RAW");

                // Normalizar XML del CAF
                string xml = SiiResponseGuard.NormalizarCafXml(cafXml);
                
                // Si hubo dump, registrar la ruta (ya está registrado en SiiResponseGuard)
                // Registrar que se procede con el XML normalizado
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_log.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ExtraerCAF] XML normalizado, longitud: {xml.Length}\n",
                    Encoding.UTF8);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);

                XmlNode nodoCAF = xmlDoc.SelectSingleNode("//CAF");
                if (nodoCAF == null)
                    throw new Exception("No se encontró el nodo <CAF> en el XML.");

                return LimpiarNodoDD(nodoCAF.OuterXml);
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_log.txt",
                                   $"❌ Error en ExtraerCAF: {ex.Message}\n",
                                   Encoding.UTF8);
                return string.Empty;
            }
        }

        public static RSACryptoServiceProvider CrearRSADesdePEM(string clavePrivadaPEM)
        {
            try
            {
                clavePrivadaPEM = clavePrivadaPEM
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\n", "").Replace("\r", "").Trim();

                byte[] claveBytes = Convert.FromBase64String(clavePrivadaPEM);

                RSACryptoServiceProvider rsa = DecodeRSAPrivateKey(claveBytes);
                if (rsa == null)
                    throw new Exception("Error al importar la clave privada. Formato incorrecto.");

                return rsa;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al importar clave privada: " + ex.Message);
                return null;
            }
        }

        public static string GenerarTEDCompleto(string dd, string firma)
        {
            if (dd.StartsWith("<DD>") && dd.EndsWith("</DD>"))
            {
                dd = dd.Substring(4, dd.Length - 9);
            }

            string ted = $@"
<TED version=""1.0"">
    <DD>{dd}</DD>
    <FRMT algoritmo=""SHA1withRSA"">{firma}</FRMT>
</TED>";

            string rutaArchivo = @"C:\FacturasElectronicas\Depuracion_TED_Final.txt";
            try
            {
                File.WriteAllText(rutaArchivo, ted);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error guardando archivo TED: {ex.Message}");
            }

            return ted;
        }

        public static string GenerarNodoDDParaFirma(
            string rutEmisor, int tipoDoc, int folio, string fechaEmision,
            string rutReceptor, string razonSocialReceptor, int montoTotal,
            string itemPrincipal, string cafXml, string fechaTimbre)
        {
            string depuracionPath = @"C:\FacturasElectronicas\depuracion_nodoDD.txt";

            try
            {
                File.WriteAllText(depuracionPath, "=== [1] INICIO GENERACIÓN DEL NODO DD ===\n");

                XmlDocument cafDoc = new XmlDocument();
                cafDoc.LoadXml(cafXml);
                XmlNode cafNode = cafDoc.SelectSingleNode("//CAF");

                if (cafNode == null)
                {
                    File.AppendAllText(depuracionPath,
                        "❌ [2] ERROR: No se encontró el nodo <CAF> en el XML.\n");
                    return string.Empty;
                }

                File.AppendAllText(depuracionPath,
                    "\n=== [3] Contenido del nodo <CAF> antes de insertarlo ===\n" +
                    cafNode.OuterXml + "\n");

                // Truncar itemPrincipal a máximo 40 caracteres (requisito SII para IT1)
                string itemPrincipalTruncado = itemPrincipal != null && itemPrincipal.Length > 40 
                    ? itemPrincipal.Substring(0, 40) 
                    : itemPrincipal ?? "";
                
                // Codificar caracteres especiales XML en los valores de texto
                string rutEmisorEscapado = SecurityElement.Escape(rutEmisor ?? "");
                string rutReceptorEscapado = SecurityElement.Escape(rutReceptor ?? "");
                string razonSocialReceptorEscapado = SecurityElement.Escape(razonSocialReceptor ?? "");
                string itemPrincipalEscapado = SecurityElement.Escape(itemPrincipalTruncado);
                
                if (itemPrincipal != null && itemPrincipal.Length > 40)
                {
                    File.AppendAllText(depuracionPath,
                        $"⚠️ [ADVERTENCIA] itemPrincipal truncado de {itemPrincipal.Length} a 40 caracteres.\n" +
                        $"Original: '{itemPrincipal}'\n" +
                        $"Truncado: '{itemPrincipalTruncado}'\n");
                }
                
                string nodoDD = $@"
<DD>
    <RE>{rutEmisorEscapado}</RE>
    <TD>{tipoDoc}</TD>
    <F>{folio}</F>
    <FE>{fechaEmision}</FE>
    <RR>{rutReceptorEscapado}</RR>
    <RSR>{razonSocialReceptorEscapado}</RSR>
    <MNT>{montoTotal}</MNT>
    <IT1>{itemPrincipalEscapado}</IT1>
    {cafNode.OuterXml}
    <TSTED>{fechaTimbre}</TSTED>
</DD>";

                File.AppendAllText(depuracionPath,
                    "\n=== [4] Nodo DD generado correctamente ===\n" + nodoDD + "\n");

                return nodoDD.Trim();
            }
            catch (Exception ex)
            {
                File.AppendAllText(depuracionPath,
                    $"❌ [5] ERROR en GenerarNodoDDParaFirma: {ex.Message}\n");
                return string.Empty;
            }
        }

        // =========================================================
        //  UTILIDADES RSA
        // =========================================================
        private static RSACryptoServiceProvider DecodeRSAPrivateKey(byte[] privkey)
        {
            try
            {
                using (MemoryStream mem = new MemoryStream(privkey))
                using (BinaryReader binr = new BinaryReader(mem))
                {
                    binr.ReadUInt16();
                    binr.ReadByte();

                    RSAParameters rsaParams = new RSAParameters
                    {
                        Modulus = binr.ReadBytes(GetIntegerSize(binr)),
                        Exponent = binr.ReadBytes(GetIntegerSize(binr)),
                        D = binr.ReadBytes(GetIntegerSize(binr)),
                        P = binr.ReadBytes(GetIntegerSize(binr)),
                        Q = binr.ReadBytes(GetIntegerSize(binr)),
                        DP = binr.ReadBytes(GetIntegerSize(binr)),
                        DQ = binr.ReadBytes(GetIntegerSize(binr)),
                        InverseQ = binr.ReadBytes(GetIntegerSize(binr))
                    };

                    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                    rsa.ImportParameters(rsaParams);
                    return rsa;
                }
            }
            catch
            {
                return null;
            }
        }

        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = binr.ReadByte();
            if (bt != 0x02) return 0;

            int count;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();
            else if (bt == 0x82)
            {
                byte[] modint = { binr.ReadByte(), binr.ReadByte(), 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
                count = bt;

            while (binr.ReadByte() == 0x00)
                count -= 1;

            binr.BaseStream.Seek(-1, SeekOrigin.Current);
            return count;
        }

        public static string LimpiarTextoParaFirma(string texto)
        {
            return texto.Replace("\n", "").Replace("\r", "").Replace("\t", "").Trim();
        }

        public static string QuitarAcentos(string texto)
        {
            string normalizedString = texto.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < normalizedString.Length; i++)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(normalizedString[i]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(normalizedString[i]);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static RSACryptoServiceProvider ImportarClavePrivada(string clavePrivadaPEM)
        {
            try
            {
                clavePrivadaPEM = clavePrivadaPEM
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\n", "").Replace("\r", "").Trim();

                using (var sr = new StringReader(
                           $"-----BEGIN RSA PRIVATE KEY-----\n{clavePrivadaPEM}\n-----END RSA PRIVATE KEY-----"))
                {
                    PemReader pemReader = new PemReader(sr);
                    object obj = pemReader.ReadObject();

                    if (obj is AsymmetricCipherKeyPair keyPair)
                    {
                        RsaPrivateCrtKeyParameters keyParams =
                            (RsaPrivateCrtKeyParameters)keyPair.Private;
                        RSAParameters parametros = DotNetUtilities.ToRSAParameters(keyParams);
                        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                        rsa.ImportParameters(parametros);

                        File.AppendAllText(@"C:\FacturasElectronicas\depuracion_general.txt",
                            "\n=== [3] Clave privada importada con éxito ===\n");

                        return rsa;
                    }

                    throw new Exception("❌ ERROR: La clave privada no es válida o no se pudo leer.");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_general.txt",
                    $"❌ [4] ERROR en ImportarClavePrivada: {ex.Message}\n");

                return null;
            }
        }

        private static RSACryptoServiceProvider ImportarClaveBouncyCastle(string clavePEM)
        {
            try
            {
                using (StringReader sr = new StringReader(clavePEM))
                {
                    PemReader pemReader = new PemReader(sr);
                    object obj = pemReader.ReadObject();

                    if (obj is AsymmetricCipherKeyPair keyPair)
                    {
                        RsaPrivateCrtKeyParameters keyParams =
                            (RsaPrivateCrtKeyParameters)keyPair.Private;
                        RSAParameters rsaParams = DotNetUtilities.ToRSAParameters(keyParams);
                        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                        rsa.ImportParameters(rsaParams);
                        return rsa;
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_general.txt",
                    $"❌ ERROR al importar clave con BouncyCastle: {ex.Message}\n",
                    Encoding.UTF8);
            }

            return null;
        }

        private static RSACryptoServiceProvider ConvertirBouncyCastleRSA(AsymmetricKeyParameter privateKey)
        {
            RsaPrivateCrtKeyParameters rsaParams = (RsaPrivateCrtKeyParameters)privateKey;
            RSAParameters parametros = new RSAParameters
            {
                Modulus = rsaParams.Modulus.ToByteArrayUnsigned(),
                Exponent = rsaParams.PublicExponent.ToByteArrayUnsigned(),
                D = rsaParams.Exponent.ToByteArrayUnsigned(),
                P = rsaParams.P.ToByteArrayUnsigned(),
                Q = rsaParams.Q.ToByteArrayUnsigned(),
                DP = rsaParams.DP.ToByteArrayUnsigned(),
                DQ = rsaParams.DQ.ToByteArrayUnsigned(),
                InverseQ = rsaParams.QInv.ToByteArrayUnsigned()
            };

            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(parametros);
            return rsa;
        }

        // =========================================================
        //  LIMPIEZA / EJEMPLOS
        // =========================================================
        public static string LimpiarNodoDD(string nodoDD)
        {
            if (string.IsNullOrEmpty(nodoDD))
                return string.Empty;

            nodoDD = Regex.Replace(nodoDD, @">\s+<", "><");
            nodoDD = nodoDD.Replace("\n", "").Replace("\r", "").Replace("\t", "").Trim();

            return nodoDD;
        }

        public static string ExtraerClavePrivada(string cafXml)
        {
            try
            {
                // Log snippet inicial de 300 caracteres
                string snippet = cafXml.Length > 300 ? cafXml.Substring(0, 300) : cafXml;
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_log.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ExtraerClavePrivada] Snippet inicial (300 chars): {snippet}\n",
                    Encoding.UTF8);

                // Blindar contra HTML
                SiiResponseGuard.DumpYThrowSiHtml(cafXml, "CAF_RSASK_RAW");

                // Normalizar XML del CAF
                string xml = SiiResponseGuard.NormalizarCafXml(cafXml);
                
                // Si hubo dump, registrar la ruta (ya está registrado en SiiResponseGuard)
                // Registrar que se procede con el XML normalizado
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_log.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ExtraerClavePrivada] XML normalizado, longitud: {xml.Length}\n",
                    Encoding.UTF8);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);
                XmlNode nodoClave = xmlDoc.SelectSingleNode("//RSASK");

                if (nodoClave == null || string.IsNullOrEmpty(nodoClave.InnerText))
                {
                    File.AppendAllText(@"C:\FacturasElectronicas\depuracion_general.txt",
                        "❌ [1] ERROR: No se encontró el nodo <RSASK> en el XML del CAF.\n");
                    return string.Empty;
                }

                string clavePrivada = nodoClave.InnerText.Trim();

                clavePrivada = clavePrivada
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Trim();

                string claveFormateada =
                    $"-----BEGIN RSA PRIVATE KEY-----\n{clavePrivada}\n-----END RSA PRIVATE KEY-----";

                string rutaDepuracion = @"C:\FacturasElectronicas\depuracion_general.txt";
                File.AppendAllText(rutaDepuracion,
                    "\n=== [2] Clave privada corregida ===\n" + claveFormateada + "\n");

                File.WriteAllText(@"C:\FacturasElectronicas\Depuracion_ClavePrivada.pem",
                    claveFormateada);

                if (new FileInfo(@"C:\FacturasElectronicas\Depuracion_ClavePrivada.pem").Length == 0)
                {
                    File.AppendAllText(rutaDepuracion,
                        "❌ [3] ERROR: La clave privada se guardó vacía. Verifica la extracción del XML.\n");
                    return string.Empty;
                }

                return claveFormateada;
            }
            catch (Exception ex)
            {
                File.AppendAllText(@"C:\FacturasElectronicas\depuracion_general.txt",
                    $"❌ [4] ERROR en ExtraerClavePrivada: {ex.Message}\n");
                return string.Empty;
            }
        }

        public static string ObtenerEjemploNodoDD()
        {
            return @"<DD>
    <RE>97975000-5</RE>
    <TD>33</TD>
    <F>27</F>
    <FE>2003-09-08</FE>
    <RR>8414240-9</RR>
    <RSR>JORGE GONZALEZ LTDA</RSR>
    <MNT>502946</MNT>
    <IT1>Cajon AFECTO</IT1>
    <CAF version='1.0'>
        <DA>
            <RE>97975000-5</RE>
            <RS>RUT DE PRUEBA</RS>
            <TD>33</TD>
            <RNG>
                <D>1</D>
                <H>200</H>
            </RNG>
            <FA>2003-09-04</FA>
            <RSAPK>
                <M>0a4O6Kbx8Qj3K4iWSP4w7KneZYeJ+g/prihYtIEolKt3cykSxl1zO8vSXu397QhTmsX7SBEudTUx++2zDXBhZw==</M>
                <E>Aw==</E>
            </RSAPK>
            <IDK>100</IDK>
        </DA>
        <FRMA algoritmo='SHA1withRSA'>g1AQX0sy8NJugX52k2hTJEZAE9Cuul6pqYBdFxj1N17umW7zG/hAavCALKByHzdYAfZ3LhGTXCai5zNxOo4lDQ==</FRMA>
    </CAF>
    <TSTED>2003-09-08T12:28:31</TSTED>
</DD>";
        }

        public static void CompararConEjemploSII(string nodoGenerado)
        {
            string nodoEjemplo = ObtenerEjemploNodoDD();

            if (nodoGenerado.Trim() == nodoEjemplo.Trim())
            {
                Console.WriteLine("✔ El nodo DD generado es idéntico al del SII.");
            }
            else
            {
                Console.WriteLine("⚠ ADVERTENCIA: El nodo DD generado NO coincide con el del SII.");
                File.WriteAllText(@"C:\FacturasElectronicas\ComparacionNodoDD.txt",
                    $"Nodo generado:\n{nodoGenerado}\n\nNodo oficial del SII:\n{nodoEjemplo}",
                    Encoding.UTF8);
            }
        }
    }
}
