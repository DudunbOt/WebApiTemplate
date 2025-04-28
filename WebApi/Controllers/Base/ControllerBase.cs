using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{ 
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    public class ControllerBase<TService>(TService service, IMapper mapper) : ControllerBase where TService : class
    {
        protected readonly TService _service = service;
        protected readonly IMapper _mapper = mapper;

        protected void InitFilter(Dictionary<string, string> filterParams, out int pageNumber, out int pageSize)
        {
            pageNumber = 1;
            pageSize = 10;

            if (filterParams.ContainsKey("pageNumber"))
            {
                pageNumber = int.Parse(filterParams["pageNumber"]);
            }

            if (filterParams.ContainsKey("pageSize"))
            {
                pageSize = int.Parse(filterParams["pageSize"]);
            }
        }
    }
}
