using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FacturacionElectronicaSII.Models.DTO
{
    /// <summary>
    /// Request SIMPLE para emitir una factura - solo 4 campos obligatorios
    /// </summary>
    public class FacturaSimpleRequest
    {
        /// <summary>
        /// RUT del cliente (ej: 66666666-6)
        /// </summary>
        [Required]
        [DefaultValue("66666666-6")]
        public string RutCliente { get; set; } = null!;

        /// <summary>
        /// Nombre o razón social del cliente
        /// </summary>
        [Required]
        [DefaultValue("CLIENTE PRUEBA")]
        public string NombreCliente { get; set; } = null!;

        /// <summary>
        /// Descripción del producto o servicio
        /// </summary>
        [Required]
        [DefaultValue("SERVICIO DE PRUEBA")]
        public string Descripcion { get; set; } = null!;

        /// <summary>
        /// Monto neto (sin IVA)
        /// </summary>
        [Required]
        [DefaultValue(100000)]
        public decimal MontoNeto { get; set; }

        // Campos opcionales con valores por defecto
        public string? Giro { get; set; }
        public string? Direccion { get; set; }
        public string? Comuna { get; set; }
        public string? Ciudad { get; set; }
    }

    /// <summary>
    /// Request para emitir un DTE
    /// </summary>
    public class EmitirDTERequest
    {
        /// <summary>
        /// Tipo de documento: 33=Factura Electrónica, 34=Factura Exenta, 39=Boleta, 61=Nota de Crédito, 56=Nota de Débito
        /// </summary>
        [Required]
        [DefaultValue(33)]
        public int TipoDTE { get; set; }

        /// <summary>
        /// Datos del receptor del documento
        /// </summary>
        [Required]
        public ReceptorDTO Receptor { get; set; } = null!;

        /// <summary>
        /// Lista de items/productos del documento
        /// </summary>
        [Required]
        public List<DetalleDTO> Detalles { get; set; } = new();

        /// <summary>
        /// Forma de pago: 1=Contado, 2=Crédito, 3=Sin costo (opcional)
        /// </summary>
        [DefaultValue(1)]
        public int? FormaPago { get; set; }

        /// <summary>
        /// Referencias a otros documentos (requerido para NC/ND)
        /// </summary>
        public List<ReferenciaDTO>? Referencias { get; set; }

        /// <summary>
        /// Descuento global en porcentaje sobre items afectos (0-100)
        /// </summary>
        public decimal? DescuentoGlobalPorcentaje { get; set; }
    }

    /// <summary>
    /// Datos del receptor del documento
    /// </summary>
    public class ReceptorDTO
    {
        /// <summary>
        /// RUT del receptor con dígito verificador (ej: 66666666-6)
        /// </summary>
        [Required]
        [DefaultValue("66666666-6")]
        public string RUT { get; set; } = null!;

        /// <summary>
        /// Razón social o nombre del receptor
        /// </summary>
        [Required]
        [DefaultValue("EMPRESA RECEPTOR PRUEBA")]
        public string RazonSocial { get; set; } = null!;

        /// <summary>
        /// Giro o actividad económica del receptor
        /// </summary>
        [DefaultValue("COMERCIO AL POR MENOR")]
        public string? Giro { get; set; }

        /// <summary>
        /// Dirección del receptor
        /// </summary>
        [Required]
        [DefaultValue("CALLE EJEMPLO 123")]
        public string Direccion { get; set; } = null!;

        /// <summary>
        /// Comuna del receptor
        /// </summary>
        [Required]
        [DefaultValue("SANTIAGO")]
        public string Comuna { get; set; } = null!;

        /// <summary>
        /// Ciudad del receptor
        /// </summary>
        [Required]
        [DefaultValue("SANTIAGO")]
        public string Ciudad { get; set; } = null!;
    }

    /// <summary>
    /// Detalle de un item/producto del documento
    /// </summary>
    public class DetalleDTO
    {
        /// <summary>
        /// Código interno del producto (opcional)
        /// </summary>
        [DefaultValue("PROD001")]
        public string? Codigo { get; set; }

        /// <summary>
        /// Nombre o descripción del producto/servicio
        /// </summary>
        [Required]
        [DefaultValue("SERVICIO DE DESARROLLO DE SOFTWARE")]
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Cantidad de unidades
        /// </summary>
        [Required]
        [DefaultValue(1)]
        public decimal Cantidad { get; set; }

        /// <summary>
        /// Precio unitario NETO (sin IVA)
        /// </summary>
        [Required]
        [DefaultValue(100000)]
        public decimal PrecioUnitario { get; set; }

        /// <summary>
        /// Descuento en porcentaje para este item (0-100)
        /// </summary>
        public decimal? DescuentoPorcentaje { get; set; }

        /// <summary>
        /// true = item exento de IVA, false = afecto a IVA
        /// </summary>
        [DefaultValue(false)]
        public bool Exento { get; set; } = false;
    }

    /// <summary>
    /// Referencia a otro documento (para Notas de Crédito/Débito)
    /// </summary>
    public class ReferenciaDTO
    {
        /// <summary>
        /// Tipo del documento referenciado (33=Factura, 61=NC, etc.)
        /// </summary>
        [Required]
        [DefaultValue(33)]
        public int TipoDTE { get; set; }

        /// <summary>
        /// Folio del documento referenciado
        /// </summary>
        [Required]
        [DefaultValue(1)]
        public int Folio { get; set; }

        /// <summary>
        /// Fecha del documento referenciado
        /// </summary>
        [Required]
        public DateTime Fecha { get; set; }

        /// <summary>
        /// Código de referencia: 1=Anula documento, 2=Corrige texto, 3=Corrige monto
        /// </summary>
        [Required]
        [DefaultValue(1)]
        public int CodigoReferencia { get; set; }

        /// <summary>
        /// Razón de la referencia (ej: "ANULA FACTURA POR ERROR")
        /// </summary>
        [DefaultValue("ANULA DOCUMENTO DE REFERENCIA")]
        public string? Razon { get; set; }
    }
}
