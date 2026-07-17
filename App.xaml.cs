using Microsoft.Extensions.Configuration;
using System.IO;
using System.Windows;

namespace CtrlCenter
{
    public partial class App : System.Windows.Application
    {
        public static IConfiguration Configuration { get; private set; }

        public App()
        {
            // Load appsettings.json
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();
        }
    }
}
