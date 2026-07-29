using ApplicationCore.DTO;
using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.AuthController
{
    public class AuthController(IAuthService service, IMapper mapper) : ControllerBase<IAuthService>(service, mapper)
    {
        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] UserInfoDTO userParam, CancellationToken token = default)
        {
            var user = _mapper.Map<UserInfo>(userParam);
            var result = await _service.Login(user.UserName, user.Password, token);
            return Ok(result);
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] UserInfoDTO userParam, CancellationToken token = default)
        {
            var user = _mapper.Map<UserInfo>(userParam);
            var result = await _service.Register(user, token);
            return Ok(_mapper.Map<UserInfoDTO>(result));
        }
    }
}
