using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Models.CAF;
using FacturacionElectronicaSII.Models;

namespace FacturacionElectronicaSII.Services.Mock
{
    /// <summary>
    /// Implementación Mock del servicio CAF para desarrollo
    /// </summary>
    public class MockCAFService : ICAFService
    {
        private readonly ILogger<MockCAFService> _logger;
        private readonly Dictionary<int, HashSet<int>> _foliosUsados = new();
        private readonly Dictionary<int, CAFData> _cafsCache = new();

        public MockCAFService(ILogger<MockCAFService> logger)
        {
            _logger = logger;
            InicializarCAFsMock();
        }

        private void InicializarCAFsMock()
        {
            // CAF Mock para Factura (33)
            _cafsCache[33] = new CAFData
            {
                RutEmisor = DatosPrueba.RutEmisor,
                RazonSocial = DatosPrueba.RazonSocialEmisor,
                TipoDTE = 33,
                FolioInicial = 1,
                FolioFinal = 100,
                FechaAutorizacion = DateTime.Parse(DatosPrueba.FchResol),
                ModuloRSA = "BASE64_MODULUS_MOCK",
                ExponenteRSA = "Aw==",
                IdK = 100,
                ClavePrivadaPEM = "-----BEGIN RSA PRIVATE KEY-----\nCLAVE_PRIVADA_MOCK\n-----END RSA PRIVATE KEY-----",
                XMLOriginal = DatosPrueba.CAFPrueba
            };

            // CAF Mock para Boleta (39)
            _cafsCache[39] = new CAFData
            {
                RutEmisor = DatosPrueba.RutEmisor,
                RazonSocial = DatosPrueba.RazonSocialEmisor,
                TipoDTE = 39,
                FolioInicial = 1,
                FolioFinal = 100,
                FechaAutorizacion = DateTime.Parse(DatosPrueba.FchResol),
                ModuloRSA = "BASE64_MODULUS_MOCK",
                ExponenteRSA = "Aw==",
                IdK = 100,
                ClavePrivadaPEM = "-----BEGIN RSA PRIVATE KEY-----\nCLAVE_PRIVADA_MOCK\n-----END RSA PRIVATE KEY-----",
                XMLOriginal = DatosPrueba.CAFPrueba
            };

            _foliosUsados[33] = new HashSet<int>();
            _foliosUsados[39] = new HashSet<int>();
        }

        public Task<int> ObtenerFolioDisponibleAsync(int tipoDTE)
        {
            _logger.LogInformation("Mock: Obteniendo folio disponible para tipo DTE {TipoDTE}", tipoDTE);

            if (!_cafsCache.ContainsKey(tipoDTE))
            {
                throw new InvalidOperationException($"No hay CAF disponible para tipo DTE {tipoDTE}");
            }

            var caf = _cafsCache[tipoDTE];
            var foliosUsados = _foliosUsados.GetValueOrDefault(tipoDTE, new HashSet<int>());

            for (int folio = caf.FolioInicial; folio <= caf.FolioFinal; folio++)
            {
                if (!foliosUsados.Contains(folio))
                {
                    _logger.LogInformation("Mock: Folio {Folio} disponible para tipo DTE {TipoDTE}", folio, tipoDTE);
                    return Task.FromResult(folio);
                }
            }

            throw new InvalidOperationException($"No hay folios disponibles para tipo DTE {tipoDTE}");
        }

        public Task<CAFData?> ObtenerCAFAsync(int tipoDTE)
        {
            _logger.LogInformation("Mock: Obteniendo CAF para tipo DTE {TipoDTE}", tipoDTE);
            _cafsCache.TryGetValue(tipoDTE, out var caf);
            return Task.FromResult(caf);
        }

        public Task<bool> MarcarFolioUsadoAsync(int tipoDTE, int folio)
        {
            _logger.LogInformation("Mock: Marcando folio {Folio} como usado para tipo DTE {TipoDTE}", folio, tipoDTE);
            
            if (!_foliosUsados.ContainsKey(tipoDTE))
            {
                _foliosUsados[tipoDTE] = new HashSet<int>();
            }

            _foliosUsados[tipoDTE].Add(folio);
            return Task.FromResult(true);
        }

        public Task<int> FoliosDisponiblesAsync(int tipoDTE)
        {
            if (!_cafsCache.ContainsKey(tipoDTE))
            {
                return Task.FromResult(0);
            }

            var caf = _cafsCache[tipoDTE];
            var foliosUsados = _foliosUsados.GetValueOrDefault(tipoDTE, new HashSet<int>());
            var disponibles = (caf.FolioFinal - caf.FolioInicial + 1) - foliosUsados.Count;

            return Task.FromResult(disponibles);
        }
    }
}
