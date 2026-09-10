using System.Text.Json;
using System.Threading;
using Microsoft.Data.Sqlite;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using System.Net;
using System.Text;

namespace AvitoMonitoring;

internal class GetWebpage(GetUserInfo userInfo)
{
    HttpClient httpCli = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip
                                   | DecompressionMethods.Deflate
                                   | DecompressionMethods.Brotli
    });
    Random random = new();
    Semaphore lockConnections = new(name: "Global\\AvitoMonitoringSynchronization", initialCount: 1, maximumCount: 20);

    internal async Task<string?> GetContents()
    {
        string urlGet = userInfo.linkCheck;

        Console.WriteLine("Ожидаю освобождения семафора...");
        lockConnections.WaitOne();
        {
            Console.WriteLine("Семафор захвачен. Получаю информацию от Авито...");
            try
            {
                var webpage = await httpCli.GetAsync(urlGet);
                System.Console.WriteLine("получаю сайт");

                int statusCode = (int)webpage.StatusCode;
                if (statusCode.ToString()[0] != '2')
                {
                    string message = string.Empty;
                    switch (statusCode)
                    {
                        case 403:
                            message = $"Сайт Авито отклонил подключение (пришла капча или ваш IP-адрес). Мониторинг остановлен на 3 минуты.\nКод ошибки HTTP-запроса: {statusCode}";
                            await SendWarning(message);
                            await Task.Delay(TimeSpan.FromMinutes(3));
                            lockConnections.Release();
                            return string.Empty;
                        case 429:
                            message = $"Сайт Авито отклонил подключение из-за слишком частой отправки запросов. Мониторинг остановлен на 1 минуту.\nКод ошибки HTTP-запроса: {statusCode}";
                            await SendWarning(message);
                            await Task.Delay(TimeSpan.FromMinutes(1));
                            lockConnections.Release();
                            return string.Empty;
                        case 500 or 501 or 502 or 503 or 504:
                            message = $"Сайт Авито не установил подключение, т.к. произошла ошибка со стороны сервера. Мониторинг остановлен на 1 минуту.\nКод ошибки HTTP-запроса: {statusCode}";
                            await SendWarning(message);
                            await Task.Delay(TimeSpan.FromMinutes(1));
                            lockConnections.Release();
                            return string.Empty;
                        default:
                            message = $"Произошла иная HTTP-ошибка, не обработанная программой. Мониторинг остановлен на 2 минуты.\nКод ошибки HTTP-запроса: {statusCode}";
                            await SendWarning(message);
                            await Task.Delay(TimeSpan.FromMinutes(2));
                            lockConnections.Release();
                            return string.Empty;
                    }
                }

                byte[] temp = await webpage.Content.ReadAsByteArrayAsync();
                string webpageContent = Encoding.UTF8.GetString(temp);

                if (string.IsNullOrEmpty(webpageContent))
                {
                    Console.WriteLine("Полученная веб-страница не содержит какой-либо информации.");
                    lockConnections.Release();
                    return string.Empty;
                }
                if (!webpageContent.Contains("\"items\":["))
                {
                    string logFilename = $"{DateTime.Now.ToString("dd-MM-yyyy")}_{DateTime.Now.ToString("HH-mm-ss")}avito-response.html";
                    Console.WriteLine($"Полученная веб-страница не содержит информации об объявлениях. Возможно, сайт Авито вернул капчу или IP-бан в необычном виде. Полученная веб-страница сохранена в файле {logFilename} в одной директории с программой. Работа мониторинга приостановлена на 3 минуты.");

                    using (var writeHtml = new StreamWriter(logFilename))
                    {
                        await writeHtml.WriteAsync(webpageContent);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(3));
                    lockConnections.Release();
                    return string.Empty;
                }

                return webpageContent;
            }
            catch (TaskCanceledException timeout)
            {
                string message = $"Не удалось установить подключение к сайту Авито спустя 15 секунд. Отправка запросов остановлена на 1 минуту. Опрашиваемый адрес:\n{urlGet}\nСообщение:\n{timeout.Message}";
                await SendWarning(message);
                await Task.Delay(TimeSpan.FromMinutes(2));
                lockConnections.Release();
            }
            catch (HttpRequestException reqex)
            {
                string message = $"Не удалось установить подключение к сайту Авито. Отправка запросов остановлена на 1 минуту. Опрашиваемый адрес:\n{urlGet}\nСообщение:\n{reqex.Message}";
                await SendWarning(message);
                await Task.Delay(TimeSpan.FromMinutes(2));
                lockConnections.Release();
            }
        }

        return string.Empty;
    }

    internal async Task FetchWebpage(string sWebpage)
    {
        using var webpage = JsonDocument.Parse(GetJsonArray(sWebpage, "\"items\":["));
        var root = webpage.RootElement.EnumerateArray();

        foreach (var element in root)
        {
            var itemInfo = element.Deserialize<AvitoInfo>();
            using (var connection = new SqliteConnection(SetupSqlite.Path))
            {
                await connection.OpenAsync();

                var checkIfSentAlready = new SqliteCommand("SELECT 1 FROM items WHERE id = $id", connection);
                checkIfSentAlready.Parameters.AddWithValue("$id", itemInfo!.ItemId);
                var infoId = await checkIfSentAlready.ExecuteReaderAsync();

                if (infoId.HasRows) continue;
            }
            if (itemInfo.ItemPriceInformation!.ItemPrice > userInfo.priceLimit) continue;
            long timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            byte minutesPassed = (byte)((timeNow - (itemInfo.ItemTimeStamp / 1000)) / 60);
            if (timeNow - (itemInfo.ItemTimeStamp / 1000) > 3600) continue;

            System.Console.WriteLine($"{itemInfo!.ItemId} прошёл все проверки");

            /*var imagesArray = element.GetProperty("images").EnumerateArray();
            foreach (var link in imagesArray)
            {
                if (link.TryGetProperty("636x636", out var linkInfo))
                {
                    itemInfo!.ItemImageLink = linkInfo.GetString();
                }
            }*/

            string message = $"""
            <i>Обнаружено новое объявление!</i>

            <a href="avito.ru{itemInfo.ItemUrlPath}"><b>{itemInfo.ItemTitle}</b></a>
            Цена: {itemInfo.ItemPriceInformation.ItemPrice}₽
            Объявление было выложено <b>{minutesPassed} минут назад.</b>
            """;

            // System.Console.WriteLine(message);

            try
            {
                await userInfo.telegramBotInstance!.SendMessage(
                    chatId: userInfo.telegramUserId,
                    text: message,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                    linkPreviewOptions: LinkPreviewOptions.Disabled
                );

                using var connection = new SqliteConnection(SetupSqlite.Path);
                await connection.OpenAsync();
                var recordSentItem = new SqliteCommand("INSERT INTO items (id) VALUES ($id)", connection);
                recordSentItem.Parameters.AddWithValue("$id", itemInfo.ItemId);
                await recordSentItem.ExecuteNonQueryAsync();
            }
            catch (RequestException ex)
            {
                Console.WriteLine($"Не удалось отправить сообщение в Telegram, т.к. не было установлено соединение.\nТекст исключения: {ex.Message}\nОтправляю отчёт сюда:\n\n{message}\n");
            }
        }

        int waitSeconds = random.Next(16, 19);
        await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
        lockConnections.Release();
        await Task.Delay(TimeSpan.FromSeconds(userInfo.delaySeconds - waitSeconds));
        return;
    }

    internal void SetHttpClient()
    {
        // это было взято с клода вроде. потому что я не знаю, как выставлять хедеры нормально, кроме User-Agent
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                         "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif," +
            "image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding",
            "gzip, deflate, br");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
            "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua",
            "\"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Mobile",
            "?0");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Platform",
            "\"Windows\"");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest",
            "document");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode",
            "navigate");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site",
            "none");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User",
            "?1");
        httpCli.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests",
            "1");
        httpCli.Timeout = TimeSpan.FromSeconds(15);
    }

    async Task SendWarning(string message)
    {
        Console.WriteLine(message);

        try
        {
            await userInfo.telegramBotInstance!.SendMessage(
                chatId: userInfo.telegramUserId,
                text: message
            );
        }
        catch (RequestException)
        {
            Console.WriteLine("Не удалось отправить сообщение об ошибке в Telegram.");
        }
    }

    string GetJsonArray(string jsonFile, string startString)
    {
        int startingIndex = jsonFile.IndexOf(startString, StringComparison.Ordinal) + startString.Length;

        int level = 1;
        bool inText = false;

        for (int i = startingIndex; i < jsonFile.Length; i++)
        {
            char character = jsonFile[i];

            if (inText)
            {
                if (character == '"') inText = false;
                if (character == '\\') i++;
            }
            else
            {
                switch (character)
                {
                    case '"': inText = true; break;
                    case '[': level++; break;
                    case ']': level--; break;
                    default: break;
                }
            }

            if (level < 1) { return jsonFile.Substring(startingIndex - 1, i - startingIndex + 2); }
        }

        return string.Empty;
    }
}
