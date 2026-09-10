using Microsoft.Data.Sqlite;

namespace AvitoMonitoring;

public static class SetupSqlite
{
    public const string Path = "Data Source=AvitoMonitoring.db";

    public static async Task SetDatabase()
    {
        using var connection = new SqliteConnection(Path);
        await connection.OpenAsync();

        using var setDatabase = new SqliteCommand("CREATE TABLE IF NOT EXISTS items(id INTEGER NOT NULL PRIMARY KEY);", connection);
        int addedTable = await setDatabase.ExecuteNonQueryAsync();
        if (addedTable == 1)
        {
            Console.WriteLine("Таблица идентефикаторов объявлений успешно создана.");
        }
        else
        {
            Console.WriteLine("Таблица идентефикаторов уже была создана.");
        }
    }
}