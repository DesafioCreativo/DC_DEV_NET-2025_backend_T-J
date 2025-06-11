namespace ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime GetMonday(this DateTime date)
        {
            DateTime monday = date.Date;
            while (monday.DayOfWeek != DayOfWeek.Monday)
                monday = monday.AddDays(-1);
            return monday;
        }
        public static DateTime GetSunday(this DateTime date)
        {
            DateTime sunday = date.Date;
            while (sunday.DayOfWeek != DayOfWeek.Sunday)
                sunday = sunday.AddDays(1);
            return sunday;
        }
    }
}
