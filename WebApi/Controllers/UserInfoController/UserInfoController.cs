using ApplicationCore.DTO;
using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers.UserInfoController
{
    public class UserInfoController(IUserInfoService userInfoService, IMapper mapper) : ControllerBase<IUserInfoService>(userInfoService, mapper)
    {

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] UserInfoDTO userParam, CancellationToken token = default)
        {
            try
            {
                var user = _mapper.Map<UserInfo>(userParam);
                var result = await _service.Login(user.UserName, user.Password, token);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] UserInfoDTO userParam, CancellationToken token = default)
        {
            try
            {
                var user = _mapper.Map<UserInfo>(userParam);
                var result = await _service.Register(user, token);
                return Ok(_mapper.Map<UserInfoDTO>(result));
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetUser(int id, CancellationToken token = default)
        {
            try
            {
                var result = await _service.GetOne(id, token);
                return Ok(_mapper.Map<UserInfoDTO>(result));
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
    }
}
