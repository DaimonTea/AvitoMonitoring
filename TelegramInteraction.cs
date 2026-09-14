using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;

namespace AvitoMonitoring;

internal class TelegramCommands(GetUserInfo userInfo)
{
    public async Task DecideMessage(Message msg, UpdateType update)
    {
        if (msg.Chat.Id != userInfo.telegramUserId) return;
        if (msg.Text!.Equals("/spentinfo"))
        {
            await SendSpentInfo(msg);
        }
    }

    private async Task SendSpentInfo(Message msg)
    {
        using var connection = new SqliteConnection(SetupSqlite.Path);
        await connection.OpenAsync();

        long instanceTrafficInfo = 0;
        long generalTrafficInfo = 0;

        using var getInstanceTrafficInfo = new SqliteCommand("SELECT (kbCount) FROM DataCounter WHERE id = $InstanceId;", connection);
        getInstanceTrafficInfo.Parameters.AddWithValue("$InstanceId", SetupSqlite.InstanceId);
        using var instanceInfo = await getInstanceTrafficInfo.ExecuteReaderAsync();

        if (await instanceInfo.ReadAsync()) instanceTrafficInfo = instanceInfo.GetInt64(instanceInfo.GetOrdinal("kbCount"));

        using var getGeneralTrafficInfo = new SqliteCommand("SELECT SUM(kbCount) AS summaryUsage FROM DataCounter;", connection);
        using var generalInfo = await getGeneralTrafficInfo.ExecuteReaderAsync();

        if (await generalInfo.ReadAsync()) generalTrafficInfo = generalInfo.GetInt64(generalInfo.GetOrdinal("summaryUsage"));

        string message = $"""
        Потребление интернета программой:

        С последнего запуска - <b>{ConvertIntoRightSize(instanceTrafficInfo)}</b>
        За всё время - <b>{ConvertIntoRightSize(generalTrafficInfo)}</b>
        """;

        try
        {
            await userInfo.telegramBotInstance!.SendMessage(
                chatId: msg.Chat.Id,
                text: message,
                parseMode: ParseMode.Html
            );
        }
        catch (RequestException ex)
        {
            Console.WriteLine($"Не удалось отправить сообщение в Telegram, т.к. не было установлено соединение.\nТекст исключения: {ex.Message}\nОтправляю потребление интернета:\n\n{message}\n");
        }

        string ConvertIntoRightSize(long kilobytes)
        {
            if (kilobytes / (1024 * 1024) >= 1) return $"{(double)kilobytes / (1024 * 1024):F2} ГБ";
            else if (kilobytes / 1024 >= 1) return $"{(double)kilobytes / 1024:F2} МБ";
            else return $"{kilobytes} КБ";
        }
    }
}