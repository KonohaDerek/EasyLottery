using System.Text.Json.Serialization;
using EasyLotteryAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
// 添加其他服務
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("EasyLotteryCors", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddSingleton<OvertimeFeedStore>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();


var app = builder.Build();
   app.UseAuthentication();
    app.UseRouting();
app.UseCors("EasyLotteryCors");
    app.UseAuthorization();

app.MapGet("/", () => Results.Content("""
<!doctype html>
<html lang="zh-Hant">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>EasyLottery API</title>
  <style>
    body {
      font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      background: #0f172a;
      color: #e2e8f0;
    }
    main {
      max-width: 640px;
      padding: 2rem;
      background: rgba(15, 23, 42, 0.72);
      border: 1px solid rgba(148, 163, 184, 0.2);
      border-radius: 16px;
      box-shadow: 0 20px 60px rgba(15, 23, 42, 0.35);
    }
    code {
      display: inline-block;
      padding: 0.15rem 0.4rem;
      border-radius: 6px;
      background: rgba(148, 163, 184, 0.12);
    }
  </style>
</head>
<body>
  <main>
    <h1>EasyLottery API</h1>
    <p>服務已啟動。</p>
    <p>可用端點：<code>/api/members</code></p>
  </main>
</body>
</html>
""", "text/html"));

app.MapControllers();

await app.RunAsync();
