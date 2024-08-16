using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers


    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    public class ControllerBase<TService>(TService service, IMapper mapper) : ControllerBase where TService : class
    {
        protected readonly TService _service = service;
        protected readonly IMapper _mapper = mapper;
    }
}
