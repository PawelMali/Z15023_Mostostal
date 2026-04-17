using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Z25023_Mostostal.PlcCommunication;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using Z25023_Mostostal.PlcCommunication.Drivers;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.State;
using Z25023_Mostostal.Tasks.Inbound_PLC_PC;
using System.Windows.Documents;

namespace Z25023_Mostostal.PlcCommunication
{
    public class PlcWorkerService(
        PlcConnectionConfig _config,
        IPlcDriver _plcDriver,
        ILogger<PlcWorkerService> _logger,
        InboundTaskRouter _inboundRouter,
        TimeProvider _timeProvider,
        Channel<PlcTaskRequest> _incomingTasksFromPlc,   // Kanał: Zlecenia z PLC do Aplikacji
        Channel<PlcTaskRequest> _completedTasksFromApp,  // Kanał: Potwierdzenia z Aplikacji, że zadanie wykonano
        Channel<PlcTaskRequest> _outgoingTasksToPlc,     // Kanał: Zlecenia z Aplikacji do PLC
        PlcDataStore _dataStore)
    : BackgroundService
    {
        private readonly int _plcId = _config.Id; // Przypisanie dla wygody

        // Stan i timeouty dla kierunku: OD PLC DO PC
        private long _fromPlcStateStart;
        private short _currentFromPlcTaskId = 0; // Pamiętamy aktualnie przetwarzane zadanie od PLC

        // Zmienne do odczytu aktualnego zlecenia na plc 
        private long _lastOrderReadTimestamp;
        private const int OrderReadIntervalMs = 500; // 5 sekund

        // Stan i timeouty dla kierunku: OD PC DO PLC
        private ToPlcState _toPlcState = ToPlcState.Idle;
        private long _toPlcStateStart;

        private PlcTaskRequest? _currentToPlcRequest = null; // Zapisujemy aktualne żądanie

        // Lokalny bufor do zapisu (odwzorowanie pamięci PLC)
        private readonly SiemensWriteData _writeBuffer = new();

        // UWAGA: Skoro aplikacja wykonuje operacje SQL, timeout musi być odpowiednio długi!
        // Jeśli SQL może potrwać dłużej, należy zwiększyć tę wartość.
        private const int TaskTimeoutMs = 5000;

        //Dynamiczny czas oczekiwania (startujemy od 1 sekundy)
        private int _currentReconnectDelayMs = 1000;

        // ZMIENNE DO WATCHDOGA (Zmienna Life)
        private short? _lastLifeValue = null;
        private long _lastLifeChangeTimestamp;
        private const int HeartbeatTimeoutMs = 2500; // 2.5 sekundy na zmianę zmiennej Life
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ==========================================
            // Wszystko, co wykona się wewnątrz tych klamer (nawet zagnieżdżone metody
            // wewnątrz innych klas, np. w AsCommPlcDriver), zostanie w tle otagowane nazwą tej maszyny!
            // ==========================================
            using (LogContext.PushProperty("PlcName", _config.Name))
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
                            _dataStore.ClearData(_plcId);
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
                                _lastLifeChangeTimestamp = _timeProvider.GetTimestamp();
                                _currentReconnectDelayMs = 1000;
                            }
                        }

                        // 3. ODCZYT DANYCH Z UŻYCIEM ADRESU ZE STRINGA
                        var readData = await _plcDriver.ReadAreaAsync<SiemensReadData>("ReadData");

                        if (readData == null)
                        {
                            _dataStore.ClearData(_plcId); // Czyścimy dane
                            continue; // Jeśli odczyt zawiódł, pomijamy cykl (połączenie mogło spaść)
                        }


                        // 3.1. WERYFIKACJA WATCHDOGA (HEARTBEAT)
                        if (readData.Life != _lastLifeValue)
                        {
                            // Wartość się zmieniła = PLC ŻYJE
                            _lastLifeValue = readData.Life;
                            _lastLifeChangeTimestamp = _timeProvider.GetTimestamp();
                        }
                        else if (_timeProvider.GetElapsedTime(_lastLifeChangeTimestamp).TotalMilliseconds > HeartbeatTimeoutMs)
                        {
                            _logger.LogWarning("Brak sygnału Life od {Timeout}ms w PLC {PlcId}! PLC w trybie STOP lub awaria pętli logiki.", HeartbeatTimeoutMs, _plcId);

                            // Czyścimy dane, UI wyświetli "BRAK POŁĄCZENIA"
                            _dataStore.ClearData(_plcId);

                            // Zerujemy maszynę stanów (bezpieczeństwo)
                            ChangeToPlcState(ToPlcState.Idle);

                            // Usypiamy na chwilę, aby nie spamować logów z prędkością 100ms
                            await Task.Delay(5000, stoppingToken);

                            // Wymuszamy restart fizycznego połączenia (często pomaga, gdy PLC wstaje po restarcie)
                            // (Zakładając że dodałeś metodę Disconnect do interfejsu, albo po prostu pomijamy cykl)
                            continue;
                        }

                        // 3.2. ODCZYT WOLNY (co 5 sekund) - AKTUALNE ZLECENIE
                        if (_timeProvider.GetElapsedTime(_lastOrderReadTimestamp).TotalMilliseconds > OrderReadIntervalMs)
                        {
                            var currentOrder = await _plcDriver.ReadAreaAsync<SiemensOrderData>("ReadOrder");
                            var currentConfig = await _plcDriver.ReadAreaAsync<SiemensConfigData>("ReadConfig");

                            if (currentOrder != null) _dataStore.UpdateCurrentOrder(_plcId, currentOrder);
                            if (currentConfig != null) _dataStore.UpdateCurrentConfig(_plcId, currentConfig);

                            _lastOrderReadTimestamp = _timeProvider.GetTimestamp();
                        }

                        _dataStore.UpdateData(_plcId, readData);

                        // 4. WYKRYCIE NOWEGO ZADANIA (50-99)
                        int incomingTaskId = readData.Task_Send_To_PC;
                        if (incomingTaskId >= 50 && _writeBuffer.Task_Confirm_From_PC != incomingTaskId)
                        {
                            _logger.LogInformation("Wykryto żądanie Task {TaskId} od maszyny {PlcId}", incomingTaskId, _plcId);
                            UILoggerStartTask(incomingTaskId);

                            // Dyspozytor znajduje klasę, wykonuje jej kod SQL i zwraca wynik
                            bool success = await _inboundRouter.RouteAsync(incomingTaskId, _plcId, readData);
                            UILoggerEndTaskTask(incomingTaskId, success);

                            // Ustawiamy Handshake dla sterownika
                            _writeBuffer.Task_Confirm_From_PC = (short)incomingTaskId;

                            // Jeśli SQL zapisał poprawnie -> 0, jeśli był błąd -> 1
                            _writeBuffer.Error_Status = (short)(success ? 1 : 2);
                        }
                        // 5. RESETOWANIE HANDSHAKE (Gdy PLC odbierze potwierdzenie)
                        else if (incomingTaskId == 0 && _writeBuffer.Task_Confirm_From_PC > 0)
                        {
                            _writeBuffer.Task_Confirm_From_PC = 0;
                            _writeBuffer.Error_Status = 0;
                            _logger.LogDebug("Handshake dla Taska wyzerowany poprawnie przez PLC.");
                        }

                        // 6. PRZETWARZANIE MASZYN STANÓW (Równoległe logicznie)
                        ProcessToPlcStateMachine(readData);

                        // 7. Sprawdzenie Timeoutów
                        CheckTimeouts();

                        // 8. ZAPIS DANYCH Z UŻYCIEM ADRESU ZE STRINGA
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
        }

        /// <summary>
        /// Maszyna stanów dla zadań wysyłanych Z PC DO PLC
        /// </summary>
        private void ProcessToPlcStateMachine(SiemensReadData readData)
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
                if (_timeProvider.GetElapsedTime(_toPlcStateStart).TotalMilliseconds > TaskTimeoutMs)
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
        }

        /// <summary>
        /// Odczyt NA ŻĄDANIE. Wywoływane przez Task 11 (Odbiór parametrów z maszyny).
        /// </summary>
        public async Task<(SiemensOrderData? Order, SiemensConfigData? Config)> ReadCurrentProductionDataOnDemandAsync()
        {
            if (!_plcDriver.IsConnected) return (null, null);

            // Czytamy synchronicznie oba bloki naraz, aby mieć pewność zgodności danych
            var order = await _plcDriver.ReadAreaAsync<SiemensOrderData>("ReadOrder");
            var config = await _plcDriver.ReadAreaAsync<SiemensConfigData>("ReadConfig");

            return (order, config);
        }



        private void ChangeToPlcState(ToPlcState newState)
        {
            _toPlcState = newState;
            _toPlcStateStart = _timeProvider.GetTimestamp();
        }

        private void EnterGlobalError()
        {

            ChangeToPlcState(ToPlcState.Error);
            // Tutaj można dodać wysłanie eventu do interfejsu WPF o błędzie
        }

        private void UILoggerStartTask( int incomingTaskId)
        {
            switch (incomingTaskId)
            {
                case 50: _dataStore.AddTaskLog(_plcId, $"Zapisanie parametrów Task[{incomingTaskId}] [{_dataStore.GetCurrentOrder(_plcId).KOLZLEC.ToString()}]");
                    break;
                case 51:
                    _dataStore.AddTaskLog(_plcId, $"Pobranie zlecenia Task[{incomingTaskId}] [{_dataStore.GetData(_plcId).OrderNumberReq.ToString()}]");
                    break;
                default:
                    _dataStore.AddTaskLog(_plcId, $"Nie zdefiniowany Task[{incomingTaskId}] [X]");
                    break;
            }
        }

        private void UILoggerEndTaskTask(int incomingTaskId, bool status)
        {
            string statusText = status ? "SUKCES" : "BŁĄD";

            switch (incomingTaskId)
            {
                case 50:
                    _dataStore.AddTaskLog(_plcId, $"Zapisanie parametrów Task[{incomingTaskId}] [{statusText}]");
                    break;
                case 51:
                    _dataStore.AddTaskLog(_plcId, $"Pobranie zlecenia Task[{incomingTaskId}] [{statusText}]");
                    break;
                default:
                    _dataStore.AddTaskLog(_plcId, $"Nie zdefiniowany Task[{incomingTaskId}] [{statusText}]");
                    break;
            }
        }
    }
}
