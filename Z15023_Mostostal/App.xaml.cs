using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Configuration;
using System.Data;
using System.Windows;

namespace Z15023_Mostostal
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        public App()
        {
            // Wstępna konfiguracja Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/plc_app_log.txt", rollingInterval: RollingInterval.Day,retainedFileCountLimit: 60)
                .CreateLogger();

            try
            {
                // Nowoczesny builder DI, standard w .NET 9/10
                var builder = Host.CreateApplicationBuilder();

                // Rejestracja Serilog jako domyślnego loggera w aplikacji
                builder.Services.AddSerilog();

                // Rejestracja UI (Okno główne) jako Singleton
                builder.Services.AddSingleton<MainWindow>();

                // Zostawiamy miejsce na nasze usługi:
                // builder.Services.AddHostedService<PlcWorkerService>();
                // builder.Services.AddSingleton<OrderManagerService>();

                _host = builder.Build();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Błąd podczas inicjalizacji Hosta aplikacji");
            }
        }

        // Nadpisujemy moment startu aplikacji
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (_host != null)
            {
                // Uruchomienie hosta odpali również wszystkie nasze przyszłe BackgroundServices (PLC)
                await _host.StartAsync();

                // Pobranie głównego okna z kontenera DI i wyświetlenie go
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
        }

        // Nadpisujemy moment zamknięcia aplikacji, aby czysto zamknąć usługi
        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                Log.Information("Zamykanie aplikacji...");
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }

    }

}
