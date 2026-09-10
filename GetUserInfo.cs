using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace AvitoMonitoring;

internal class GetUserInfo
{
    internal TelegramBotClient? telegramBotInstance;
    internal long telegramUserId;
    internal string linkCheck = string.Empty;
    internal int priceLimit;
    internal int delaySeconds;

    internal static async Task<GetUserInfo> CreateProgramInstance()
    {
        var userInfoClass = new GetUserInfo();

        userInfoClass.linkCheck = userInfoClass.GetCategoryLink();
        userInfoClass.telegramBotInstance = await userInfoClass.GetBotToken();
        userInfoClass.telegramUserId = await userInfoClass.GetUserId();
        userInfoClass.delaySeconds = userInfoClass.GetDelay();
        userInfoClass.priceLimit = userInfoClass.GetPriceLimit();

        return userInfoClass;
    }

    string GetCategoryLink()
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

        while (true)
        {
            Console.Write("Пожалуйста, вставьте ссылку для отслеживания: ");

            string inputLink = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrEmpty(inputLink)) { Console.Clear(); Console.WriteLine("Ссылка не может быть пустой."); continue; }
            if (!inputLink.StartsWith("https://")) inputLink = "https://" + inputLink;

            if (!inputLink.Contains("avito.ru/")) { Console.Clear(); Console.WriteLine("Вы должны вставить ссылку на страницу Авито."); continue; }

            Console.Clear();
            Console.WriteLine("Ссылка определена.");
            return inputLink;
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

    int GetDelay()
    {
        Console.Clear();
        Console.WriteLine("""
        Совет по назначению задержки.

        Для выставления задержки следует придерживаться двух правил:
        задержка должна быть не меньше 20-25 секунд (в зависимости от скорости интернета), если запущен только 1 инстанс данной программы,
        а при запуске нескольких процессов задержка должна быть не меньше ((от 20 до 25 в зависимости от интернета) * количество запущенных экземпляров программы) секунд.
        Эти правила соблюдаются для того, чтобы не получить IP-бан/капчу от Авито, и чтобы в программе(-ах) не накапливалась очередь из запросов.

        Данный текст показывается только один раз, но если вам нужна более детальная информация по работе с программой, перейдите в соответствующий репозиторий по ссылке https://github.com/DaimonTea/AvitoMonitoring/.
        """);

        while (true)
        {
            Console.Write("Введите задержку между запросами в секундах: ");
            string sDelay = Console.ReadLine() ?? string.Empty;

            int delay = 0;
            if (int.TryParse(sDelay, out delay))
            {
                if (delay < 20)
                {
                    Console.Clear();
                    Console.WriteLine("Ставить задержку меньше 20 секунд категорически не рекомендуется.");
                    continue;
                }
                else
                {
                    return delay;
                }
            }
            else { Console.Clear(); Console.WriteLine("Неправильно указана задержка."); }
        }
    }
}