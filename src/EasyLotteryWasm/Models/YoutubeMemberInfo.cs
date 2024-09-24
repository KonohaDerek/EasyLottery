using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MiniExcelLibs.Attributes;

namespace EasyLotteryWasm.Models
{
    public class YoutubeMemberInfo
    {
        [ExcelColumnName("會員")]
        public string Name { get; init; }

         [ExcelColumnName("連結到個人資料")]
        public string Link { get; init; }

         [ExcelColumnName("目前級別")]
        public string Level { get; init; }
    }
}