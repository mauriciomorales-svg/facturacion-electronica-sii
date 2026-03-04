$b = "http://localhost:5030/api/dte"
$f = @{}
$t = (Get-Date).ToString("yyyy-MM-dd")

$f["c1"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body '{"tipoDTE":33,"receptor":{"rut":"66666666-6","razonSocial":"CLIENTE CERTIFICACION SII","giro":"COMERCIO","direccion":"AVENIDA PRUEBA 123","comuna":"SANTIAGO","ciudad":"SANTIAGO"},"detalles":[{"nombre":"Cajon AFECTO","cantidad":132,"precioUnitario":1406,"exento":false},{"nombre":"Relleno AFECTO","cantidad":56,"precioUnitario":2289,"exento":false}],"formaPago":1}').folio
Write-Host "CASO1 OK folio=$($f['c1'])"
Start-Sleep 1

$f["c2"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body '{"tipoDTE":33,"receptor":{"rut":"66666666-6","razonSocial":"CLIENTE CERTIFICACION SII","giro":"COMERCIO","direccion":"AVENIDA PRUEBA 123","comuna":"SANTIAGO","ciudad":"SANTIAGO"},"detalles":[{"nombre":"Panuelo AFECTO","cantidad":333,"precioUnitario":2668,"descuentoPorcentaje":5,"exento":false},{"nombre":"ITEM 2 AFECTO","cantidad":263,"precioUnitario":1730,"descuentoPorcentaje":9,"exento":false}],"formaPago":1}').folio
Write-Host "CASO2 OK folio=$($f['c2'])"
Start-Sleep 1

$f["c3"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body '{"tipoDTE":33,"receptor":{"rut":"66666666-6","razonSocial":"CLIENTE CERTIFICACION SII","giro":"COMERCIO","direccion":"AVENIDA PRUEBA 123","comuna":"SANTIAGO","ciudad":"SANTIAGO"},"detalles":[{"nombre":"Pintura BW AFECTO","cantidad":28,"precioUnitario":2949,"exento":false},{"nombre":"ITEM 2 AFECTO","cantidad":165,"precioUnitario":3109,"exento":false},{"nombre":"ITEM 3 SERVICIO EXENTO","cantidad":1,"precioUnitario":34815,"exento":true}],"formaPago":1}').folio
Write-Host "CASO3 OK folio=$($f['c3'])"
Start-Sleep 1

$f["c4"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body '{"tipoDTE":33,"receptor":{"rut":"66666666-6","razonSocial":"CLIENTE CERTIFICACION SII","giro":"COMERCIO","direccion":"AVENIDA PRUEBA 123","comuna":"SANTIAGO","ciudad":"SANTIAGO"},"detalles":[{"nombre":"ITEM 1 AFECTO","cantidad":144,"precioUnitario":2470,"exento":false},{"nombre":"ITEM 2 AFECTO","cantidad":61,"precioUnitario":2495,"exento":false},{"nombre":"ITEM 3 SERVICIO EXENTO","cantidad":2,"precioUnitario":6780,"exento":true}],"descuentoGlobalPorcentaje":9,"formaPago":1}').folio
Write-Host "CASO4 OK folio=$($f['c4'])"
Start-Sleep 1

$body5 = "{`"tipoDTE`":61,`"receptor`":{`"rut`":`"66666666-6`",`"razonSocial`":`"CLIENTE CERTIFICACION SII`",`"giro`":`"COMERCIO CORREGIDO`",`"direccion`":`"AVENIDA PRUEBA 123`",`"comuna`":`"SANTIAGO`",`"ciudad`":`"SANTIAGO`"},`"detalles`":[],`"referencias`":[{`"tipoDTE`":33,`"folio`":$($f['c1']),`"fecha`":`"$t`",`"codigoReferencia`":2,`"razon`":`"CORRIGE GIRO DEL RECEPTOR`"}],`"formaPago`":1}"
$f["c5"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body $body5).folio
Write-Host "CASO5 OK folio=$($f['c5'])"
Start-Sleep 1

$body6 = "{`"tipoDTE`":61,`"receptor`":{`"rut`":`"66666666-6`",`"razonSocial`":`"CLIENTE CERTIFICACION SII`",`"giro`":`"COMERCIO`",`"direccion`":`"AVENIDA PRUEBA 123`",`"comuna`":`"SANTIAGO`",`"ciudad`":`"SANTIAGO`"},`"detalles`":[{`"nombre`":`"Panuelo AFECTO`",`"cantidad`":122,`"precioUnitario`":2668,`"descuentoPorcentaje`":5,`"exento`":false},{`"nombre`":`"ITEM 2 AFECTO`",`"cantidad`":178,`"precioUnitario`":1730,`"descuentoPorcentaje`":9,`"exento`":false}],`"referencias`":[{`"tipoDTE`":33,`"folio`":$($f['c2']),`"fecha`":`"$t`",`"codigoReferencia`":1,`"razon`":`"DEVOLUCION DE MERCADERIAS`"}],`"formaPago`":1}"
$f["c6"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body $body6).folio
Write-Host "CASO6 OK folio=$($f['c6'])"
Start-Sleep 1

$body7 = "{`"tipoDTE`":61,`"receptor`":{`"rut`":`"66666666-6`",`"razonSocial`":`"CLIENTE CERTIFICACION SII`",`"giro`":`"COMERCIO`",`"direccion`":`"AVENIDA PRUEBA 123`",`"comuna`":`"SANTIAGO`",`"ciudad`":`"SANTIAGO`"},`"detalles`":[{`"nombre`":`"Pintura BW AFECTO`",`"cantidad`":28,`"precioUnitario`":2949,`"exento`":false},{`"nombre`":`"ITEM 2 AFECTO`",`"cantidad`":165,`"precioUnitario`":3109,`"exento`":false},{`"nombre`":`"ITEM 3 SERVICIO EXENTO`",`"cantidad`":1,`"precioUnitario`":34815,`"exento`":true}],`"referencias`":[{`"tipoDTE`":33,`"folio`":$($f['c3']),`"fecha`":`"$t`",`"codigoReferencia`":1,`"razon`":`"ANULA FACTURA`"}],`"formaPago`":1}"
$f["c7"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body $body7).folio
Write-Host "CASO7 OK folio=$($f['c7'])"
Start-Sleep 1

$body8 = "{`"tipoDTE`":56,`"receptor`":{`"rut`":`"66666666-6`",`"razonSocial`":`"CLIENTE CERTIFICACION SII`",`"giro`":`"COMERCIO`",`"direccion`":`"AVENIDA PRUEBA 123`",`"comuna`":`"SANTIAGO`",`"ciudad`":`"SANTIAGO`"},`"detalles`":[],`"referencias`":[{`"tipoDTE`":61,`"folio`":$($f['c5']),`"fecha`":`"$t`",`"codigoReferencia`":1,`"razon`":`"ANULA NOTA DE CREDITO ELECTRONICA`"}],`"formaPago`":1}"
$f["c8"] = (Invoke-RestMethod -Uri "$b/emitir" -Method Post -ContentType "application/json" -Body $body8).folio
Write-Host "CASO8 OK folio=$($f['c8'])"

Write-Host "`nRESUMEN: C1=$($f['c1']) C2=$($f['c2']) C3=$($f['c3']) C4=$($f['c4']) C5=$($f['c5']) C6=$($f['c6']) C7=$($f['c7']) C8=$($f['c8'])"
