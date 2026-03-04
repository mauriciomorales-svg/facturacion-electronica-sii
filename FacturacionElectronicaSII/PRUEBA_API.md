# Prueba de Emisión de DTE

## Petición Válida para Swagger/Postman

Usa este JSON en lugar del ejemplo por defecto:

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
      "precioUnitario": 10000,
      "descuentoPorcentaje": 0
    }
  ],
  "formaPago": 1
}
```

## Validaciones Requeridas

- **tipoDTE**: Debe ser `33` (Factura), `39` (Boleta), `61` (NC) o `56` (ND)
- **cantidad**: Debe ser mayor a 0
- **precioUnitario**: Debe ser mayor a 0
- **rut**: Debe tener formato válido (ej: "60803000-K")
- **razonSocial**: No puede estar vacío

## cURL Ejemplo

```bash
curl -X 'POST' \
  'https://localhost:7295/api/DTE/emitir' \
  -H 'accept: application/json' \
  -H 'Content-Type: application/json' \
  -d '{
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
      "precioUnitario": 10000,
      "descuentoPorcentaje": 0
    }
  ],
  "formaPago": 1
}'
```

## PowerShell Ejemplo

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
            descuentoPorcentaje = 0
        }
    )
    formaPago = 1
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "https://localhost:7295/api/DTE/emitir" -Method Post -Body $body -ContentType "application/json"
```
