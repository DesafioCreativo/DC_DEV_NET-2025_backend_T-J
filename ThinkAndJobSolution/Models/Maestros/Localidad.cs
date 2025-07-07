namespace ThinkAndJobSolution.Controllers._Model
{
    public class Localidad
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public int provinciaRef { get; set; }
        public int integration_id { get; set; }
        public int api_id { get; set; }
        public string code { get; set; }
        public string timezone { get; set; }
        public string name_dt { get; set; }
        public int parent { get; set; }
        public int status { get; set; }
    }
}
