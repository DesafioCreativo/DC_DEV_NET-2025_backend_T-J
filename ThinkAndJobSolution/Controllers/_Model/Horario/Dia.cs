namespace ThinkAndJobSolution.Controllers._Model.Horario
{
    public class Dia
    {
        public Turno manana { get; set; }
        public Turno tarde { get; set; }
        public Turno noche { get; set; }

        public bool baja { get; set; }
        public bool vacaciones { get; set; }

        public override string ToString()
        {
            string mananaString = manana == null ? "" : $"Mañana {manana} ";
            string tardeString = tarde == null ? "" : $"Tarde {tarde} ";
            string nocheString = noche == null ? "" : $"Noche {noche} ";
            return $"{mananaString}{tardeString}{nocheString}".Trim();
        }
    }
}
