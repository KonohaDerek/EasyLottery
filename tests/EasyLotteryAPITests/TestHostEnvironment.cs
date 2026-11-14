using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EasyLotteryApiTests;

internal sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string ApplicationName { get; set; } = "EasyLotteryApiTests";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = contentRootPath;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
