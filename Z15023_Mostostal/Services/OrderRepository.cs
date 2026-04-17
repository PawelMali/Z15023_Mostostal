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
    public async Task<IEnumerable<ProductionOrder>> GetPendingOrdersAsync()
    {
        string connectionString = _settings.GetSqlConnectionString();
        string viewName = _settings.CurrentConfig.Database.OrdersViewName;

        using var connection = new SqlConnection(connectionString);

        // Pobieramy wszystkie rekordy z widoku zdefiniowanego w ustawieniach
        string query = $"SELECT * FROM {viewName}";

        return await connection.QueryAsync<ProductionOrder>(query);
    }

    public async Task<ProductionOrder?> GetOrderByNumberAsync(string orderNumber)
    {
        string connectionString = _settings.GetSqlConnectionString();
        string viewName = _settings.CurrentConfig.Database.OrdersViewName;

        using var connection = new SqlConnection(connectionString);

        // Szukamy zlecenia po numerze (dopasuj nazwę kolumny do swojego SQL)
        string query = $"SELECT TOP 1 * FROM {viewName} WHERE KOLZLEC = @OrderNo OR NRZLEC = @OrderNo";

        return await connection.QuerySingleOrDefaultAsync<ProductionOrder>(query, new { OrderNo = orderNumber });
    }
}
