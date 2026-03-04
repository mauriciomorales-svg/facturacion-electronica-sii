# Ejecutar Script para Insertar CAF Limpio

## ✅ XML del CAF Preparado

He preparado el script SQL `INSERTAR_CAF_FINAL.sql` con tu XML del CAF limpio y válido.

## Pasos para Ejecutar

1. **Abre tu cliente MySQL** (MySQL Workbench, phpMyAdmin, etc.)

2. **Selecciona la base de datos `dbisabel2`**

3. **Abre y ejecuta el archivo `INSERTAR_CAF_FINAL.sql`**

   El script:
   - ✅ Crea un backup automático
   - ✅ Valida el XML antes de insertar
   - ✅ Actualiza el CAF existente o inserta uno nuevo
   - ✅ Extrae los rangos de folios (1-60) del XML
   - ✅ Verifica que se insertó correctamente

4. **Verifica el resultado:**
   
   Deberías ver en la última consulta:
   - ✅ XML VÁLIDO Y LIMPIO - LISTO PARA USAR

## Después de Ejecutar el Script

1. **Reinicia el servicio** (detén con Ctrl+C y vuelve a iniciar con `dotnet run`)

2. **Prueba la emisión de DTE** nuevamente

3. **El error de parseo XML debería desaparecer** ✅

## Verificación Rápida

Si quieres verificar rápidamente que el CAF está limpio:

```sql
SELECT 
    ID,
    TD,
    RangoInicio,
    RangoFin,
    CASE 
        WHEN CAFContenido LIKE '%<?xml%' 
             AND CAFContenido LIKE '%</AUTORIZACION>%'
             AND CAFContenido NOT LIKE '%<td%'
             AND CAFContenido NOT LIKE '%<hr%'
        THEN '✅ LIMPIO'
        ELSE '❌ AÚN TIENE PROBLEMAS'
    END as Estado
FROM CAF 
WHERE TD = 33 AND TRIM(UPPER(Estado)) = 'ACTIVO';
```

## Nota

El script está listo para ejecutar. El XML que proporcionaste está limpio y válido, sin HTML corrupto.
