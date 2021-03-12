using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(ContainerRunnerFuncApp.Startup))]
namespace ContainerRunnerFuncApp
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<ContainerRunnerLib>(new ContainerRunnerLib());
        }
    }
}