using ApplicationCore.Entities;
using ApplicationCore.Specifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationCore.Interfaces
{
    public partial interface IUserInfoService : IServiceBase<UserInfo>
    {
        Task<UserInfo> Register(UserInfo userInfo, CancellationToken token = default);
        Task<string> Login(string username, string password, CancellationToken token = default);
        Task<UserInfo> ResetPassword(UserInfo userInfo, CancellationToken token = default);
    }
}
