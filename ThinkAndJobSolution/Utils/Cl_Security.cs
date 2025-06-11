using System.Security.Claims;

namespace ThinkAndJobSolution.Utils
{
    public class Cl_Security
    {
        public Cl_Security() { }
        public static string getSecurityInformation(ClaimsPrincipal User, string type)
        {
            string result = "";

            switch (type)
            {
                case "token":
                    result = User.Claims.FirstOrDefault(c => c.Type == "token")?.Value;
                    break;
                case "candidateId":
                    result = User.Claims.FirstOrDefault(c => c.Type == "candidateId")?.Value;
                    break;
                case "securityToken":
                    result = User.Claims.FirstOrDefault(c => c.Type == "securityToken")?.Value;
                    break;
                default:
                    break;
            }


            return result;
        }
    }
}
