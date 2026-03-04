$b = "http://localhost:5030"
$t = (Get-Date).ToString("yyyy-MM-dd")

Write-Host "Consultando proximos folios disponibles..."
$f33 = (Invoke-RestMethod -Uri "$b/api/caf/proximo-folio/33" -Method Get)
$f61 = (Invoke-RestMethod -Uri "$b/api/caf/proximo-folio/61" -Method Get)
$f56 = (Invoke-RestMethod -Uri "$b/api/caf/proximo-folio/56" -Method Get)

$c1 = $f33; $c2 = $f33+1; $c3 = $f33+2; $c4 = $f33+3
$c5 = $f61; $c6 = $f61+1; $c7 = $f61+2
$c8 = $f56

Write-Host "Folios calculados: C1=$c1 C2=$c2 C3=$c3 C4=$c4 C5=$c5 C6=$c6 C7=$c7 C8=$c8"

$set = @(
    @{
        tipoDTE=33; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@(@{nombre="Cajon AFECTO";cantidad=132;precioUnitario=1406;exento=$false},@{nombre="Relleno AFECTO";cantidad=56;precioUnitario=2289;exento=$false})
    },
    @{
        tipoDTE=33; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@(@{nombre="Panuelo AFECTO";cantidad=333;precioUnitario=2668;descuentoPorcentaje=5;exento=$false},@{nombre="ITEM 2 AFECTO";cantidad=263;precioUnitario=1730;descuentoPorcentaje=9;exento=$false})
    },
    @{
        tipoDTE=33; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@(@{nombre="Pintura BW AFECTO";cantidad=28;precioUnitario=2949;exento=$false},@{nombre="ITEM 2 AFECTO";cantidad=165;precioUnitario=3109;exento=$false},@{nombre="ITEM 3 SERVICIO EXENTO";cantidad=1;precioUnitario=34815;exento=$true})
    },
    @{
        tipoDTE=33; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@(@{nombre="ITEM 1 AFECTO";cantidad=144;precioUnitario=2470;exento=$false},@{nombre="ITEM 2 AFECTO";cantidad=61;precioUnitario=2495;exento=$false},@{nombre="ITEM 3 SERVICIO EXENTO";cantidad=2;precioUnitario=6780;exento=$true})
        descuentoGlobalPorcentaje=9
    },
    @{
        tipoDTE=61; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO CORREGIDO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@()
        referencias=@(@{tipoDTE=33;folio=$c1;fecha=$t;codigoReferencia=2;razon="CORRIGE GIRO DEL RECEPTOR"})
    },
    @{
        tipoDTE=61; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@(@{nombre="Panuelo AFECTO";cantidad=122;precioUnitario=2668;descuentoPorcentaje=5;exento=$false},@{nombre="ITEM 2 AFECTO";cantidad=178;precioUnitario=1730;descuentoPorcentaje=9;exento=$false})
        referencias=@(@{tipoDTE=33;folio=$c2;fecha=$t;codigoReferencia=1;razon="DEVOLUCION DE MERCADERIAS"})
    },
    @{
        tipoDTE=61; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@(@{nombre="Pintura BW AFECTO";cantidad=28;precioUnitario=2949;exento=$false},@{nombre="ITEM 2 AFECTO";cantidad=165;precioUnitario=3109;exento=$false},@{nombre="ITEM 3 SERVICIO EXENTO";cantidad=1;precioUnitario=34815;exento=$true})
        referencias=@(@{tipoDTE=33;folio=$c3;fecha=$t;codigoReferencia=1;razon="ANULA FACTURA"})
    },
    @{
        tipoDTE=56; formaPago=1
        receptor=@{rut="66666666-6";razonSocial="CLIENTE CERTIFICACION SII";giro="COMERCIO";direccion="AVENIDA PRUEBA 123";comuna="SANTIAGO";ciudad="SANTIAGO"}
        detalles=@()
        referencias=@(@{tipoDTE=61;folio=$c5;fecha=$t;codigoReferencia=1;razon="ANULA NOTA DE CREDITO ELECTRONICA"})
    }
)

Write-Host "Enviando SET completo (8 DTEs en un solo EnvioDTE)..."
try {
    $resp = Invoke-RestMethod -Uri "$b/api/dte/emitir-set" -Method Post -ContentType "application/json" -Body ($set | ConvertTo-Json -Depth 10 -Compress)
    Write-Host "SET enviado exitosamente!"
    Write-Host "TrackID: $($resp.trackID)"
    Write-Host "Folios: $($resp.folios -join ', ')"
    Write-Host "Mensaje: $($resp.mensaje)"
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $error = $reader.ReadToEnd()
    Write-Host "ERROR: $error"
}
