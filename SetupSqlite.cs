using Microsoft.Data.Sqlite;

namespace AvitoMonitoring;

public static class SetupSqlite
{
    public const string Path = "Data Source=AvitoMonitoring.db";
    public static int InstanceId { get; private set; }

    public static async Task SetDatabase()
    {
        using var connection = new SqliteConnection(Path);
        await connection.OpenAsync();

        using var setDatabase = new SqliteCommand("CREATE TABLE IF NOT EXISTS Items(id INTEGER NOT NULL PRIMARY KEY);", connection);
        int addedTable = await setDatabase.ExecuteNonQueryAsync();
        if (addedTable == 1)
        {
            Console.WriteLine("Таблица идентефикаторов объявлений успешно создана.");
        }
        else
        {
            Console.WriteLine("Таблица идентефикаторов уже была создана.");
        }

        using var setDataCounter = new SqliteCommand("CREATE TABLE IF NOT EXISTS DataCounter(id INTEGER NOT NULL PRIMARY KEY, kbCount INTEGER DEFAULT 0);", connection);
        int createdDataCounter = await setDataCounter.ExecuteNonQueryAsync();
        if (createdDataCounter == 1)
        {
            Console.WriteLine("Таблица подсчёта трафика создана.");
        }
        else
        {
            Console.WriteLine("Таблица подсчёта трафика уже была создана ранее.");
        }

        int tryInstanceId = 1;
        while (true)
        {
            using var createCounter = new SqliteCommand("INSERT OR IGNORE INTO DataCounter (id, kbCount) VALUES ($id, 0);", connection);
            createCounter.Parameters.AddWithValue("$id", tryInstanceId);
            int createdCounter = await createCounter.ExecuteNonQueryAsync();

            if (createdCounter == 1)
            {
                InstanceId = tryInstanceId;
                return;
            }
            tryInstanceId++;
        }
    }
}