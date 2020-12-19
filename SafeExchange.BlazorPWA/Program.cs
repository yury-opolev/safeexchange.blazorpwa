
namespace SafeExchange.BlazorPWA
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using SafeExchange.Client.Web.Components;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            ServicesHelper.ConfigureServices(builder);

            await builder.Build().RunAsync();
        }
    }
}
