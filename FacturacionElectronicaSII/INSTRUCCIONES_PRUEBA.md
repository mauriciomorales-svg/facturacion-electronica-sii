# Instrucciones para Probar Emisión de DTE

## ⚠️ IMPORTANTE: Antes de Probar

1. **Verifica la estructura de tu base de datos**: El `CAFService` asume ciertos nombres de tablas/columnas. Si son diferentes, edita `Services/CAFService.cs`:
   - Línea ~66: Nombre de tabla y columnas del CAF
   - Línea ~120: Nombre de tabla de folios usados
   - Línea ~150: Nombre de tabla de folios usados

2. **Verifica que tengas CAFs en la BD**: Debe haber al menos un CAF activo para el tipo de DTE que quieres emitir.

3. **Verifica el certificado**: El certificado "Marta Inalbia Urra Escobar" debe estar instalado en Windows.

## Pasos Rápidos

### 1. Ejecutar el Servicio

```bash
cd FacturacionElectronicaSII
dotnet run
```

### 2. Abrir Swagger

Navega a: `https://localhost:7295/swagger`

### 3. Probar Emisión

1. Expande `POST /api/dte/emitir`
2. Click en "Try it out"
3. Pega este JSON:

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
      "nombre": "Producto de prueba",
      "cantidad": 1,
      "precioUnitario": 10000
    }
  ],
  "formaPago": 1
}
```

4. Click en "Execute"
5. Revisa la respuesta

## Respuesta Esperada

Si todo funciona, deberías recibir algo como:

```json
{
  "exito": true,
  "mensaje": "DTE emitido exitosamente",
  "tipoDTE": 33,
  "folio": 1,
  "fechaEmision": "2024-01-15T10:30:00",
  "montoNeto": 10000,
  "iva": 1900,
  "montoTotal": 11900,
  "trackID": "1234567",
  "estadoSII": "Enviado",
  "xmlBase64": "...",
  "errores": []
}
```

## Si Hay Errores

Revisa los logs en la consola. Los errores más comunes:

- **"No hay CAF disponible"**: Ajusta las consultas SQL en `CAFService.cs` o verifica que haya CAFs en la BD
- **"Certificado no encontrado"**: Verifica el nombre del certificado en `appsettings.json`
- **Error de conexión a BD**: Verifica la cadena de conexión en `appsettings.json`
