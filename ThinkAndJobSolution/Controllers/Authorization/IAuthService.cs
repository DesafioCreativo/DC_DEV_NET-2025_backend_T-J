namespace ThinkAndJobSolution.Controllers.Authorization
{
    public interface IAuthService
    {
        AuthenticateResponse Authenticate(AuthenticateRequest model, string ipAddress);
        AuthenticateResponse RefreshToken(string token, string ipAddress);
        void RevokeToken(string token, string ipAddress);
        //IEnumerable<User> GetAll();
        //LoginData getbyid(int id);
    }
}
