
using CtrlCenter.DataModel;
using CtrlCenter.Storage;
using CtrlCenter.View;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
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

            //register ioptionssnapshot(support(hot updates)
//           services.AddOptions<AppSetting>()
                //.Bind(configuration.GetSection("appsetting"))
              //.ValidateDataAnnotations();//option-enable verification

            var appSetting = new AppSetting();
            configuration.GetSection("AppSetting").Bind(appSetting);
            services.AddSingleton(appSetting);

            InitializeDatabase();

            //registerwindows and ViewModel
            services.AddTransient<MainWindow>();
            services.AddTransient<AppMainView>();
            //services.AddTransient<SettingsViewModel>();

            ServiceProvider = services.BuildServiceProvider();



            //starup mainwindow
            //var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            var mainWindow = ServiceProvider.GetRequiredService<AppMainView>();
            mainWindow.Show();            
        }

        private void InitializeDatabase()
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "report_his.db");                        
            var initializer = new DabInitializer(dbPath);
            initializer.EnsureDatabaseCreated();
        }

        public App()
        {
            // Load appsettings.json
            //var builder = new ConfigurationBuilder()
            //    .SetBasePath(Directory.GetCurrentDirectory())
            //    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            //Configuration = builder.Build();
        }
    }
}
