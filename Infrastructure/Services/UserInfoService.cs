using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using ApplicationCore.Specifications;
using Infrastructure.Configurations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using static Infrastructure.Services.Crypto;
using System.Text;

namespace Infrastructure.Services
{
    public class UserInfoService : ServiceBase<UserInfo>, IUserInfoService
    {

        private readonly JwtSettings _jwtSettings;

        public UserInfoService(AppDbContext context, IDistributedCache cache, IOptions<JwtSettings> jwtSettings, IOptions<AppConfig> appConfig) : base(context, cache, appConfig)
        {
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<string> Login(string username, string password, CancellationToken token = default)
        {
            UserInfoSpecification specification = new UserInfoSpecification(username);

            UserInfo user = await this.GetOne(specification, token);
            if(user == null)
                throw new Exception($"Cannot Find User with username: {username}");

            if (!VerifyHashedPassword(user.Password, password))
                throw new Exception($"Password is incorrect");

            List<Claim> claims =
            [
                new(ClaimTypes.Name, user.UserName),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            ];

            return GenerateToken(claims);
        }

        private string GenerateToken(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience:_jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpiryInMinutes), // Token expiration time
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<UserInfo> Register(UserInfo userInfo, CancellationToken token = default)
        {
            if(userInfo == null)
                throw new ArgumentException("parameter userinfo can't be null");

            userInfo.Password = HashPassword(userInfo.Password);

            userInfo = await this.Upsert(userInfo, userInfo.Id, token);

            return userInfo;
        }

        //TODO: Implement Reset Password
        public async Task<UserInfo> ResetPassword(UserInfo user, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

    }
}
