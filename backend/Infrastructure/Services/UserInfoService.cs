using ApplicationCore.Entities;
using ApplicationCore.Exceptions;
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

        public UserInfoService(AppDbContext context, IDistributedCache cache, IOptions<AppConfig> appConfig, ICurrentUser currentUser) : base(context, cache, appConfig, currentUser)
        {
            
        }

    }
}
