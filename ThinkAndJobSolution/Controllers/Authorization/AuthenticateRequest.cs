using System.ComponentModel.DataAnnotations;
namespace ThinkAndJobSolution.Controllers.Authorization
{
    public class AuthenticateRequest
    {

        [Required]
        public string username { get; set; }

        [Required]
        public string password { get; set; }
    }
}
