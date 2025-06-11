namespace ThinkAndJobSolution.Controllers._Helper.OfficeTools
{
    public class ImageInsert : Insert
    {
        public byte[] value { get; set; }

        public ImageInsert(string id, byte[] value)
        {
            base.id = id;
            this.value = value;
        }
        public ImageInsert(string id, string value)
        {
            base.id = id;
            if (value.Contains("base64,"))
                value = value.Split("base64,")[1];
            this.value = Convert.FromBase64String(value);
        }

        public override OfficeTools.ExchangeInsert GetExchangeInsert(MultipartFormDataContent content)
        {
            string key = "img_" + HelperMethods.RandomString(10);

            content.Add(new StreamContent(new MemoryStream(this.value)), key, key);
            return new OfficeTools.ExchangeInsert()
            {
                id = base.id,
                value = key,
                type = "img"
            };
        }
    }
}
