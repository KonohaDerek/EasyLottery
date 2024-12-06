using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyLotteryDomain.Models.Entities
{
    public class SystemSetting
    {

        [Key]
        [Description("Unique identifier for the system setting")]
        public int Id { get; set; }

        [Description("The API key for the Youtube API")]
        public string YoutubeApiKey { get; set; }   = "";

        [Description("The Youtube channel ID")]
        public string YoutubeCredentialsJson { get; set; } = "";

        [Description("The API key for the OpenAI API")]
        public string OpenAIKey { get; set; } = "";
    }

     public class SystemSettingEntityTypeConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.HasKey(e => e.Id);
        }
    }
}