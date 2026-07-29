using ApplicationCore.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApplicationCore.Interfaces
{
    public interface IAuthService
    {
        Task<UserInfo> Register(UserInfo userInfo, CancellationToken token = default);
        Task<string> Login(string username, string password, CancellationToken token = default);
        Task<UserInfo> ResetPassword(UserInfo userInfo, CancellationToken token = default);
    }
}
