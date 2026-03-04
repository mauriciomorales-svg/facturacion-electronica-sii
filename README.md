# Servicio de Facturación Electrónica SII

Servicio independiente de facturación electrónica para Chile (SII) desarrollado en C# (.NET 8.0).

## Características

- ✅ Emisión de DTEs (Facturas 33, Boletas 39, NC 61, ND 56)
- ✅ Generación de XMLs según formato SII
- ✅ Firma digital de documentos
- ✅ Comunicación con SII (Maullin/Palena)
- ✅ Modo MOCK para desarrollo sin conexión real
- ✅ API REST consumible por cualquier sistema POS

## Estructura del Proyecto

```
FacturacionElectronicaSII/
├── Controllers/          # Endpoints de la API
│   ├── DTEController.cs
│   ├── CAFController.cs
│   └── EstadoController.cs
├── Services/            # Servicios Core
│   ├── DTEService.cs
│   ├── TEDService.cs
│   ├── XMLBuilderService.cs
│   └── Mock/           # Implementaciones Mock
│       ├── MockSIIService.cs
│       ├── MockCAFService.cs
│       └── MockFirmaService.cs
├── Interfaces/          # Interfaces de servicios
├── Models/              # Modelos de datos
│   ├── DTO/
│   ├── DTE/
│   ├── CAF/
│   └── Enums/
└── Program.cs          # Configuración e inyección de dependencias
```

## Requisitos

- .NET 8.0 SDK
- Visual Studio 2022 o VS Code

## Configuración

El archivo `appsettings.json` contiene la configuración del servicio:

```json
{
  "FacturacionElectronica": {
    "Ambiente": "Mock",  // "Mock", "Certificacion", "Produccion"
    "Emisor": {
      "RUT": "76123456-7",
      "RazonSocial": "EMPRESA DE PRUEBA SPA",
      ...
    }
  }
}
```

## Uso

### 1. Ejecutar el servicio

```bash
dotnet run
```

El servicio estará disponible en:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger`

### 2. Emitir un DTE

**POST** `/api/dte/emitir`

```json
{
  "tipoDTE": 33,
  "receptor": {
    "rut": "12345678-9",
    "razonSocial": "CLIENTE DE PRUEBA",
    "giro": "Comercio",
    "direccion": "Calle Falsa 123",
    "comuna": "Santiago",
    "ciudad": "Santiago"
  },
  "detalles": [
    {
      "codigo": "PROD001",
      "nombre": "Producto de prueba",
      "cantidad": 2,
      "precioUnitario": 10000
    }
  ],
  "formaPago": 1
}
```

**Respuesta:**

```json
{
  "exito": true,
  "mensaje": "DTE emitido exitosamente",
  "tipoDTE": 33,
  "folio": 1,
  "fechaEmision": "2024-01-15T10:30:00",
  "montoNeto": 20000,
  "iva": 3800,
  "montoTotal": 23800,
  "trackID": "1000001",
  "estadoSII": "Enviado",
  "xmlBase64": "...",
  "timbreBase64": "MOCK_TIMBRE_BASE64"
}
```

### 3. Consultar estado de envío

**GET** `/api/estado/{trackId}`

### 4. Consultar folios disponibles

**GET** `/api/caf/folios-disponibles/{tipoDTE}`

## Modo Mock

El servicio está configurado por defecto en modo **Mock**, lo que permite:

- ✅ Probar el flujo completo sin conexión al SII real
- ✅ No requiere certificados digitales reales
- ✅ No requiere CAFs reales del SII
- ✅ Simula respuestas exitosas del SII

Para cambiar a modo real, actualiza `appsettings.json`:

```json
{
  "FacturacionElectronica": {
    "Ambiente": "Certificacion"  // o "Produccion"
  }
}
```

## Endpoints Disponibles

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| POST | `/api/dte/emitir` | Emite un DTE |
| GET | `/api/dte/estado/{trackId}` | Consulta estado de envío |
| GET | `/api/dte/pdf/{tipoDTE}/{folio}` | Genera PDF del DTE |
| GET | `/api/caf/folios-disponibles/{tipoDTE}` | Folios disponibles |
| GET | `/api/caf/{tipoDTE}` | Información del CAF |
| GET | `/api/estado/{trackId}` | Estado de envío al SII |

## Tipos de DTE

- **33**: Factura Electrónica
- **39**: Boleta Electrónica
- **61**: Nota de Crédito
- **56**: Nota de Débito

## Próximos Pasos

- [ ] Implementar servicios reales para Certificación y Producción
- [ ] Generación de PDFs con timbre PDF417
- [ ] Persistencia en base de datos
- [ ] Validación avanzada de datos
- [ ] Tests unitarios e integración

## Licencia

Este proyecto es de uso interno.
