using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace AvitoMonitoring;

internal class GetUserInfo
{
    internal TelegramBotClient? telegramBotInstance;
    internal long telegramUserId = 0;
    internal List<string>? linkCheck { get; set; } = new();
    internal int linkChoice
    {
        get; set
        {
            if (value >= linkCheck!.Count)
            {
                field = 0;
            }
            else field = value;
        }
    } = 0;
    internal List<int> priceLimit = new();

    internal static async Task<GetUserInfo> CreateProgramInstance()
    {
        var userInfoClass = new GetUserInfo();

        userInfoClass.telegramBotInstance = await userInfoClass.GetBotToken();

        userInfoClass.ImportPriceLimits();
        userInfoClass.ImportCategoryLinks();
        if (userInfoClass.linkCheck!.Count != userInfoClass.priceLimit!.Count)
        {
            userInfoClass.telegramUserId = 0;
            Console.Clear();
            Console.WriteLine("Количество ссылок и лимитов из userinfo.json не совпадает. Убедитесь, что в конфигурационном файле нет ошибок, и перезапустите программу.\n"
            + $"Количество рабочих ссылок: {userInfoClass.linkCheck.Count}; Количество ценовых лимитов: {userInfoClass.priceLimit.Count}.");
        }
        else
        {
            await userInfoClass.GetCategoryLink();
            userInfoClass.telegramUserId = await userInfoClass.GetUserId();
        }

        return userInfoClass;
    }

    void ImportCategoryLinks()
    {
        try
        {
            using (JsonDocument userinfo = JsonDocument.Parse(File.ReadAllText("userinfo.json")))
            {
                var root = userinfo.RootElement[0];

                var links = root.GetProperty("link-check").EnumerateArray();
                foreach (var link in links)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(link.GetString())) continue;
                        CheckAddLink(link.GetString()!);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            Console.WriteLine($"Импортировано {linkCheck!.Count} ссылок из userinfo.json.");
        }
        catch { }
    }

    async Task GetCategoryLink()
    {
        Console.WriteLine("""
        Небольшой, но важный совет, чтобы правильно вставить ссылку.
        Зайдите на сайт Авито в браузере на ПК или телефоне, впишите название вещи в поиск и не нажимая Enter (ввод), выберите товар в категории.
        В выпадающем списке это будет выглядеть примерно также, как и то, что вы списали + снизу обязательно должна быть полупрозрачная надпись, обозначающая категорию на телефоне или надпись справа от названия товара на ПК. Как это примерно должно выглядеть:

        [ПК]
        ryzen 5 5600 <- Процессоры AMD

        [Телефон]
        ryzen 5 5600
        Процессоры AMD

        Нажмите на нужный пункт. Дальше вам нужно будет отсортировать товары по дате (⬇⬆ Сортировка -> По дате), ведь иначе в работе программы не остаётся много смысла.
        Теперь ссылку с данной страницы можно скопировать (в случае, если ссылка выглядит слишком длинной, это нормально т.к. была поставлена сортировка по дате) и вставить в программу.

        Данный текст показывается только один раз, но если вам нужна более детальная информация по работе с программой, перейдите в соответствующий репозиторий по ссылке https://github.com/DaimonTea/AvitoMonitoring/. 
        """);

        string inputLink = string.Empty;
        if (linkCheck!.Count == 0)
        {
            while (true)
            {
                Console.Write("Пожалуйста, вставьте ссылку для отслеживания: ");
                inputLink = Console.ReadLine() ?? string.Empty;
                if (string.IsNullOrEmpty(inputLink)) { Console.Clear(); Console.WriteLine("Ссылка не может быть пустой."); continue; }
                if (!CheckAddLink(inputLink)) continue;
                priceLimit!.Add(GetPriceLimit());
                break;
            }
        }

        while (true)
        {
            Console.Clear();
            Console.Write($"Сохранено {linkCheck.Count} ссылок.\nНажмите Enter для перехода к вводу токена, или введите следующую ссылку для отслеживания: ");
            inputLink = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrEmpty(inputLink)) { Console.Clear(); return; }

            if (!CheckAddLink(inputLink)) continue;
            priceLimit!.Add(GetPriceLimit());
        }
    }

    int GetPriceLimit()
    {
        while (true)
        {
            Console.Write("Укажите цену в рублях, ниже которой вам нужны объявления: ");
            string sLimit = Console.ReadLine() ?? string.Empty;
            if (int.TryParse(sLimit, out int limit) || limit > 0)
            {
                return limit;
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Указано неправильное значение.");
            }
        }
    }

    void ImportPriceLimits()
    {
        try
        {
            using (JsonDocument userinfo = JsonDocument.Parse(File.ReadAllText("userinfo.json")))
            {
                var root = userinfo.RootElement[0];

                var prices = root.GetProperty("price-limit").EnumerateArray();
                foreach (var price in prices)
                {
                    try
                    {
                        priceLimit!.Add(price.GetInt32());
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine(ex.Message);
                        continue;
                    }
                }
            }
        }
        catch { }
    }

    async Task<TelegramBotClient> GetBotToken()
    {
        string botToken = string.Empty;

        try
        {
            using (JsonDocument userInfo = JsonDocument.Parse(File.ReadAllText("userinfo.json")))
            {
                if (string.IsNullOrEmpty(userInfo.RootElement[0].GetProperty("bot-token").GetString())) throw new DirectoryNotFoundException();
                else botToken = userInfo.RootElement[0].GetProperty("bot-token").GetString()!;

                string errorMessage = await CheckBotToken();
                if (errorMessage == "1") return new TelegramBotClient(botToken);
                else Console.WriteLine(errorMessage);
            }
        }
        catch (Exception) { Console.WriteLine("Токен телеграм-бота и ID для отправки уведомлений можно заполнять автоматически при каждом новом запуске программы, если создать файл userinfo.json по шаблону (на сайте репозитория https://github.com/DaimonTea/AvitoMonitoring/) в одной папке с .exe/.dll файлом программы (ID аккаунта можно получить в t.me/userinfobot)\n"); }

        while (true)
        {
            Console.Write("\nВпишите токен вашего телеграм-бота (сохраняется в программе только до конца работы и никуда не отправляется): ");
            botToken = Console.ReadLine() ?? string.Empty;
            var botClient = new TelegramBotClient(botToken);

            string errorMessage = await CheckBotToken();
            if (errorMessage != "1") { Console.Clear(); Console.WriteLine(errorMessage); continue; }
            else { Console.Clear(); Console.WriteLine("Токен сработал."); return botClient; }
        }

        async Task<string> CheckBotToken()
        {
            string message = "1";
            var checkBot = new TelegramBotClient(botToken);
            try { Console.Write("\nПроверка..."); await checkBot.GetMe(); }
            catch (ArgumentException) { message = "Неверный токен телеграм-бота."; }
            catch (RequestException) { message = "Не удалось подключиться к телеграму. Проверьте ваше подключение и/или работоспособность вашего VPN."; }

            return message;
        }
    }

    async Task<long> GetUserId()
    {
        try
        {
            using (JsonDocument userInfo = JsonDocument.Parse(File.ReadAllText("userinfo.json")))
            {
                if (userInfo.RootElement[0].GetProperty("user-id").GetInt64() != 0)
                {
                    if (await CheckUserId(userInfo.RootElement[0].GetProperty("user-id").GetInt64().ToString())) return userInfo.RootElement[0].GetProperty("user-id").GetInt64();
                    else Console.WriteLine("ID пользователя из userinfo.json не сработал.");
                }
            }
        }
        catch (Exception) { Console.WriteLine("Токен телеграм-бота и ID для отправки уведомлений можно заполнять автоматически при каждом новом запуске программы, если создать файл userinfo.json по шаблону (на сайте репозитория https://github.com/DaimonTea/AvitoMonitoring/) в одной папке с .exe/.dll файлом программы (ID аккаунта можно получить в t.me/userinfobot)\n"); }

        while (true)
        {
            Console.Write("Введите ваш ID в телеграме (ID аккаунта можно получить в t.me/userinfobot): ");
            string sUserId = Console.ReadLine() ?? string.Empty;

            if (await CheckUserId(sUserId)) return long.Parse(sUserId);
        }

        async Task<bool> CheckUserId(string sUserId)
        {
            if (long.TryParse(sUserId, out long userId))
            {
                try
                {
                    await telegramBotInstance!.SendMessage(
                        chatId: userId,
                        text: "Проверка. Если вы указали ваш ID в программе и получили это сообщение, то можно продолжать работу."
                    );

                    Console.Write("Сообщение для проверки успешно отправлено. Если вы получили уведомление от вашего бота, нажмите Y. Если вы указали неправильный ID, нажмите N... ");
                    var idChoice = Console.ReadKey(intercept: true);

                    Console.Clear();
                    if (idChoice.Key == ConsoleKey.Y) return true;

                    return false;
                }
                catch (ApiRequestException)
                {
                    Console.Clear();
                    Console.WriteLine("Неправильно указан ID пользователя.");
                }
                catch (RequestException)
                {
                    Console.Clear();
                    Console.WriteLine("Не удалось подключиться к телеграму. Проверьте ваше подключение и/или работоспособность вашего VPN.");
                }

                return false;
            }
            else
            {
                Console.WriteLine("Указанное число не является рабочим ID в телеграм.");
                return false;
            }
        }
    }

    bool CheckAddLink(string inputLink)
    {
        if (!inputLink.StartsWith("https://")) inputLink = "https://" + inputLink;
        if (!inputLink.Contains("avito.ru/")) { Console.Clear(); Console.WriteLine("Вы должны вставить ссылку на страницу Авито."); return false; }
        linkCheck!.Add(inputLink);
        return true;
    }
}