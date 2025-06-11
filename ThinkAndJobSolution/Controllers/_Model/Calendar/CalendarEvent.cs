using ThinkAndJobSolution.Controllers.MainHome.RRHH;

namespace ThinkAndJobSolution.Controllers._Model.Calendar
{
    public abstract class CalendarEvent
    {
        public string id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public DateTime date { get; set; }
        public DateTime? start { get; set; }
        public DateTime? end { get; set; }
        public virtual string type { get; }
        public bool seen { get; set; }

        //Definido aqui porque si no no se serializa y configurarlo es un palo
        public GuardiasController.Guardia? guardia { get; set; }
        public string category { get; set; }
        public bool isGroup { get; set; }
        public string createdBy { get; set; }

        public virtual bool important { get; }
    }

    public static class JsonExtensions
    {
        public static IEnumerable<T> SortByDate<T>(this IEnumerable<T> list) where T : CalendarEvent
        {
            return list.OrderBy(e => e.date).ThenBy(e => e.start).ThenBy(e => e.end).ThenBy(e => e.title);
        }

    }
}
