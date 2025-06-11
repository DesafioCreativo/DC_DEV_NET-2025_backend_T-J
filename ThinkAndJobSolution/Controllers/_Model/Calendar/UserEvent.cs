using Microsoft.Data.SqlClient;

namespace ThinkAndJobSolution.Controllers._Model.Calendar
{
    public class UserEvent : CalendarEvent
    {
        public override string type { get; } = "user";
        public override bool important => category == "important";

        public static List<UserEvent> fromReader(SqlDataReader reader)
        {
            List<UserEvent> events = new();

            while (reader.Read())
            {
                UserEvent uevent = new UserEvent()
                {
                    id = reader.GetString(reader.GetOrdinal("id")),
                    title = reader.GetString(reader.GetOrdinal("title")),
                    description = reader.GetString(reader.GetOrdinal("description")),
                    category = reader.GetString(reader.GetOrdinal("category")),
                    date = reader.GetDateTime(reader.GetOrdinal("day")),
                    isGroup = reader.GetInt32(reader.GetOrdinal("grupal")) == 1,
                    createdBy = reader.GetString(reader.GetOrdinal("createdBy"))
                };
                if (!reader.IsDBNull(reader.GetOrdinal("timeStart")))
                    uevent.start = uevent.date.Add(reader.GetTimeSpan(reader.GetOrdinal("timeStart")));
                if (!reader.IsDBNull(reader.GetOrdinal("timeEnd")))
                    uevent.end = uevent.date.Add(reader.GetTimeSpan(reader.GetOrdinal("timeEnd")));
                events.Add(uevent);
            }

            return events;
        }
    }
}
