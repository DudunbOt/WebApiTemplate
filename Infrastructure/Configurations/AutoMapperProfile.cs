using ApplicationCore.DTO;
using ApplicationCore.Entities;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Configurations
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            //Create Mapper for Object <-> DTO here
            CreateMap<UserInfo, UserInfoDTO>()
                .ForMember(dest => dest.Password, opt => opt.Ignore());
            CreateMap<UserInfoDTO, UserInfo>()
                .ForMember(dest => dest.Password, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Password)));
        }
    }
}
