using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Image = SixLabors.ImageSharp.Image;

namespace ThinkAndJobSolution.Controllers._Helper.OfficeTools
{
    public class OfficeTools
    {
        public struct ExchangeInsert
        {
            public string id { get; set; }
            public object value { get; set; }
            public string type { get; set; }
        }

        public static async Task<byte[]> applyTemplateProcessor(byte[] template, List<Insert> inserts, bool convertToPdf)
        {
            //Obtener el endpoint y la clave de api
            if (!tryGetEndpointAndApikey(out string endpoint, out string apikey))
                throw new Exception("Could not restore endpoint or api key");

            using (MemoryStream ms = new(template))
            {
                using (HttpClient client = new())
                {
                    using (MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                    {
                        List<ExchangeInsert> exchangeInserts = new();
                        foreach (Insert insert in inserts)
                            exchangeInserts.Add(insert.GetExchangeInsert(content));

                        content.Add(new StreamContent(ms), "template", "template.docx");
                        content.Add(new StringContent(JsonSerializer.Serialize(exchangeInserts)), "inserts");
                        content.Add(new StringContent(convertToPdf ? "pdf" : "word"), "target");

                        using (HttpResponseMessage message = await client.PostAsync(endpoint + "/ProcessTemplate?apikey=" + apikey, content))
                        {
                            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                                throw new Exception("Status code not OK: " + message.StatusCode);
                            byte[] result = await message.Content.ReadAsByteArrayAsync();
                            if (result == null || result.Length == 0) throw new Exception("Could not convert");
                            return result;
                        }
                    }
                }
            }
        }

        public static async Task<string> applyTemplateProcessor(string template, List<Insert> inserts, bool convertToPdf)
        {
            if (template.Contains("base64,"))
                template = template.Split("base64,")[1];

            byte[] result = await applyTemplateProcessor(Convert.FromBase64String(template), inserts, convertToPdf);

            return (convertToPdf ? "data:application/pdf;base64," : "data:application/vnd.openxmlformats-officedocument.wordprocessingml.document;base64,") + Convert.ToBase64String(result);
        }

        public static async Task<List<string>> extractTemplateKeys(byte[] template)
        {
            //Obtener el endpoint y la clave de api
            if (!tryGetEndpointAndApikey(out string endpoint, out string apikey))
                throw new Exception("Could not restore endpoint or api key");

            using (MemoryStream ms = new(template))
            {
                using (HttpClient client = new())
                {
                    using (MultipartFormDataContent content = new("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                    {

                        content.Add(new StreamContent(ms), "template", "template.docx");

                        using (HttpResponseMessage message = await client.PostAsync(endpoint + "/ExtractKeys?apikey=" + apikey, content))
                        {
                            if (message.StatusCode != System.Net.HttpStatusCode.OK)
                                throw new Exception("Status code not OK: " + message.StatusCode);
                            string result = await message.Content.ReadAsStringAsync();
                            if (result == null || result.Length == 0) throw new Exception("Could not convert");
                            return HelperMethods.GetJsonStringList(JsonDocument.Parse(result).RootElement);
                        }
                    }
                }
            }
        }

        public static async Task<List<string>> extractTemplateKeys(string template)
        {
            if (template.Contains("base64,"))
                template = template.Split("base64,")[1];

            return await extractTemplateKeys(Convert.FromBase64String(template));
        }


        public static async Task prepareCombinedImages(byte[] template, List<Insert> inserts)
        {
            //Buscar las calves de imagenes combinadas
            List<string> keys = await extractTemplateKeys(template);
            keys = keys.FindAll(x => x.Contains(";"));

            foreach (string key in keys)
            {
                Dictionary<string, float> transparencies = new(); //Lista de transparencias a aplicar

                //Extraer los componentes
                string cleanKey = key;
                if (cleanKey.Contains(":"))
                    cleanKey = cleanKey.Split(":")[0];
                string[] components = cleanKey.Split(";");

                //Buscar el inserto de cada componente y determinar el componente principal
                List<Insert> insertComponents = new();
                Insert mainInsertComponent = null;
                foreach (string component in components)
                {
                    bool willBeMain = false;
                    string componenetKey = component.Trim();
                    if (componenetKey.StartsWith("*"))
                    {
                        componenetKey = componenetKey.Substring(1);
                        willBeMain = true;
                    }
                    if (componenetKey.Contains("."))
                    {
                        string[] parts = componenetKey.Split(".");
                        componenetKey = parts[0];
                        try
                        {
                            transparencies[componenetKey] = (float)(int.Parse(parts[1]) / 100.0);
                        }
                        catch (Exception) { }
                    }
                    Insert insert = inserts.FirstOrDefault(x => x.id == componenetKey);
                    if (insert != null)
                    {
                        insertComponents.Add(insert);
                        if (willBeMain)
                            mainInsertComponent = insert;
                    }
                }
                if (mainInsertComponent == null)
                    mainInsertComponent = insertComponents[0];

                //El componente principal tiene que ser una imagen
                if (!(mainInsertComponent is ImageInsert)) continue;
                byte[] mainImage = (mainInsertComponent as ImageInsert).value;

                //Unir las imagenes
                using (var mainMS = new MemoryStream(mainImage, 0, mainImage.Length))
                {
                    //Obtener el tamaño
                    int baseWidth, baseHeight;
                    using (Image image = Image.Load(mainMS))
                    {
                        baseWidth = image.Width;
                        baseHeight = image.Height;
                    }
                    float baseRatio = baseWidth / (float)baseHeight;

                    //Crear la imagen resultado
                    using (Image<Rgba32> image = new Image<Rgba32>(baseWidth, baseHeight))
                    {
                        using (Image rImgage = image.Clone(ctx =>
                        {
                            ctx.BackgroundColor(Color.Transparent);
                            foreach (Insert insert in insertComponents)
                            {
                                //Intentar procesar como una imagen
                                if (insert is ImageInsert)
                                {
                                    byte[] imageData = (insert as ImageInsert).value;
                                    using (var ms = new MemoryStream(imageData, 0, imageData.Length))
                                    {
                                        using (Image pImage = Image.Load(ms))
                                        {
                                            //Calcular dónde dibujar la imagen
                                            float scale;
                                            int x, y;
                                            if (pImage.Width / (float)pImage.Height > baseRatio)
                                            {
                                                scale = baseWidth / (float)pImage.Width;
                                                x = 0;
                                                y = (int)((baseHeight - pImage.Height * scale) / 2);
                                            }
                                            else
                                            {
                                                scale = baseHeight / (float)pImage.Height;
                                                y = 0;
                                                x = (int)((baseWidth - pImage.Width * scale) / 2);
                                            }
                                            pImage.Mutate(i => i.Resize((int)(pImage.Width * scale), (int)(pImage.Height * scale)));
                                            ctx.DrawImage(pImage, new Point(x, y), transparencies.ContainsKey(insert.id) ? transparencies[insert.id] : 1);
                                        }
                                    }
                                }
                            }
                        }))
                        {
                            //Guardar la imagen en un inserto con el nombre real
                            using (MemoryStream ms = new())
                            {
                                rImgage.SaveAsPng(ms);
                                inserts.Add(new ImageInsert(cleanKey, ms.ToArray()));
                            }
                        }
                    }
                }
            }
        }
        public static async Task prepareCombinedImages(string template, List<Insert> inserts)
        {
            if (template.Contains("base64,"))
                template = template.Split("base64,")[1];

            await prepareCombinedImages(Convert.FromBase64String(template), inserts);
        }

        private static bool tryGetEndpointAndApikey(out string endpoint, out string apikey)
        {
            endpoint = null;
            apikey = null;
            using (SqlConnection conn = new SqlConnection(HelperMethods.CONNECTION_STRING))
            {
                conn.Open();

                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT [value] FROM sys_config WHERE [key] = 'woffice-endpoint'";
                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            endpoint = reader.GetString(reader.GetOrdinal("value"));
                }
                using (SqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = "SELECT [value] FROM sys_config WHERE [key] = 'woffice-apikey'";
                    using (SqlDataReader reader = command.ExecuteReader())
                        if (reader.Read())
                            apikey = reader.GetString(reader.GetOrdinal("value"));
                }
            }
            return endpoint != null && apikey != null;
        }


    }
}
