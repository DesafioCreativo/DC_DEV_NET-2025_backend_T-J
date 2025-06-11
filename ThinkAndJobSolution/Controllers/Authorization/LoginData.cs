using System.Text.Json.Serialization;

namespace ThinkAndJobSolution.Controllers.Authorization
{
    public class LoginData
    {
        public bool found { get; set; }
        public object error { get; set; }
        public string token { get; set; }
        public string id { get; set; }
        public string username { get; set; }
        public string pwd { get; set; }
        public string docID { get; set; }
        public string docType { get; set; }
        public string name { get; set; }
        public string surname { get; set; }
        public string email { get; set; }
        public bool authorized { get; set; }
        public string securityToken { get; set; }
        public bool isExternal { get; set; }
        public bool hideSocieties { get; set; }
        public bool hasToShift { get; set; }
        public bool requiresKeepAlive { get; set; }
        public string photo { get; set; }
        public string type { get; set; }
        public string candidateId { get; set; }
        public string candidateDni { get; set; }
        public Object lastSignLink { get; set; }
        public DateTime lastAccess { get; set; }
        public string autoLoginToken { get; set; }
        public int? cstatus { get; set; }
        public int? periodoGracia { get; set; }

        [JsonIgnore]
        public List<RefreshToken> RefreshTokens { get; set; }
    }
}
