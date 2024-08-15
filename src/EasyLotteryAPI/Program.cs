using System.Text.Json.Serialization;
using EasyLotteryAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
// 添加其他服務
builder.Services.AddControllers();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();


var app = builder.Build();
   app.UseAuthentication();
    app.UseRouting();
    app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
