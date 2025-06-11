namespace ThinkAndJobSolution.Controllers._Model.Horario
{
    using Horario = List<Dia>;

    public class Semana
    {
        public Dictionary<string, Horario> candidatos { get; set; }
        public List<Grupo> grupos { get; set; }

        public class Grupo
        {
            public string nombre { get; set; }
            public List<string> miembros { get; set; }
            public Horario horario { get; set; }
        }
    }
}
