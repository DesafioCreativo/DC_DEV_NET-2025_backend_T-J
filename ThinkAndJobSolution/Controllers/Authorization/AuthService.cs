using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThinkAndJobSolution.AccesoDato;
using ThinkAndJobSolution.Security;

namespace ThinkAndJobSolution.Controllers.Authorization
{
    public class AuthService : IAuthService
    {
        private DataContext _context;
        private IJwtUtils _jwtUtils;
        private readonly AppSettings _appSettings;
        private readonly IDataAccess _dataAccess;
        private readonly ICl_Encryption _cl_Encryption;

        public AuthService(
            DataContext context,
            IJwtUtils jwtUtils,
            IOptions<AppSettings> appSettings, IDataAccess dataAccess, ICl_Encryption cl_Encryption)
        {
            _context = context;
            _jwtUtils = jwtUtils;
            _appSettings = appSettings.Value;
            _dataAccess = dataAccess;
            _cl_Encryption = cl_Encryption;
        }

        public AuthenticateResponse Authenticate(AuthenticateRequest model, string ipAddress)
        {
            LoginData loginData = AccessController.getLoginData(model.username, model.password);

            if (loginData.error.ToString() == "False") {
                var jwtToken = _jwtUtils.GenerateJwtToken(loginData);
                var refreshToken = _jwtUtils.GenerateRefreshToken(ipAddress);

                return new AuthenticateResponse(loginData, jwtToken, refreshToken.Token);
            }
            else
            {
                return new AuthenticateResponse(loginData, null, null);
            }

            
        }

        public AuthenticateResponse RefreshToken(string token, string ipAddress)
        {
            var user = getUserByRefreshToken(token);
            var refreshToken = user.RefreshTokens.Single(x => x.Token == token);

            if (refreshToken.IsRevoked)
            {
                revokeDescendantRefreshTokens(refreshToken, user, ipAddress, $"Attempted reuse of revoked ancestor token: {token}");
                _context.Update(user);
                _context.SaveChanges();
            }

            if (!refreshToken.IsActive)
                throw new AppException("Invalid token");

            var newRefreshToken = rotateRefreshToken(refreshToken, ipAddress);
            user.RefreshTokens.Add(newRefreshToken);

            removeOldRefreshTokens(user);

            _context.Update(user);
            _context.SaveChanges();

            var jwtToken = _jwtUtils.GenerateJwtToken(user);

            return new AuthenticateResponse(user, jwtToken, newRefreshToken.Token);
        }

        public void RevokeToken(string token, string ipAddress)
        {
            var user = getUserByRefreshToken(token);
            var refreshToken = user.RefreshTokens.Single(x => x.Token == token);

            if (!refreshToken.IsActive)
                throw new AppException("Invalid token");

            revokeRefreshToken(refreshToken, ipAddress, "Revoked without replacement");
            _context.Update(user);
            _context.SaveChanges();
        }

        //public IEnumerable<LoginData> GetAll()
        //{
        //    return _context.Users;
        //}

        //public LoginData GetById(int id)
        //{
        //    var user = new LoginData();
        //    List<UserCustom> listuser = JsonConvert.DeserializeObject<List<UserCustom>>
        //        (_dataAccess.QueryReturnJSON(string.Format(@"exec sb_sp_segman_cn_usuario {0},{1}", 0, id)));

        //    if (listuser.Count == 0)
        //        throw new KeyNotFoundException("User not found");
        //    else
        //    {
        //        user.id = listuser[0].CODIGO;
        //        user.name = listuser[0].IDENTI;
        //    }
        //    return user;
        //}
        private LoginData getUserByRefreshToken(string token)
        {
            var user = _context.Users.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token));

            if (user == null)
                throw new AppException("Invalid token");

            return user;
        }
        private RefreshToken rotateRefreshToken(RefreshToken refreshToken, string ipAddress)
        {
            var newRefreshToken = _jwtUtils.GenerateRefreshToken(ipAddress);
            revokeRefreshToken(refreshToken, ipAddress, "Replaced by new token", newRefreshToken.Token);
            return newRefreshToken;
        }
        private void removeOldRefreshTokens(LoginData user)
        {
            // remove old inactive refresh tokens from user based on TTL in app settings
            user.RefreshTokens.RemoveAll(x =>
                !x.IsActive &&
                x.Created.AddDays(_appSettings.RefreshTokenTTL) <= DateTime.UtcNow);
        }
        private void revokeDescendantRefreshTokens(RefreshToken refreshToken, LoginData user, string ipAddress, string reason)
        {
            // recursively traverse the refresh token chain and ensure all descendants are revoked
            if (!string.IsNullOrEmpty(refreshToken.ReplacedByToken))
            {
                var childToken = user.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken.ReplacedByToken);
                if (childToken.IsActive)
                    revokeRefreshToken(childToken, ipAddress, reason);
                else
                    revokeDescendantRefreshTokens(childToken, user, ipAddress, reason);
            }
        }
        private void revokeRefreshToken(RefreshToken token, string ipAddress, string reason = null, string replacedByToken = null)
        {
            token.Revoked = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.ReasonRevoked = reason;
            token.ReplacedByToken = replacedByToken;
        }



    }
}
