namespace FacturacionElectronicaSII.Models
{
    /// <summary>
    /// Datos de prueba para desarrollo sin conexión real al SII
    /// </summary>
    public static class DatosPrueba
    {
        // EMISOR (datos ficticios para desarrollo)
        public const string RutEmisor = "76123456-7";
        public const string RazonSocialEmisor = "EMPRESA DE PRUEBA SPA";
        public const string GiroEmisor = "Venta al por menor de productos varios";
        public const string DireccionEmisor = "Av. Principal 123";
        public const string ComunaEmisor = "Santiago";
        public const string CiudadEmisor = "Santiago";
        public const string Acteco = "479100";
        
        // RESOLUCIÓN (ambiente certificación Maullin)
        public const string FchResol = "2024-01-15";  // Fecha ficticia
        public const string NroResol = "0";           // Cero para certificación
        
        // RECEPTOR SII (certificación)
        public const string RutReceptorSII = "60803000-K";
        
        // CAF DE PRUEBA (simular estructura, no es válido para SII real)
        public static string CAFPrueba => @"<?xml version=""1.0""?>
<AUTORIZACION>
    <CAF version=""1.0"">
        <DA>
            <RE>76123456-7</RE>
            <RS>EMPRESA DE PRUEBA SPA</RS>
            <TD>33</TD>
            <RNG><D>1</D><H>100</H></RNG>
            <FA>2024-01-15</FA>
            <RSAPK>
                <M>BASE64_MODULUS_MOCK</M>
                <E>Aw==</E>
            </RSAPK>
            <IDK>100</IDK>
        </DA>
        <FRMA algoritmo=""SHA1withRSA"">FIRMA_MOCK_BASE64</FRMA>
    </CAF>
    <RSASK>-----BEGIN RSA PRIVATE KEY-----
CLAVE_PRIVADA_MOCK
-----END RSA PRIVATE KEY-----</RSASK>
</AUTORIZACION>";

        // SEMILLA Y TOKEN MOCK (para pruebas sin conexión)
        public const string SemillaMock = "123456789012";
        public const string TokenMock = "TOKEN_MOCK_PARA_PRUEBAS_DESARROLLO";
    }
}
