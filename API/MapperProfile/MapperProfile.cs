using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Entities.Models;
using Core.Entities.InputModels;

namespace api.CodigoCodeTest.MapperProfile
{
    public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<Evoucher, EVoucherModel>();
            //CreateMap<Product, ProductSelectViewModel>();
        }
    }
}
