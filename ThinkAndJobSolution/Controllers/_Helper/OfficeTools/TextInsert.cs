namespace ThinkAndJobSolution.Controllers._Helper.OfficeTools
{
    public class TextInsert : Insert
    {
        private string value { get; set; }

        public TextInsert(string id, string value)
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
                type = "text"
            };
        }
    }
}
