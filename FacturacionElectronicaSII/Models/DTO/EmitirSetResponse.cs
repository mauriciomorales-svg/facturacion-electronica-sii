namespace FacturacionElectronicaSII.Models.DTO
{
    public class EmitirSetResponse
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = "";
        public string? TrackID { get; set; }
        public int CantidadDTEs { get; set; }
        public List<string> Folios { get; set; } = new();
        public List<string> Errores { get; set; } = new();
    }
}
