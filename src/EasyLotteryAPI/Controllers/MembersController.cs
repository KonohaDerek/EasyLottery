using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EasyLotteryAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyLotteryAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembersController : ControllerBase
    {
        

        public async Task<List<Google.Apis.YouTube.v3.Data.Member>> GetAsync()
        {
            return await DemoService.GetChannelMembers();
        }
    }
}