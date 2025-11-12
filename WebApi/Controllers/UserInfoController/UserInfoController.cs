using ApplicationCore.DTO;
using ApplicationCore.Entities;
using ApplicationCore.Interfaces;
using ApplicationCore.Specifications;
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

        [Authorize]
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetUser(int id, CancellationToken token = default)
        {
            var result = await _service.GetOne(id, token);
            return Ok(_mapper.Map<UserInfoDTO>(result));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetUsers(CancellationToken token = default)
        {
            // Convert Request.Query to Dictionary for legacy specification filters
            var filterParams = Request.Query.ToDictionary(k => k.Key, k => k.Value.ToString());

            var userSpec = CreateFilter(filterParams, out int pageNumber, out int pageSize);
            var sortDescriptors = ParseSortDescriptors(filterParams);
            var filterDescriptors = ParseFilterDescriptors(); // No params - will use Request.Query directly

            var pagination = await _service.GetCount(userSpec, pageNumber, pageSize, filterDescriptors, token);
            var result = await _service.GetList(userSpec, pageNumber, pageSize, sortDescriptors, filterDescriptors, token);

            var response = new
            {
                pageInfo = pagination,
                users = _mapper.Map<List<UserInfoDTO>>(result)
            };

            return Ok(response);
        }

        private UserInfoSpecification CreateFilter(Dictionary<string, string> filterParams, out int pageNumber, out int pageSize)
        {
            InitFilter(filterParams, out pageNumber, out pageSize);

            var filter = new UserInfoSpecification();

            if(filterParams.ContainsKey("userName"))
            {
                filter.UserName = filterParams["userName"];
            }
            if(filterParams.ContainsKey("userNameContains"))
            {
                filter.UserNameContains = filterParams["userNameContains"];
            }

            return filter;
        }
        
    }
}
