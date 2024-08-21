using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using ApplicationCore.Specifications;
using AutoMapper;
using Infrastructures.Configurations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructures.Services
{
    public class UserInfoService : ServiceBase<UserInfo>, IUserInfoService
    {

        private readonly JwtSettings _jwtSettings;

        public UserInfoService(AppDbContext context, IDistributedCache cache, IOptions<JwtSettings> jwtSettings) : base(context, cache)
        {
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<string> Login(string username, string password, CancellationToken token = default)
        {
            UserInfoSpecification specification = new UserInfoSpecification()
            {
                UserName = username,
                Password = HashPassword(password)
            };

            UserInfo user = await this.GetOne(specification, token);

            if(user == null)
            {
                throw new Exception("Username or Password Incorrect");
            }

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

        public async Task<UserInfo> ResetPassword(UserInfo user, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        private static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
