using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductionApp.PlcCommunication;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using Z15023_Mostostal.PlcCommunication.Drivers;
using Z15023_Mostostal.PlcCommunication.Models;
using Z15023_Mostostal.State;

namespace Z15023_Mostostal.PlcCommunication
{
    public class PlcWorkerService(
        PlcConnectionConfig _config,
        IPlcDriver _plcDriver,
        ILogger<PlcWorkerService> _logger,
        TimeProvider _timeProvider,
        Channel<PlcTaskRequest> _incomingTasksFromPlc,   // Kanał: Zlecenia z PLC do Aplikacji
        Channel<PlcTaskRequest> _completedTasksFromApp,  // Kanał: Potwierdzenia z Aplikacji, że zadanie wykonano
        Channel<PlcTaskRequest> _outgoingTasksToPlc,     // Kanał: Zlecenia z Aplikacji do PLC
        PlcDataStore _dataStore)
    : BackgroundService
    {
        private readonly int _plcId = _config.Id; // Przypisanie dla wygody

        // Stan i timeouty dla kierunku: OD PLC DO PC
        private FromPlcState _fromPlcState = FromPlcState.Idle;
        private long _fromPlcStateStart;
        private short _currentFromPlcTaskId = 0; // Pamiętamy aktualnie przetwarzane zadanie od PLC

        // Stan i timeouty dla kierunku: OD PC DO PLC
        private ToPlcState _toPlcState = ToPlcState.Idle;
        private long _toPlcStateStart;

        private PlcTaskRequest? _currentToPlcRequest = null; // Zapisujemy aktualne żądanie

        // Lokalny bufor do zapisu (odwzorowanie pamięci PLC)
        private readonly SiemensDataWrite _writeBuffer = new();

        // UWAGA: Skoro aplikacja wykonuje operacje SQL, timeout musi być odpowiednio długi!
        // Jeśli SQL może potrwać dłużej, należy zwiększyć tę wartość.
        private const int TimeoutMs = 5000;

        //Dynamiczny czas oczekiwania (startujemy od 1 sekundy)
        private int _currentReconnectDelayMs = 1000;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Inicjalizacja PLC {PlcId} [{Name}] pod adresem {IpAddress}...", _config.Id, _config.Name, _config.IpAddress);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100), _timeProvider);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // 1. BLOKADA BŁĘDU KONFIGURACYJNEGO
                    if (_plcDriver.HasConfigurationError)
                    {
                        // Zły DB! Usypiamy ten wątek na długo (np. 10 sekund), 
                        // aby nie zżerać procesora, ale nie ubijamy samej pętli na wypadek, 
                        // gdyby reszta PLC działała poprawnie.
                        await Task.Delay(10000, stoppingToken);
                        continue;
                    }

                    // 2. AUTO-RECONNECT DLA BŁĘDÓW SIECIOWYCH
                    if (!_plcDriver.IsConnected)
                    {
                        bool connected = await _plcDriver.ConnectAsync();

                        if (!connected)
                        {
                            // 1. Usypiamy pętlę na DYNAMICZNY czas (najpierw tylko 1s)
                            await Task.Delay(_currentReconnectDelayMs, stoppingToken);

                            // 2. Jeśli to stała awaria, wydłużamy czas kolejnych prób, max do 10 sekund
                            if (_currentReconnectDelayMs < 10000)
                            {
                                // Zwiększamy o 3 sekundy po każdej nieudanej próbie
                                _currentReconnectDelayMs += 1000;
                                if (_currentReconnectDelayMs > 10000)
                                    _currentReconnectDelayMs = 10000;
                            }

                            continue; // Przerywamy ten cykl
                        }
                        else
                        {
                            // 3. SUKCES! W przypadku udanego powrotu zasilania/kabla
                            // natychmiast resetujemy karę czasową z powrotem do 1 sekundy
                            _currentReconnectDelayMs = 1000;
                        }
                    }

                    // 3. ODCZYT DANYCH Z UŻYCIEM ADRESU ZE STRINGA
                    var readData = await _plcDriver.ReadAreaAsync<SiemensDataRead>("ReadData");

                    if (readData == null)
                        continue; // Jeśli odczyt zawiódł, pomijamy cykl (połączenie mogło spaść)

                    _dataStore.UpdateData(_plcId, readData);

                    // 3. PRZETWARZANIE MASZYN STANÓW (Równoległe logicznie)
                    ProcessFromPlcStateMachine(readData);
                    ProcessToPlcStateMachine(readData);

                    // 4. Sprawdzenie Timeoutów
                    CheckTimeouts();

                    // 5. ZAPIS DANYCH Z UŻYCIEM ADRESU ZE STRINGA
                    await _plcDriver.WriteAreaAsync("WriteData", _writeBuffer);
                }
                catch (OperationCanceledException)
                {
                    // ROZWIĄZANIE PROBLEMU Z ZAMYKANIEM (ZOMBIE PROCESS)
                    // Kiedy klikniesz X, stoppingToken przerywa await. Wpadamy tutaj!
                    _logger.LogInformation("Zatrzymywanie pętli komunikacyjnej dla PLC {PlcId}...", _plcId);
                    break; // Wychodzimy z pętli while! Usługa grzecznie się zamyka.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Krytyczny błąd pętli głównej w PLC {PlcId}", _plcId);
                    EnterGlobalError();
                }
            }
        }

        /// <summary>
        /// Maszyna stanów dla zadań przychodzących Z PLC DO PC
        /// </summary>
        private void ProcessFromPlcStateMachine(SiemensDataRead readData)
        {
            if (_fromPlcState == FromPlcState.Error) return;

            switch (_fromPlcState)
            {
                case FromPlcState.Idle:
                    // KROK 1: PLC zgłasza zadanie
                    if (readData.Task_Send_To_PC > 0)
                    {
                        _currentFromPlcTaskId = readData.Task_Send_To_PC;
                        _logger.LogInformation("PLC {PlcId} zleca zadanie: {TaskId}. Przekazano do przetwarzania w tle.", _plcId, _currentFromPlcTaskId);

                        // Wrzucamy do logiki biznesowej
                        _incomingTasksFromPlc.Writer.TryWrite(new PlcTaskRequest(readData.Task_Send_To_PC));

                        // ZMIANA: Przechodzimy w stan oczekiwania na aplikację, NIE potwierdzamy jeszcze w PLC
                        ChangeFromPlcState(FromPlcState.ProcessingInApp);
                    }
                    break;

                case FromPlcState.ProcessingInApp:
                    // KROK 2: Czekamy, aż logika SQL/WPF zgłosi wykonanie zadania w nowym kanale
                    if (_completedTasksFromApp.Reader.TryRead(out PlcTaskRequest completedTaskId))
                    {
                        // Upewniamy się, że aplikacja zrealizowała właściwe zadanie
                        if (completedTaskId.TaskId == _currentFromPlcTaskId)
                        {
                            _logger.LogInformation("Aplikacja pomyślnie wykonała zadanie {TaskId}. Wysyłam potwierdzenie do PLC.", completedTaskId);

                            // DOPIERO TERAZ potwierdzamy do PLC
                            _writeBuffer.Task_Confirm_From_PC = _currentFromPlcTaskId;
                            ChangeFromPlcState(FromPlcState.WaitingForPlcReset);
                        }
                        else
                        {
                            _logger.LogWarning("Otrzymano potwierdzenie dla złego zadania. Oczekiwano: {Current}, Otrzymano: {Completed}", _currentFromPlcTaskId, completedTaskId);
                        }
                    }
                    break;

                case FromPlcState.WaitingForPlcReset:
                    // KROK 3: Czekamy aż PLC zarejestruje nasze potwierdzenie i wyzeruje request
                    if (readData.Task_Send_To_PC == 0)
                    {
                        _writeBuffer.Task_Confirm_From_PC = 0;
                        _currentFromPlcTaskId = 0;
                        ChangeFromPlcState(FromPlcState.Idle);
                    }
                    break;
            }
        }

        /// <summary>
        /// Maszyna stanów dla zadań wysyłanych Z PC DO PLC
        /// </summary>
        private void ProcessToPlcStateMachine(SiemensDataRead readData)
        {
            if (_toPlcState == ToPlcState.Error) return;

            switch (_toPlcState)
            {
                case ToPlcState.Idle:
                    // Sprawdzamy czy aplikacja (np. WPF/SQL) ma coś do wysłania do PLC
                    if (_outgoingTasksToPlc.Reader.TryRead(out PlcTaskRequest pendingRequest))
                    {
                        _currentToPlcRequest = pendingRequest;
                        _writeBuffer.Task_Recived_From_PC = _currentToPlcRequest.TaskId;
                        ChangeToPlcState(ToPlcState.WaitingForPlcConfirm);
                    }
                    break;

                case ToPlcState.WaitingForPlcConfirm:
                    // Czekamy, aż PLC potwierdzi odebranie naszego zadania
                    if (readData.Task_Confirm_To_PC == _currentToPlcRequest!.TaskId)
                    {
                        // PLC odebrał, więc zerujemy nasz rozkaz wysłania
                        _writeBuffer.Task_Recived_From_PC = 0;
                        ChangeToPlcState(ToPlcState.WaitingForPlcReset);
                    }
                    break;

                case ToPlcState.WaitingForPlcReset:
                    // Czekamy, aż PLC wyzeruje swoje potwierdzenie
                    if (readData.Task_Confirm_To_PC == 0)
                    {
                        // SUKCES! PLC wyzerował stan. Powiadamiamy kod biznesowy!
                        _currentToPlcRequest!.Tcs.TrySetResult(true);

                        _currentToPlcRequest = null;
                        ChangeToPlcState(ToPlcState.Idle);
                    }
                    break;
            }
        }

        private void CheckTimeouts()
        {
            long currentTime = _timeProvider.GetTimestamp();

            // Sprawdzamy Timeout dla kierunku Do PLC (tylko jeśli nie jest Idle/Error)
            if (_toPlcState is not ToPlcState.Idle and not ToPlcState.Error)
            {
                if (_timeProvider.GetElapsedTime(_toPlcStateStart).TotalMilliseconds > TimeoutMs)
                {
                    _logger.LogWarning("Timeout zadania ToPlc dla PLC {PlcId}", _plcId);
                    // 1. Zwalniamy zablokowany wątek UI! 
                    // Rzucenie wyjątku rozwiązuje `await taskRequest.Tcs.Task` w Twoim Task
                    _currentToPlcRequest?.Tcs.TrySetException(new TimeoutException("Brak odpowiedzi od PLC"));

                    // 2. Czyścimy referencję do zadania
                    _currentToPlcRequest = null;

                    // 3. Wycofujemy nasz rozkaz wysłania (zerujemy pamięć w PLC)
                    _writeBuffer.Task_Recived_From_PC = 0; // Awaryjne zerowanie

                    // 4. KLUCZOWE: Wracamy do stanu Idle, zamiast blokować maszynę w stanie Error!
                    // Dzięki temu kolejne kliknięcie przycisku w WPF zostanie od razu obsłużone.
                    ChangeToPlcState(ToPlcState.Idle);
                }
            }

            // Sprawdzamy Timeout dla kierunku Od PLC
            if (_fromPlcState is not FromPlcState.Idle and not FromPlcState.Error)
            {
                if (_timeProvider.GetElapsedTime(_fromPlcStateStart).TotalMilliseconds > TimeoutMs)
                {
                    _logger.LogWarning("Timeout zadania FromPlc dla PLC {PlcId}", _plcId);

                    // Awaryjne wyzerowanie naszego potwierdzenia dla PLC
                    _writeBuffer.Task_Confirm_From_PC = 0; // Awaryjne zerowanie

                    // Powrót do nasłuchiwania nowych zadań od PLC
                    ChangeFromPlcState(FromPlcState.Idle);

                }
            }
        }

        private void ChangeFromPlcState(FromPlcState newState)
        {
            _fromPlcState = newState;
            _fromPlcStateStart = _timeProvider.GetTimestamp();
        }

        private void ChangeToPlcState(ToPlcState newState)
        {
            _toPlcState = newState;
            _toPlcStateStart = _timeProvider.GetTimestamp();
        }

        private void EnterGlobalError()
        {
            ChangeFromPlcState(FromPlcState.Error);
            ChangeToPlcState(ToPlcState.Error);
            // Tutaj można dodać wysłanie eventu do interfejsu WPF o błędzie
        }
    }
}
