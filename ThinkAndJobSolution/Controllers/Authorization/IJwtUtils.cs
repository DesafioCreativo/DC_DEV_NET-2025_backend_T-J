namespace ThinkAndJobSolution.Controllers.Authorization
{
    public interface IJwtUtils
    {
        public string GenerateJwtToken(LoginData user);
        public int? ValidateJwtToken(string token);
        public RefreshToken GenerateRefreshToken(string ipAddress);
    }
}
