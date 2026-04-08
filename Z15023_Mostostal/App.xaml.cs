using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Configuration;
using System.Data;
using System.Threading.Channels;
using System.Windows;
using Z15023_Mostostal.PlcCommunication;
using Z15023_Mostostal.PlcCommunication.Drivers;
using Z15023_Mostostal.PlcCommunication.Models;
using Z15023_Mostostal.State;
using Z15023_Mostostal.Tasks;
using Z15023_Mostostal.ViewModels;

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
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File("logs/plc_app_log.txt", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 180,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] | {Message:lj}{NewLine}{Exception}",
                shared: true)
                .CreateLogger();

            try
            {
                // Nowoczesny builder DI, standard w .NET 9/10
                var builder = Host.CreateApplicationBuilder();

                // Rejestracja Serilog jako domyślnego loggera w aplikacji
                builder.Services.AddSerilog();

                // Rejestrujemy kontener danych jako jeden na całą aplikację
                builder.Services.AddSingleton<PlcDataStore>();

                // Rejestrujemy ViewModel dla okna głównego (Singleton, bo mamy jedno okno)
                builder.Services.AddSingleton<MainViewModel>();

                builder.Services.AddSingleton<PlcChannelRegistry>();

                // Rejestracja UI (Okno główne) jako Singleton
                builder.Services.AddSingleton<MainWindow>();

                builder.Services.AddSingleton(TimeProvider.System);

                // Rejestrujemy fasadę jako Singleton (żyje przez cały czas działania aplikacji)
                builder.Services.AddSingleton<PlcTaskManager>();

                // Rejestrujemy klasę, która będzie bezpiecznie przechowywać referencje do 4 fizycznych sterowników
                builder.Services.AddSingleton<PlcDriverRegistry>();


                // Rejestrujemy nasze konkretne zadanie. 
                // Używamy AddTransient, co oznacza, że obiekt zadania powstanie w pamięci 
                // TYLKO na czas jego wykonania i zaraz potem zostanie usunięty (Garbage Collector).
                builder.Services.AddTransient<Task10_SendNewOrder>();

                // Pobieranie danych połączeniowych z appsettings.json
                var plcConfigs = builder.Configuration
                    .GetSection("PlcConnections")
                    .Get<List<PlcConnectionConfig>>();

                if (plcConfigs != null)
                {
                    // 2. Rejestracja 4 niezależnych usług, każdej z osobnymi kanałami i konfiguracją
                    foreach (var config in plcConfigs)
                    {
                        builder.Services.AddHostedService(provider =>
                        {
                            var logger = provider.GetRequiredService<ILogger<PlcWorkerService>>();
                            var timeProvider = provider.GetRequiredService<TimeProvider>();
                            var dataStore = provider.GetRequiredService<PlcDataStore>();
                            var registry = provider.GetRequiredService<PlcChannelRegistry>();

                            // Pobieramy nasz nowy rejestr sterowników
                            var driverRegistry = provider.GetRequiredService<PlcDriverRegistry>();

                            // Tworzenie unikalnych kanałów dla tej konkretnej instancji PLC
                            var incomingTasks = Channel.CreateUnbounded<PlcTaskRequest>();
                            var completedTasks = Channel.CreateUnbounded<PlcTaskRequest>();
                            var outgoingTasks = Channel.CreateUnbounded<PlcTaskRequest>();

                            // Rejestrujemy kanał wychodzący w centralnym rejestrze, 
                            // aby nasza logika biznesowa mogła do niego trafić
                            registry.RegisterChannel(config.Id, outgoingTasks);

                            // Tworzymy instancję fizycznego sterownika dla danego IP
                            var loggerDriver = provider.GetRequiredService<ILogger<AsCommPlcDriver>>();
                            IPlcDriver asCommDriver = new AsCommPlcDriver(loggerDriver, config);

                            // REJESTRUJEMY STEROWNIK w słowniku pod numerem maszyny!
                            driverRegistry.RegisterDriver(config.Id, asCommDriver);

                            // Zwracamy gotową usługę dla tego sterownika
                            return new PlcWorkerService(
                                config, asCommDriver, logger, timeProvider,
                                incomingTasks, completedTasks, outgoingTasks, dataStore);
                        });
                    }
                }
                else
                {
                    Log.Warning("Brak konfiguracji 'PlcConnections' w pliku appsettings.json!");
                }

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
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Rozpoczęto procedurę zamykania aplikacji...");

            // Uruchamiamy niezależny wątek w tle. Gwarantuje to, że główny wątek WPF 
            // nie zostanie zablokowany (unikamy Deadlocka).
            Task.Run(async () =>
            {
                try
                {
                    if (_host != null)
                    {
                        // Dajemy maksymalnie 1.5 sekundy na miękkie zamknięcie
                        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));

                        // ConfigureAwait(false) dodatkowo chroni przed powrotem na wątek UI
                        await _host.StopAsync(cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("Zamykanie hosta przekroczyło limit czasu. Wymuszanie.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Błąd podczas zamykania Hosta.");
                }
                finally
                {
                    _host?.Dispose();
                    Log.CloseAndFlush(); // Bezpieczny zapis logów na dysk

                    // BEZWZGLĘDNE ZABICIE PROCESU
                    // Zostanie wykonane z wątku w tle, natychmiast zabijając całą aplikację
                    Environment.Exit(0);
                }
            });

            // Pozwalamy frameworkowi WPF natychmiast zamknąć okna graficzne
            base.OnExit(e);
        }

    }

}
