# Prueba de Emisión de DTE en Certificación

## Pasos para Probar

### 1. Verificar Configuración

Asegúrate de que `appsettings.json` tenga:
- ✅ `Ambiente: "Certificacion"`
- ✅ `Certificado.Nombre: "Marta Inalbia Urra Escobar"`
- ✅ Cadena de conexión a `dbisabel2` configurada
- ✅ Datos del emisor correctos

### 2. Verificar Base de Datos

Asegúrate de que en `dbisabel2` existan:
- Tabla con los CAFs (ajustar nombres en `CAFService.cs` si es necesario)
- Al menos un CAF activo para el tipo de DTE que vas a emitir (33=Factura, 39=Boleta)

### 3. Ejecutar el Servicio

```bash
dotnet run
```

El servicio estará disponible en:
- **HTTPS**: `https://localhost:7295`
- **HTTP**: `http://localhost:5030`
- **Swagger**: `https://localhost:7295/swagger`

### 4. Probar Emisión de DTE

#### Opción A: Usando Swagger UI

1. Abre el navegador en: `https://localhost:7295/swagger`
2. Expande el endpoint `POST /api/dte/emitir`
3. Haz clic en "Try it out"
4. Pega el siguiente JSON en el body:

```json
{
  "tipoDTE": 33,
  "receptor": {
    "rut": "60803000-K",
    "razonSocial": "SERVICIO DE IMPUESTOS INTERNOS",
    "giro": "Servicios públicos",
    "direccion": "Teatinos 120",
    "comuna": "Santiago",
    "ciudad": "Santiago"
  },
  "detalles": [
    {
      "codigo": "PROD001",
      "nombre": "Producto de prueba certificación",
      "cantidad": 1,
      "precioUnitario": 10000
    }
  ],
  "formaPago": 1
}
```

5. Haz clic en "Execute"
6. Revisa la respuesta

#### Opción B: Usando cURL

```bash
curl -X POST "https://localhost:7295/api/dte/emitir" \
  -H "Content-Type: application/json" \
  -d @test-emitir-dte.json \
  -k
```

#### Opción C: Usando PowerShell

```powershell
$body = Get-Content test-emitir-dte.json -Raw
Invoke-RestMethod -Uri "https://localhost:7295/api/dte/emitir" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body `
  -SkipCertificateCheck
```

### 5. Verificar Resultado

La respuesta debería incluir:
- `exito: true`
- `folio`: Número de folio asignado
- `trackID`: ID de seguimiento del SII
- `estadoSII`: Estado del envío
- `xmlBase64`: XML del DTE en base64

### 6. Consultar Estado

Usa el `trackID` recibido para consultar el estado:

```bash
GET https://localhost:7295/api/estado/{trackID}
```

## Posibles Errores y Soluciones

### Error: "No hay CAF disponible"
- **Causa**: No hay CAF activo en la base de datos para ese tipo de DTE
- **Solución**: Verificar que exista un CAF activo en la tabla correspondiente

### Error: "Certificado no encontrado"
- **Causa**: El certificado no está instalado o el nombre no coincide
- **Solución**: Verificar que el certificado "Marta Inalbia Urra Escobar" esté instalado en el almacén de certificados de Windows (CurrentUser > Personal)

### Error: "Error al obtener semilla del SII"
- **Causa**: Problema de conexión con Maullin
- **Solución**: Verificar conectividad a internet y que las URLs de certificación estén correctas

### Error: "Error al parsear CAF"
- **Causa**: El XML del CAF en la BD está mal formado
- **Solución**: Verificar que el XML del CAF sea válido

## Logs

Revisa los logs en la consola para ver el detalle de cada paso:
- Obtención de folio
- Generación de TED
- Construcción de XML
- Firma del documento
- Envío al SII
