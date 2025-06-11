using static ThinkAndJobSolution.Controllers._Helper.OfficeTools.OfficeTools;

namespace ThinkAndJobSolution.Controllers._Helper.OfficeTools
{
    public abstract class Insert
    {
        public string id { get; set; }

        public abstract ExchangeInsert GetExchangeInsert(MultipartFormDataContent content);
    }
}
