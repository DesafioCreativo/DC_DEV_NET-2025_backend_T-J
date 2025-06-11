using System.Text.Json.Serialization;

namespace ThinkAndJobSolution.Controllers.Authorization
{
    public class AuthenticateResponse
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
        public int? cstatus { get; set; }
        public int? periodoGracia { get; set; }
        public string JwtToken { get; set; }

        [JsonIgnore] // refresh token is returned in http only cookie
        public string RefreshToken { get; set; }
        

        public AuthenticateResponse(LoginData user, string jwtToken, string refreshToken)
        {
            found = user.found;
            error = user.error;
            token = user.token;
            id = user.id;
            username = user.username;
            pwd = user.pwd;
            docID = user.docID;
            docType = user.docType;
            name = user.name;
            surname = user.surname;
            email = user.email;
            authorized = user.authorized;
            securityToken = user.securityToken;
            isExternal = user.isExternal;
            hideSocieties = user.hideSocieties;
            hasToShift = user.hasToShift;
            requiresKeepAlive = user.requiresKeepAlive;
            photo = user.photo;
            type = user.type;
            candidateId = user.candidateId;
            candidateDni = user.candidateDni;
            lastSignLink = user.lastSignLink;
            lastAccess = user.lastAccess;           
            cstatus = user.cstatus;
            periodoGracia = user.periodoGracia;
            JwtToken = jwtToken;
            RefreshToken = refreshToken;
        }
    }
}
