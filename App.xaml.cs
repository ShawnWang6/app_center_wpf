
using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using CtrlCenter.Storage;
using CtrlCenter.View;
using CtrlCenter.ViewModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text;
using System.Windows;


namespace CtrlCenter
{
    public partial class App : System.Windows.Application
    {
        public static IConfiguration Configuration { get; private set; }
        public static IServiceProvider ServiceProvider { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            Configuration = configuration;
            
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);

            var appSetting = new AppSetting();
            configuration.GetSection("AppSetting").Bind(appSetting);
            services.AddSingleton(appSetting);

            services.AddSingleton<IDbConnFactory, SqliteConnFactory>();
            services.AddSingleton<ISwitchHisRepos, SwitchHisRepos>();

            //registerwindows and ViewModel
            services.AddTransient<MainViewModel>();
            services.AddTransient<AppMainView>();

            ServiceProvider = services.BuildServiceProvider();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitializeDatabase(appSetting.DbOptions);
            var mainWindow = ServiceProvider.GetRequiredService<AppMainView>();
            mainWindow.Show();            
        }

        private void InitializeDatabase(DbOptions dbOptions)
        {
            var initializer = new DabInitializer(dbOptions.ConnString);
            initializer.EnsureDatabaseCreated();
        }

        public App()
        {
        }
    }
}
