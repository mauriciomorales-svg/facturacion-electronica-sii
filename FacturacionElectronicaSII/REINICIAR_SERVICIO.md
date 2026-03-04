# Reiniciar el Servicio

## Pasos

1. **Abre una terminal/PowerShell** en la carpeta del proyecto:
   ```
   cd C:\Users\ComercioIsabel\source\repos\FacturacionElectronicaSII
   ```

2. **Ejecuta el servicio:**
   ```
   dotnet run
   ```

3. **Espera a que veas el mensaje:**
   ```
   Now listening on: https://localhost:7295
   ```

4. **Luego prueba la emisión de DTE** usando Swagger, Postman, o el comando PowerShell.

## Verificar que el Servicio Está Corriendo

Si ves este mensaje, el servicio está listo:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7295
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

## Nota

Después de limpiar el CAF en la base de datos, el servicio debería funcionar correctamente sin errores de parseo XML.
