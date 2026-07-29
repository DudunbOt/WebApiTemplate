using ApplicationCore.Entities;
using ApplicationCore.Exceptions;
using ApplicationCore.Interfaces;
using ApplicationCore.Specifications;
using Infrastructure.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Infrastructure.Services.Crypto;

namespace Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly AppConfig _appConfig;
        private readonly AppDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly IUserInfoService _userInfoService;

        public AuthService(AppDbContext context, IOptions<JwtSettings> jwtSettings, IOptions<AppConfig> appConfig, ICurrentUser currentUser, IUserInfoService userInfoService)
        {
            _jwtSettings = jwtSettings.Value;
            _appConfig = appConfig.Value;
            _context = context;
            _currentUser = currentUser;
            _userInfoService = userInfoService;
        }

        public async Task<string> Login(string username, string password, CancellationToken token = default)
        {
            UserInfoSpecification specification = new UserInfoSpecification(username);

            UserInfo user = await _userInfoService.GetOne(specification, token);
            if (user == null)
                throw new NotFoundException("UserInfo", username);

            if (!VerifyHashedPassword(user.Password, password))
                throw new UnauthorizedException("Invalid username or password");

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
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpiryInMinutes), // Token expiration time
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<UserInfo> Register(UserInfo userInfo, CancellationToken token = default)
        {
            if (userInfo == null)
                throw new ArgumentNullException(nameof(userInfo));

            if (string.IsNullOrWhiteSpace(userInfo.UserName))
                throw new ValidationException("UserName", "Username is required");

            if (string.IsNullOrWhiteSpace(userInfo.Password))
                throw new ValidationException("Password", "Password is required");

            // Check if username already exists
            var existingUserSpec = new UserInfoSpecification(userInfo.UserName);
            var existingUser = await _userInfoService.GetOne(existingUserSpec, token);
            if (existingUser != null)
                throw new ConflictException($"Username '{userInfo.UserName}' is already taken");

            userInfo.Password = HashPassword(userInfo.Password);

            userInfo = await _userInfoService.Upsert(userInfo, userInfo.Id, token);

            return userInfo;
        }

        //TODO: Implement Reset Password
        public async Task<UserInfo> ResetPassword(UserInfo user, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }
    }
}
