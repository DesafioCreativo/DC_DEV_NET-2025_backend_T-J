namespace ThinkAndJobSolution.Controllers._Helper.OfficeTools
{
    public class ListInsert : Insert
    {
        private List<string> value { get; set; }

        public ListInsert(string id, List<string> value)
        {
            base.id = id;
            this.value = value;
        }

        public override OfficeTools.ExchangeInsert GetExchangeInsert(MultipartFormDataContent content)
        {
            return new OfficeTools.ExchangeInsert()
            {
                id = base.id,
                value = this.value,
                type = "list"
            };
        }
    }
}
