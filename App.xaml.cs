
using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using CtrlCenter.Storage;
using CtrlCenter.Tools;
using CtrlCenter.View;
using CtrlCenter.ViewModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;


namespace CtrlCenter
{
    public partial class App : System.Windows.Application
    {
        bool firstInstance = false;
        public static IConfiguration Configuration { get; private set; }
        public static IServiceProvider ServiceProvider { get; private set; }
        public static LoggingLevelSwitch FileLevelSwitch { get; } = new LoggingLevelSwitch(LogEventLevel.Information);
        public static LoggingLevelSwitch DebugLevelSwitch { get; } = new LoggingLevelSwitch(LogEventLevel.Debug);
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);            
            var singletonMutex = new Mutex(true, "Global\\__?ReportMaker2026?__", out firstInstance);
            if (!firstInstance)
            {
                try { singletonMutex.Dispose(); } catch { }
                WindowActivator.ActivateExistingWindow();
                Shutdown(0);                
                return;
            }


            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            Configuration = configuration;

            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", ".log");
            Log.Logger = new LoggerConfiguration()                
                .MinimumLevel.Debug()
                .WriteTo.Logger(lc => lc
                    .MinimumLevel.ControlledBy(FileLevelSwitch)
                    .WriteTo.File(logPath,
                              rollingInterval: RollingInterval.Month,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .WriteTo.Logger(lc => lc
                    .MinimumLevel.ControlledBy(DebugLevelSwitch)
                    .WriteTo.Debug(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}"))            
                .CreateLogger();
            
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
