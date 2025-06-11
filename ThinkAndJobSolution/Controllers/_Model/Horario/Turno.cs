namespace ThinkAndJobSolution.Controllers._Model.Horario
{
    public class Turno
    {
        public string responsable { get; set; }

        public Hora entrada { get; set; }
        public Hora salida { get; set; }

        public override string ToString()
        {
            return $"{entrada} - {salida}";
        }
    }
}
