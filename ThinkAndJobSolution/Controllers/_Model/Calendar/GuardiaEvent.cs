using ThinkAndJobSolution.Controllers.MainHome.RRHH;
namespace ThinkAndJobSolution.Controllers._Model.Calendar
{
    public class GuardiaEvent : CalendarEvent
    {
        public override string type { get; } = "guardia";
        public override bool important { get; } = false;

        public static List<GuardiaEvent> fromGuardia(GuardiasController.Guardia guardia)
        {
            List<GuardiaEvent> events = new();

            DateTime cDay = guardia.firstDay;
            while (cDay <= guardia.lastDay)
            {
                events.Add(new GuardiaEvent()
                {
                    title = "Guardia",
                    description = null,
                    date = cDay.Date,
                    start = cDay.Date == guardia.startTime.Date ? guardia.startTime : null,
                    end = cDay.Date == guardia.endTime.Date ? guardia.endTime : null,
                    guardia = guardia
                });
                cDay = cDay.AddDays(1);
            }

            return events;
        }
    }
}
