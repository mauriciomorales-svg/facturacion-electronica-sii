# Prueba de Emisión de DTE

## Estado Actual

✅ **Conexión a MySQL funcionando**
✅ **CAF encontrado y leído correctamente**
✅ **60 folios disponibles (rango 1-60)**

## Próximo Paso: Probar Emisión de DTE

### Opción 1: Usar Swagger (Recomendado)

1. Abre el navegador en: `https://localhost:7295/swagger` o `http://localhost:5030/swagger`
2. Busca el endpoint `POST /api/DTE/emitir`
3. Haz clic en "Try it out"
4. Usa este JSON de ejemplo:

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

### Opción 2: Usar REST Client (VS Code)

1. Abre el archivo `test-emitir-dte.http`
2. Haz clic en "Send Request" sobre la línea del POST
3. Verás la respuesta en el panel lateral

### Opción 3: Usar PowerShell (cuando el servicio esté corriendo)

```powershell
$body = @{
    tipoDTE = 33
    receptor = @{
        rut = "60803000-K"
        razonSocial = "SERVICIO DE IMPUESTOS INTERNOS"
        giro = "Servicios públicos"
        direccion = "Teatinos 120"
        comuna = "Santiago"
        ciudad = "Santiago"
    }
    detalles = @(
        @{
            codigo = "PROD001"
            nombre = "Producto de prueba certificación"
            cantidad = 1
            precioUnitario = 10000
        }
    )
    formaPago = 1
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5030/api/DTE/emitir" -Method Post -Body $body -ContentType "application/json" | ConvertTo-Json -Depth 10
```

## Qué Esperar

Si todo funciona correctamente, deberías recibir una respuesta como:

```json
{
  "tipoDTE": 33,
  "folio": 1,
  "montoNeto": 10000,
  "iva": 1900,
  "montoTotal": 11900,
  "trackId": "...",
  "exito": true,
  "mensaje": "DTE emitido exitosamente",
  "xmlDTE": "...",
  "xmlEnvioDTE": "..."
}
```

## Si Hay Errores

Revisa los logs de la consola donde ejecutaste `dotnet run` para ver detalles del error.

Los errores comunes pueden ser:
- Problemas con el certificado digital
- Errores al firmar el documento
- Problemas de conexión con el SII
- Errores al parsear el CAF
