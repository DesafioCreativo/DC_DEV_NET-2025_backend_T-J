namespace ThinkAndJobSolution.Controllers._Model
{
    public class Society
    {
        public int id { get; set; }
        public string name { get; set; }
        public string logo { get; set; }
        public int contracts { get; set; }
        public int maxContracts { get; set; }

        /**
         * Empty constructors
         **/
        public Society() { }

        /**
         * Full constructor
         **/
        public Society(int id, string name, string logo, int contracts, int maxContracts)
        {
            this.id = id;
            this.name = name;
            this.logo = logo;
            this.contracts = contracts;
            this.maxContracts = maxContracts;
        }
    }
}
