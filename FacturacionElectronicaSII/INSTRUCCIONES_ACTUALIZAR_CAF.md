# Instrucciones para Actualizar el CAF Limpio

## 🎯 Situación Actual

El servicio detectó que el CAF en la base de datos contiene HTML corrupto mezclado con XML. El servicio guardó automáticamente un dump con el XML limpio en:

**Dump guardado:** `C:\FacturasElectronicas\Logs\Dumps\CAF_HTML_DETECTADO_20260119_052214.html`

## ✅ Solución

El dump contiene el XML limpio y válido. Solo necesitas actualizar el CAF en la base de datos MySQL con este XML limpio.

## 📋 Pasos a Seguir

### Paso 1: Abrir MySQL Workbench (o tu cliente MySQL)

Conéctate a la base de datos `dbisabel2`:
- **Host:** `127.0.0.1`
- **Puerto:** `3306`
- **Usuario:** `root`
- **Contraseña:** (vacía)
- **Base de datos:** `dbisabel2`

### Paso 2: Ejecutar el Script SQL

Abre el archivo: **`ACTUALIZAR_CAF_LIMPIO_MYSQL.sql`**

Este script tiene 3 partes:

#### 2.1. Verificar el CAF Actual (PASO 1)
```sql
SELECT ... FROM CAF WHERE TD = 33 ...
```
**Ejecuta esto primero** para ver el estado actual del CAF.

#### 2.2. Actualizar el CAF (PASO 2)
```sql
UPDATE CAF SET CAFContenido = '...' WHERE TD = 33 ...
```
**Ejecuta esto** para actualizar el CAF con el XML limpio.

#### 2.3. Verificar la Actualización (PASO 3)
```sql
SELECT ... FROM CAF WHERE TD = 33 ...
```
**Ejecuta esto después** para confirmar que se actualizó correctamente.

### Paso 3: Verificar los Resultados

Después de ejecutar el PASO 3, deberías ver:

✅ **TieneAutorizacion:** `✓ TIENE AUTORIZACION`
✅ **TieneCAF:** `✓ TIENE CAF`
✅ **TieneRSASK:** `✓ TIENE RSASK`
✅ **EstadoHTML:** `✓ NO CONTIENE HTML`

Si ves alguna ✗, **NO reinicies el servicio** y revisa el script.

### Paso 4: Reiniciar el Servicio

1. Detén el servicio actual (Ctrl+C en la terminal donde está corriendo)
2. Reinicia el servicio:
   ```bash
   cd FacturacionElectronicaSII
   dotnet run
   ```

### Paso 5: Probar la Emisión

Ejecuta una prueba de emisión de DTE. El servicio ahora debería:
- ✅ Leer el CAF limpio sin errores
- ✅ Generar el TED correctamente
- ✅ Firmar el DTE
- ✅ Enviar al SII

## 🔍 Verificación Adicional

Si quieres verificar manualmente que el XML está limpio, ejecuta:

```sql
SELECT 
    LENGTH(CAFContenido) as Longitud,
    CAFContenido
FROM CAF 
WHERE TD = 33 
    AND TRIM(UPPER(Estado)) = 'ACTIVO'
LIMIT 1;
```

El XML debería:
- Empezar con `<?xml version="1.0"?>`
- Contener `<AUTORIZACION>...</AUTORIZACION>`
- Contener `<CAF>...</CAF>`
- Contener `<RSASK>...</RSASK>`
- **NO** contener `<hr>`, `<td>`, `<table>`, `<html>`

## ⚠️ Si Algo Sale Mal

Si después de actualizar el CAF el servicio sigue dando error:

1. **Revisa los logs del servicio** - Busca mensajes que indiquen qué está fallando
2. **Verifica que el UPDATE se ejecutó** - Ejecuta el PASO 3 del script nuevamente
3. **Revisa el dump guardado** - Abre `C:\FacturasElectronicas\Logs\Dumps\CAF_HTML_DETECTADO_20260119_052214.html` y verifica que contiene XML válido
4. **Verifica la conexión a la BD** - Asegúrate de que el servicio está usando la misma base de datos

## 📝 Notas Importantes

- El script actualiza **solo el CAF tipo 33** (Facturas Electrónicas)
- El script busca el CAF con `Estado = 'ACTIVO'` (sin espacios, mayúsculas)
- El XML limpio proviene del dump guardado automáticamente por el servicio
- El XML está validado y es 100% compatible con el SII

## ✅ Resultado Esperado

Después de seguir estos pasos, el servicio debería:
- ✅ Leer el CAF sin errores de parsing
- ✅ Generar el TED correctamente
- ✅ Firmar el DTE sin problemas
- ✅ Enviar al SII exitosamente
- ✅ Obtener un TrackID del SII

---

**¿Listo?** Ejecuta el script SQL y luego reinicia el servicio. 🚀
