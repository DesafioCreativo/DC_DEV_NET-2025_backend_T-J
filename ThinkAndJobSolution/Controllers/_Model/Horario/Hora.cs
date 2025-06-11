namespace ThinkAndJobSolution.Controllers._Model.Horario
{
    public class Hora
    {
        public int hora { get; set; }
        public int minuto { get; set; }

        public static DateTime operator +(DateTime date, Hora hora)
        {
            return date.Date.AddHours(hora.hora).AddMinutes(hora.minuto);
        }

        public static bool operator >(Hora a, Hora b)
        {
            return a.hora > b.hora || a.hora == b.hora && a.minuto > b.minuto;
        }

        public static bool operator <(Hora a, Hora b)
        {
            return b.hora > a.hora || b.hora == a.hora && b.minuto > a.minuto;
        }

        public static bool operator ==(Hora a, Hora b)
        {
            if (a is null || b is null)
                return ReferenceEquals(a, b);

            return a.hora == b.hora && a.minuto == b.minuto;
        }

        public static bool operator !=(Hora a, Hora b)
        {
            if (a is null || b is null)
                return !ReferenceEquals(a, b);

            return a.hora != b.hora || a.minuto != b.minuto;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            if (obj is Hora hora) return this == hora;
            return false;
        }

        public override int GetHashCode()
        {
            return hora * 60 + minuto;
        }

        public override string ToString()
        {
            string h = hora.ToString();
            string m = minuto.ToString();
            if (h.Length == 1) h = "0" + h;
            if (m.Length == 1) m = "0" + m;
            return $"{h}:{m}";
        }
    }
}
