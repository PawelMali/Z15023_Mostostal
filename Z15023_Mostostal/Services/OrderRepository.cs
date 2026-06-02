using Microsoft.Data.SqlClient;
using Dapper;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.Settings;

namespace Z25023_Mostostal.Services;

public class OrderRepository(SettingsManagerService _settings)
{
    public async Task<IEnumerable<ProductionOrder>> GetPendingOrdersAsync(string orderNumber, string orderPositionNumber)
    {
        string connectionString = _settings.GetSqlConnectionString();
        string viewName = _settings.CurrentConfig.Database.OrdersViewName;

        using var connection = new SqlConnection(connectionString);

        // Bazowe zapytanie, które zawsze wymaga numeru zlecenia
        string query = $"SELECT TOP (50) * FROM {viewName} WHERE KOLZLEC = @OrderNo";

        // Budujemy obiekt parametrów dynamicznych Dappera
        var parameters = new DynamicParameters();
        parameters.Add("OrderNo", orderNumber);

        // Jeśli pozycja została przekazana i nie jest pusta, doklejamy czysty warunek
        if (!string.IsNullOrWhiteSpace(orderPositionNumber))
        {
            query += " AND POZ_WYKWYS = @OrderPositionNo";
            parameters.Add("OrderPositionNo", orderPositionNumber);
        }

        // Na koniec dodajemy sortowanie
        query += " ORDER BY POZ_WYKWYS";

        return await connection.QueryAsync<ProductionOrder>(query, parameters);
    }

    public async Task<ProductionOrder?> GetOrderByNumberAsync(string orderNumber, string orderPositionNumber)
    {
        string connectionString = _settings.GetSqlConnectionString();
        string viewName = _settings.CurrentConfig.Database.OrdersViewName;

        using var connection = new SqlConnection(connectionString);

        // Szukamy zlecenia po numerze (dopasuj nazwę kolumny do swojego SQL)
        string query = $"SELECT TOP 1 * FROM {viewName} WHERE KOLZLEC = @OrderNo AND POZ_WYKWYS = @OrderPositionNo ";

        return await connection.QuerySingleOrDefaultAsync<ProductionOrder>(query, new { OrderNo = orderNumber, OrderPositionNo = orderPositionNumber });
    }

    public async Task UpdateProductionCounterAsync(ProductionOrder order, int currentProductionCounter, int plcId)
    {
        string connectionString = _settings.GetSqlConnectionString();

        using var connection = new SqlConnection(connectionString);

        // Wzorzec UPSERT dla SQL Server
        string sql = @"
        IF EXISTS (SELECT 1 FROM [dbo].[PROZAP_POZYCJA] WHERE KOLZLEC = @KOLZLEC AND POZ_WYKWYS = @POZ_WYKWYS AND PLC_ID = @PLC_ID)
        BEGIN
            UPDATE [dbo].[PROZAP_POZYCJA]
            SET SZTUKPOZ = @SZTUKPOZ
            WHERE KOLZLEC = @KOLZLEC AND POZ_WYKWYS = @POZ_WYKWYS AND PLC_ID = @PLC_ID
        END
        ELSE
        BEGIN
            INSERT INTO [dbo].[PROZAP_POZYCJA] 
            (KOLZLEC, CZESC, PRZESYLKA,FAKTOR, SZTUKPOZ, POZ_WYKWYS, NRZLEC, PLC_ID)
            VALUES 
            (@KOLZLEC, @CZESC, @PRZESYLKA, @FAKTOR, @SZTUKPOZ, @POZ_WYKWYS, @NRZLEC, @PLC_ID)
        END";

        // Wykorzystujemy obiekty anonimowe Dappera do przekazania parametrów
        await connection.ExecuteAsync(sql, new
        {
            KOLZLEC = order.KOLZLEC,
            CZESC = order.CZESC, 
            PRZESYLKA = order.PRZESYLKA,
            FAKTOR = order.FAKTOR,
            SZTUKPOZ = currentProductionCounter,
            POZ_WYKWYS = order.POZ_WYKWYS,
            NRZLEC = order.NRZLEC,
            PLC_ID = plcId
        });
    }

    public async Task InsertCompletedShipmentAsync(ProductionOrder order, int plcId)
    {
        string connectionString = _settings.GetSqlConnectionString();

        using var connection = new SqlConnection(connectionString);

        string sql = @"
            INSERT INTO [dbo].[PROZAP_PRZESYLKA] 
            (KOLZLEC, CZESC, PRZESYLKA, FAKTOR, NRZLEC, PLC_ID)
            VALUES 
            (@KOLZLEC, @CZESC, @PRZESYLKA, @FAKTOR, @NRZLEC, @PLC_ID)";

        // Dapper automatycznie zmapuje właściwości obiektu anonimowego na parametry SQL
        await connection.ExecuteAsync(sql, new
        {
            KOLZLEC = order.KOLZLEC,
            CZESC = order.CZESC,
            PRZESYLKA = order.PRZESYLKA,
            FAKTOR = 1,
            NRZLEC = order.NRZLEC,
            PLC_ID = plcId
        });
    }
}
