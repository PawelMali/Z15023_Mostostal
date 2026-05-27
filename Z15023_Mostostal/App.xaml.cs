using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Configuration;
using System.Data;
using System.Threading.Channels;
using System.Windows;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Drivers;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services;
using Z25023_Mostostal.Services.RecipeManager;
using Z25023_Mostostal.Settings;
using Z25023_Mostostal.Settings.Security;
using Z25023_Mostostal.State;
using Z25023_Mostostal.Tasks.Inbound_PLC_PC;
using Z25023_Mostostal.Tasks.Outbound_PC_PLC;
using Z25023_Mostostal.ViewModels;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Z25023_Mostostal
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
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File("logs/System/System_Main.txt", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 180,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] | {Message:lj}{NewLine}{Exception}",
                shared: true)
                .WriteTo.Map("PlcName", "System", (plcName, wt) =>
                {
                    // Znak '@' ułatwia bezpieczne parsowanie nazw plików (usuwa problematyczne znaki)
                    wt.File($"logs/{plcName}/{plcName}_log_.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 180,
                    outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] | {Message:lj}{NewLine}{Exception}",
                    shared: true);
                })
                .CreateLogger();

            RegisterGlobalExceptionHandlers();

            try
            {

                // Nowoczesny builder DI, standard w .NET 9/10
                var builder = Host.CreateApplicationBuilder();


                // ==========================================
                // A. WARSTWA BAZOWA I USTAWIENIA
                // ==========================================

                // Rejestracja TimeProvider jako Singleton, aby mieć spójne źródło czasu w całej aplikacji (np. do logów, timeoutów, itp.)
                builder.Services.AddSingleton(TimeProvider.System);

                // Rejestrujemy klasę, która będzie szyfrować i deszyfrować hasła do bazy danych. Dzięki temu, nawet jeśli ktoś podejrzy nasz plik appsettings.json, hasła będą bezpieczne.
                builder.Services.AddSingleton<ICryptoService, AesCryptoService>();

                // Rejestrujemy serwis, który będzie zarządzał odczytem i zapisem ustawień aplikacji (np. połączenia do bazy, ustawienia PLC, itp.). Dzięki niemu, mamy centralne miejsce do zarządzania konfiguracją.
                builder.Services.AddSingleton<SettingsManagerService>();

                // Rejestrujemy Serilog jako domyślnego loggera w aplikacji, dzięki czemu możemy wstrzykiwać ILogger<T> w dowolnym miejscu i mieć spójne logowanie do konsoli i plików.
                builder.Services.AddSerilog();

                
                // ==========================================
                // B. WARSTWA DANYCH I RECEPTUR (SQL)
                // ==========================================

                // Rejestrujemy serwis, który będzie bezpiecznie przechowywał w pamięci RAM dane z PLC (np. aktualne zlecenie, parametry procesowe, itp.). Dzięki temu, mamy szybki dostęp do tych danych z różnych miejsc w aplikacji, bez konieczności ciągłego odczytywania ich z PLC.
                builder.Services.AddSingleton<PlcDataStore>();

                // Rejestrujemy serwis, który będzie zarządzał definicjami parametrów procesowych (100 parametrów dla każdego produktu). Wczytywane z csv przy starcie, trzymane w RAMie, gotowe do szybkiego mapowania na strukturę dla PLC. 
                builder.Services.AddSingleton<ParameterDefinitionService>();

                // Rejestrujemy serwis, który będzie generował unikalne hashe dla produktów na podstawie ich wymiarów i cech. Dzięki temu, nawet jeśli zlecenia będą miały różne numery, ale identyczne wymiary, będą traktowane jako ten sam produkt.
                builder.Services.AddSingleton<ProductHashGenerator>();

                // Rejestrujemy repozytorium, które będzie zarządzało zapisem i odczytem receptur (definicji produktów) w lokalnej bazie SQL. Dzięki temu, możemy łatwo zapisywać nowe receptury z PLC (Task 50) i pobierać je przy wysyłaniu zlecenia (Task 10).
                builder.Services.AddTransient<RecipeRepository>();

                // Rejestrujemy repozytorium, które będzie zarządzało zapisem i odczytem zleceń produkcyjnych w lokalnej bazie SQL. Dzięki temu, możemy mieć historię zleceń, ich statusów, itp., co może być przydatne do raportowania i analizy.
                builder.Services.AddSingleton<OrderRepository>();


                // ==========================================
                // C. WARSTWA ZADAŃ (TASK ROUTING)
                // Ruch wychodzący (PC -> PLC)
                // ==========================================
                // Rejestrujemy centralny dispatcher zadań wychodzących do PLC. To on będzie odpowiedzialny za przyjmowanie żądań z różnych miejsc w aplikacji (np. Task 10) i kierowanie ich do odpowiednich kanałów komunikacyjnych, które są przypisane do konkretnych PLC. Dzięki temu, logika wysyłania zleceń jest odseparowana od reszty aplikacji i łatwa do zarządzania.
                builder.Services.AddSingleton<OutboundTaskDispatcher>();

                // Rejestracja konkretnego zadania wysyłającego zlecenie do PLC (Task 10). To zadanie będzie wywoływane z różnych miejsc w aplikacji (np. z UI, z innych serwisów) i będzie odpowiedzialne za przygotowanie danych zlecenia, mapowanie ich na strukturę dla PLC, oraz wysłanie ich do odpowiedniego kanału komunikacyjnego. 
                builder.Services.AddTransient<Task10_SendNewOrder>();


                // ==========================================
                // C. WARSTWA ZADAŃ (TASK ROUTING)
                // Ruch przychodzący (PLC -> PC)
                // ==========================================
                // Rejestrujemy centralny router zadań przychodzących z PLC. To on będzie odpowiedzialny za odbieranie sygnałów z maszyn (np. Task 50) i kierowanie ich do odpowiednich handlerów, które są przypisane do konkretnych numerów zadań. Dzięki temu, logika obsługi sygnałów z PLC jest odseparowana i łatwa do zarządzania.
                builder.Services.AddSingleton<InboundTaskRouter>();

                // Rejestracja konkretnego handlera dla zadania przychodzącego z PLC (Task 50 - Zapis Receptury). To zadanie będzie wywoływane, gdy maszyna wyśle sygnał, że chce zapisać recepturę. Handler ten będzie odpowiedzialny za odczyt danych z PLC, wygenerowanie hasha produktu, i zapisanie tych danych do lokalnej bazy SQL jako nowa receptura.
                builder.Services.AddTransient<IInboundTaskHandler, Task50_SaveRecipeHandler>();

                // Rejestracja konkretnego handlera dla zadania przychodzącego z PLC (Task 51 - Obsługa Żądania Zlecenia). To zadanie będzie wywoływane, gdy maszyna wyśle sygnał, że chce otrzymać dane zlecenia. Handler ten będzie odpowiedzialny za odczyt żądania z PLC, wyszukanie zlecenia w bazie ERP, wygenerowanie hasha produktu, pobranie receptury, i wysłanie tych danych z powrotem do PLC.
                builder.Services.AddTransient<IInboundTaskHandler, Task51_HandleOrderRequest>();

                // Rejestracja konkretnego handlera dla zadania przychodzącego z PLC (Task 52 - Aktualizacja Produkcji). To zadanie będzie wywoływane, gdy maszyna wyśle sygnał, że chce zaktualizować postęp produkcji (np. licznik wyprodukowanych sztuk). Handler ten będzie odpowiedzialny za odczyt aktualnego stanu produkcji z PLC, i zapisanie tych danych do lokalnej bazy SQL jako aktualizacja stanu zlecenia.
                builder.Services.AddTransient<IInboundTaskHandler, Task52_UpdateProductionHandler>();

                // Rejestracja konkretnego handlera dla zadania przychodzącego z PLC (Task 53 - Zakończenie Przesyłki). To zadanie będzie wywoływane, gdy maszyna wyśle sygnał, że zakończyła produkcję całej przesyłki. Handler ten będzie odpowiedzialny za odczyt danych z PLC, mapowanie ich na model zlecenia, i zapisanie tych danych do lokalnej bazy SQL jako zakończenie zlecenia (np. zapis do tabeli CompletedShipments).
                builder.Services.AddTransient<IInboundTaskHandler, Task53_FinishShipmentHandler>();

                // Rejestracja konkretnego handlera dla zadania przychodzącego z PLC (Task 60 - Przeliczenie i optymalizacja pętli cięcia). To zadanie będzie wywoływane, gdy maszyna wyśle sygnał, że chce przeliczyć parametry cięcia. Handler ten będzie odpowiedzialny za odczyt aktualnych parametrów z PLC, uruchomienie silnika matematycznego do optymalizacji cięcia, i zapisanie wyników z powrotem do PLC.
                builder.Services.AddTransient<IInboundTaskHandler, Task60_CalculateCuttingHandler>();

                // Rejestracja konkretnego handlera dla zadania przychodzącego z PLC (Task 54 - Pobranie konfiguracji dla aktualnego zlecenia). To zadanie będzie wywoływane, gdy maszyna wyśle sygnał, że chce pobrać parametry technologiczne dla aktualnego zlecenia. Handler ten będzie odpowiedzialny za odczyt danych z PLC, wygenerowanie hasha produktu, pobranie receptury z bazy SQL, i wysłanie tych danych z powrotem do PLC.
                builder.Services.AddTransient<IInboundTaskHandler, Task54_ReloadConfigHandler>();


                // ==========================================
                // D. WARSTWA INTERFEJSU UŻYTKOWNIKA (WPF)
                // ==========================================

                // Rejestrujemy główny ViewModel aplikacji jako Singleton, ponieważ chcemy mieć spójny stan i logikę biznesową w całej aplikacji, a także łatwy dostęp do niego z różnych miejsc (np. z okna głównego, z innych serwisów, itp.). Dzięki temu, możemy centralnie zarządzać danymi i logiką aplikacji, a UI będzie tylko prezentacją tego stanu.
                builder.Services.AddSingleton<MainViewModel>();

                // Rejestrujemy główne okno aplikacji jako Singleton, ponieważ chcemy mieć tylko jedną instancję tego okna w całym cyklu życia aplikacji. Dzięki temu, możemy łatwo wstrzykiwać do niego zależności (np. MainViewModel) i mieć pewność, że wszędzie tam, gdzie potrzebujemy odwołać się do głównego okna, będziemy korzystać z tej samej instancji.
                builder.Services.AddSingleton<MainWindow>();

                // Rejestrujemy ViewModel dla okna ustawień jako Transient, ponieważ chcemy mieć świeżą instancję tego ViewModelu za każdym razem, gdy otwieramy okno ustawień. Dzięki temu, każde otwarcie okna będzie miało niezależny stan i ewentualne zmiany wprowadzone w jednym oknie nie będą wpływać na inne otwarte okna ustawień.
                builder.Services.AddTransient<SettingsViewModel>();

                // Rejestrujemy okno ustawień jako Transient, ponieważ chcemy mieć świeżą instancję tego okna za każdym razem, gdy użytkownik zdecyduje się otworzyć ustawienia. Dzięki temu, każde otwarcie okna będzie niezależne i nie będzie wpływać na inne otwarte okna ustawień (jeśli użytkownik otworzy kilka razy).
                builder.Services.AddTransient<SettingsWindow>();


                // ==========================================
                // E. WARSTWA KOMUNIKACJI PLC (PĘTLE W TLE)
                // ==========================================

                // Rejestrujemy centralny rejestr sterowników PLC. To on będzie bezpiecznie przechowywał referencje do wszystkich fizycznych sterowników (np. AsComm) przypisanych do konkretnych numerów maszyn. Dzięki temu, w dowolnym miejscu w aplikacji, możemy łatwo pobrać sterownik dla konkretnej maszyny i wykonać na nim operacje odczytu/zapisu.
                builder.Services.AddSingleton<PlcDriverRegistry>();

                // Rejestrujemy centralny rejestr kanałów komunikacyjnych. To on będzie bezpiecznie przechowywał referencje do kanałów (Channel<PlcTaskRequest>) dla każdego PLC, które są używane do wysyłania zadań z aplikacji do maszyn. Dzięki temu, gdy chcemy wysłać zadanie do konkretnej maszyny, możemy łatwo pobrać odpowiedni kanał z tego rejestru i wrzucić tam żądanie zadania.
                builder.Services.AddSingleton<PlcChannelRegistry>();


                var plcConfigs = builder.Configuration
                    .GetSection("PlcConnections")
                    .Get<List<PlcConnectionConfig>>();

                // Rejestrujemy listę konfiguracji PLC jako Singleton, aby była dostępna w całej aplikacji (np. do wyświetlania w UI, do logowania, itp.)
                builder.Services.AddSingleton(plcConfigs);

                if (plcConfigs != null)
                {
                    // 2. Rejestracja 4 niezależnych usług, każdej z osobnymi kanałami i konfiguracją
                    foreach (var configItem in plcConfigs)
                    {
                        var config = configItem;
                        builder.Services.AddSingleton<IHostedService>(provider =>
                        {
                            var logger = provider.GetRequiredService<ILogger<PlcWorkerService>>();
                            var timeProvider = provider.GetRequiredService<TimeProvider>();
                            var dataStore = provider.GetRequiredService<PlcDataStore>();

                            var channelRegistry = provider.GetRequiredService<PlcChannelRegistry>();
                            var driverRegistry = provider.GetRequiredService<PlcDriverRegistry>();

                            // Pobieramy nasz nowy router dla żądań przychodzących (Task 50+)
                            var inboundRouter = provider.GetRequiredService<InboundTaskRouter>();

                            // Tworzenie unikalnych kanałów dla tej konkretnej instancji PLC
                            var incomingTasks = Channel.CreateUnbounded<PlcTaskRequest>();
                            var completedTasks = Channel.CreateUnbounded<PlcTaskRequest>();
                            var outgoingTasks = Channel.CreateUnbounded<PlcTaskRequest>();

                            // Rejestrujemy kanał wychodzący w centralnym rejestrze, 
                            // aby nasza logika biznesowa mogła do niego trafić
                            channelRegistry.RegisterChannel(config.Id, outgoingTasks);

                            // Tworzymy instancję fizycznego sterownika dla danego IP
                            var loggerDriver = provider.GetRequiredService<ILogger<AsCommPlcDriver>>();
                            IPlcDriver asCommDriver = new AsCommPlcDriver(loggerDriver, config);
                            // REJESTRUJEMY STEROWNIK w słowniku pod numerem maszyny!
                            driverRegistry.RegisterDriver(config.Id, asCommDriver);

                            // Zwracamy gotową usługę dla tego sterownika
                            return new PlcWorkerService(
                                config, asCommDriver, logger, inboundRouter, timeProvider,
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


        /// <summary>
        /// Metoda rejestrująca potrójną tarczę ochronną przed crashami aplikacji.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            // 1. Wyjątki w głównym wątku interfejsu użytkownika (WPF Dispatcher Thread)
            // To tutaj trafi błąd, gdy klikniesz przycisk "Odśwież zlecenia" przy braku bazy.
            this.DispatcherUnhandledException += (sender, e) =>
            {
                // Logujemy krytyczny błąd do pliku za pomocą Seriloga
                Log.Fatal(e.Exception, "Krytyczny nieobsłużony wyjątek w wątku UI (Dispatcher)");

                // Wyświetlamy operatorowi bezpieczny i przejrzysty komunikat
                MessageBox.Show(
                    $"Wystąpił nieoczekiwany błąd działania interfejsu.\n\n" +
                    $"Szczegóły błędu: {e.Exception.Message}\n\n" +
                    $"Aplikacja spróbuje kontynuować działanie. Jeśli problem będzie się powtarzał, skontaktuj się z serwisem.",
                    "Błąd Aplikacji (UI)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // KLUCZOWE: Oznaczamy wyjątek jako obsłużony. 
                // Dzięki temu WPF NIE UBIJE procesu aplikacji i okno główne nie zniknie!
                e.Handled = true;
            };

            // 2. Wyjątki w wątkach tła (ThreadPool, asynchroniczne wątki spoza Dispatchera)
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Exception? ex = e.ExceptionObject as Exception;
                Log.Fatal(ex, "Krytyczny nieobsłużony wyjątek w domenie aplikacji (Wątek w tle). Czy aplikacja kończy działanie: {IsTerminating}", e.IsTerminating);

                MessageBox.Show(
                    $"Wystąpił krytyczny błąd systemowy w tle maszynowym.\n\n" +
                    $"Szczegóły: {ex?.Message}\n\n" +
                    $"Aplikacja musi zostać zamknięta.",
                    "Krytyczny Błąd Systemu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);

                // W przypadku AppDomain runtime i tak zazwyczaj ubije proces (IsTerminating = true), 
                // ale dzięki temu zdarzeniu zdążyliśmy zapisać pełny StackTrace w logach!
            };

            // 3. Wyjątki w nieobserwowanych zadaniach asynchronicznych (Task / async/await)
            // Wywoływane przez Garbage Collector, gdy porzucony Task zgłosi błąd, którego nikt nie przechwycił przez 'await'.
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error(e.Exception, "Nieobserwowany wyjątek w zadaniu asynchronicznym (Task Scheduler)");

                // Zabezpieczamy potok i zapobiegamy eskalacji błędu do awarii procesu
                e.SetObserved();
            };
        }


        // Nadpisujemy moment startu aplikacji
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (_host != null)
            {
                // Uruchomienie hosta odpali również wszystkie nasze przyszłe BackgroundServices (PLC)
                await _host.StartAsync();

                // Wyciągamy serwis z kontenera. To uruchomi jego konstruktor 
                // i natychmiast wczyta pliki "parameters_X.csv" do RAMu.
                _host.Services.GetRequiredService<ParameterDefinitionService>();

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
