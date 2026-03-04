using FacturacionElectronicaSII.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Xml;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.PDF417;

namespace FacturacionElectronicaSII.Services
{
    /// <summary>
    /// Servicio para generación de PDFs de DTEs según protocolo SII
    /// </summary>
    public class PDFService : IPDFService
    {
        private readonly ILogger<PDFService> _logger;
        private readonly IConfiguration _configuration;

        public PDFService(ILogger<PDFService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Configurar QuestPDF con licencia Community
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<(string rutaPDF, byte[] pdfBytes)> GenerarPDFAsync(string xmlDTE, string? folderOutput = null)
        {
            try
            {
                _logger.LogInformation("Generando PDF de DTE");

                // Parsear XML
                var doc = new XmlDocument();
                doc.LoadXml(xmlDTE);

                // Extraer datos del documento
                var datos = ExtraerDatosDelXML(doc);

                // Generar código PDF417 del TED
                byte[] imagenPDF417 = Array.Empty<byte>();
                try
                {
                    var tedNode = doc.SelectSingleNode("//*[local-name()='TED']");
                    if (tedNode != null)
                    {
                        imagenPDF417 = await GenerarPDF417Async(tedNode.OuterXml);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo generar código PDF417");
                }

                // Generar PDF con QuestPDF
                var pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.Letter);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Element(c => CrearEncabezado(c, datos));

                        page.Content()
                            .Element(c => CrearContenido(c, datos));

                        page.Footer()
                            .Element(c => CrearPie(c, datos, imagenPDF417));
                    });
                })
                .GeneratePdf();

                // Guardar archivo
                var rutaPDFs = folderOutput ?? Path.Combine(
                    Directory.GetCurrentDirectory(),
                    _configuration["FacturacionElectronica:Rutas:PDFs"] ?? "./Data/PDFs"
                );

                Directory.CreateDirectory(rutaPDFs);

                var nombreArchivo = $"DTE_T{datos.TipoDTE}_F{datos.Folio}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                var rutaCompleta = Path.Combine(rutaPDFs, nombreArchivo);

                await File.WriteAllBytesAsync(rutaCompleta, pdfBytes);

                _logger.LogInformation("PDF generado: {Ruta}", rutaCompleta);

                return (rutaCompleta, pdfBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF");
                throw;
            }
        }

        public async Task<byte[]> GenerarPDF417Async(string tedXML)
        {
            try
            {
                // Limpiar el XML del TED (sin espacios extras ni saltos de línea)
                var tedLimpio = tedXML.Replace("\r\n", "").Replace("\n", "").Replace("\t", "");

                // Configurar writer PDF417
                var writer = new BarcodeWriterPixelData
                {
                    Format = BarcodeFormat.PDF_417,
                    Options = new EncodingOptions
                    {
                        Height = 100,
                        Width = 300,
                        Margin = 2,
                        PureBarcode = true
                    }
                };

                // Generar código de barras
                var pixelData = writer.Write(tedLimpio);

                // Convertir a PNG
                using (var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    var bitmapData = bitmap.LockBits(
                        new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                    try
                    {
                        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }

                    using (var ms = new MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        return await Task.FromResult(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF417");
                return Array.Empty<byte>();
            }
        }

        private DatosDTE ExtraerDatosDelXML(XmlDocument doc)
        {
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("sii", "http://www.sii.cl/SiiDte");

            var datos = new DatosDTE
            {
                TipoDTE = int.Parse(doc.SelectSingleNode("//sii:TipoDTE", nsmgr)?.InnerText ?? "33"),
                Folio = int.Parse(doc.SelectSingleNode("//sii:Folio", nsmgr)?.InnerText ?? "0"),
                FechaEmision = doc.SelectSingleNode("//sii:FchEmis", nsmgr)?.InnerText ?? "",

                RutEmisor = doc.SelectSingleNode("//sii:RUTEmisor", nsmgr)?.InnerText ?? "",
                RazonSocialEmisor = doc.SelectSingleNode("//sii:RznSoc", nsmgr)?.InnerText ?? "",
                GiroEmisor = doc.SelectSingleNode("//sii:GiroEmis", nsmgr)?.InnerText ?? "",
                DireccionEmisor = doc.SelectSingleNode("//sii:DirOrigen", nsmgr)?.InnerText ?? "",
                ComunaEmisor = doc.SelectSingleNode("//sii:CmnaOrigen", nsmgr)?.InnerText ?? "",

                RutReceptor = doc.SelectSingleNode("//sii:RUTRecep", nsmgr)?.InnerText ?? "",
                RazonSocialReceptor = doc.SelectSingleNode("//sii:RznSocRecep", nsmgr)?.InnerText ?? "",
                DireccionReceptor = doc.SelectSingleNode("//sii:DirRecep", nsmgr)?.InnerText ?? "",
                ComunaReceptor = doc.SelectSingleNode("//sii:CmnaRecep", nsmgr)?.InnerText ?? "",

                MontoNeto = int.Parse(doc.SelectSingleNode("//sii:MntNeto", nsmgr)?.InnerText ?? "0"),
                MontoExento = int.Parse(doc.SelectSingleNode("//sii:MntExe", nsmgr)?.InnerText ?? "0"),
                IVA = int.Parse(doc.SelectSingleNode("//sii:IVA", nsmgr)?.InnerText ?? "0"),
                MontoTotal = int.Parse(doc.SelectSingleNode("//sii:MntTotal", nsmgr)?.InnerText ?? "0")
            };

            // Extraer detalles
            var detalles = doc.SelectNodes("//sii:Detalle", nsmgr);
            if (detalles != null)
            {
                foreach (XmlNode detalle in detalles)
                {
                    datos.Detalles.Add(new DetalleItem
                    {
                        Nombre = detalle.SelectSingleNode("sii:NmbItem", nsmgr)?.InnerText ?? "",
                        Cantidad = decimal.Parse(detalle.SelectSingleNode("sii:QtyItem", nsmgr)?.InnerText ?? "0"),
                        Precio = int.Parse(detalle.SelectSingleNode("sii:PrcItem", nsmgr)?.InnerText ?? "0"),
                        Total = int.Parse(detalle.SelectSingleNode("sii:MontoItem", nsmgr)?.InnerText ?? "0")
                    });
                }
            }

            return datos;
        }

        private void CrearEncabezado(IContainer container, DatosDTE datos)
        {
            container.Row(row =>
            {
                // Columna izquierda: Datos emisor
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(datos.RazonSocialEmisor).FontSize(14).Bold();
                    col.Item().Text($"RUT: {datos.RutEmisor}").FontSize(10);
                    col.Item().Text(datos.GiroEmisor).FontSize(9);
                    col.Item().Text(datos.DireccionEmisor).FontSize(9);
                    col.Item().Text(datos.ComunaEmisor).FontSize(9);
                });

                // Columna derecha: Tipo y folio
                row.ConstantItem(200).Border(1).Padding(10).Column(col =>
                {
                    var tipoDoc = datos.TipoDTE switch
                    {
                        33 => "FACTURA ELECTRÓNICA",
                        61 => "NOTA DE CRÉDITO ELECTRÓNICA",
                        56 => "NOTA DE DÉBITO ELECTRÓNICA",
                        39 => "BOLETA ELECTRÓNICA",
                        _ => "DOCUMENTO TRIBUTARIO"
                    };

                    col.Item().AlignCenter().Text(tipoDoc).FontSize(12).Bold();
                    col.Item().AlignCenter().Text($"N° {datos.Folio}").FontSize(14).Bold();
                });
            });
        }

        private void CrearContenido(IContainer container, DatosDTE datos)
        {
            container.Column(col =>
            {
                // Datos receptor
                col.Item().PaddingVertical(10).Column(c =>
                {
                    c.Item().Text("DATOS DEL RECEPTOR").FontSize(11).Bold();
                    c.Item().Text($"Señor(es): {datos.RazonSocialReceptor}");
                    c.Item().Text($"RUT: {datos.RutReceptor}");
                    c.Item().Text($"Dirección: {datos.DireccionReceptor}, {datos.ComunaReceptor}");
                    c.Item().Text($"Fecha: {datos.FechaEmision}");
                });

                // Tabla de detalles
                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(60);  // Cantidad
                        columns.RelativeColumn();    // Descripción
                        columns.ConstantColumn(80);  // P. Unit
                        columns.ConstantColumn(100); // Total
                    });

                    // Encabezado
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Cant.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Descripción").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("P. Unit.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Total").Bold();
                    });

                    // Filas de detalles
                    foreach (var item in datos.Detalles)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.Cantidad.ToString("N0"));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.Nombre);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"${item.Precio:N0}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"${item.Total:N0}");
                    }
                });

                // Totales
                col.Item().PaddingTop(10).AlignRight().Column(c =>
                {
                    if (datos.MontoNeto > 0)
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().Width(120).Text("NETO:");
                            r.AutoItem().Width(100).AlignRight().Text($"${datos.MontoNeto:N0}");
                        });
                    }

                    if (datos.MontoExento > 0)
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().Width(120).Text("EXENTO:");
                            r.AutoItem().Width(100).AlignRight().Text($"${datos.MontoExento:N0}");
                        });
                    }

                    if (datos.IVA > 0)
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().Width(120).Text("IVA 19%:");
                            r.AutoItem().Width(100).AlignRight().Text($"${datos.IVA:N0}");
                        });
                    }

                    c.Item().Row(r =>
                    {
                        r.AutoItem().Width(120).Text("TOTAL:").Bold();
                        r.AutoItem().Width(100).AlignRight().Text($"${datos.MontoTotal:N0}").Bold().FontSize(12);
                    });
                });
            });
        }

        private void CrearPie(IContainer container, DatosDTE datos, byte[] imagenPDF417)
        {
            container.Column(col =>
            {
                // Timbre PDF417
                if (imagenPDF417.Length > 0)
                {
                    col.Item().PaddingVertical(10).AlignCenter().Column(c =>
                    {
                        c.Item().Text("TIMBRE ELECTRÓNICO").FontSize(8).Bold();
                        c.Item().Image(imagenPDF417).FitWidth();
                    });
                }

                // Leyenda CEDIBLE (solo para facturas)
                if (datos.TipoDTE == 33)
                {
                    col.Item().PaddingTop(5).AlignCenter()
                        .Border(1).BorderColor(Colors.Black)
                        .Padding(5).Text("CEDIBLE").FontSize(10).Bold();
                }

                // Información adicional
                col.Item().PaddingTop(5).AlignCenter().Text("Documento generado electrónicamente según normativa SII").FontSize(7);
            });
        }

        // Clase auxiliar para datos del DTE
        private class DatosDTE
        {
            public int TipoDTE { get; set; }
            public int Folio { get; set; }
            public string FechaEmision { get; set; } = "";
            public string RutEmisor { get; set; } = "";
            public string RazonSocialEmisor { get; set; } = "";
            public string GiroEmisor { get; set; } = "";
            public string DireccionEmisor { get; set; } = "";
            public string ComunaEmisor { get; set; } = "";
            public string RutReceptor { get; set; } = "";
            public string RazonSocialReceptor { get; set; } = "";
            public string DireccionReceptor { get; set; } = "";
            public string ComunaReceptor { get; set; } = "";
            public int MontoNeto { get; set; }
            public int MontoExento { get; set; }
            public int IVA { get; set; }
            public int MontoTotal { get; set; }
            public List<DetalleItem> Detalles { get; set; } = new();
        }

        private class DetalleItem
        {
            public string Nombre { get; set; } = "";
            public decimal Cantidad { get; set; }
            public int Precio { get; set; }
            public int Total { get; set; }
        }
    }
}
