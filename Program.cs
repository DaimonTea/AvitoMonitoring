using System.Runtime.InteropServices;
using Telegram.Bot.Types;

namespace AvitoMonitoring;

class Program
{
    static async Task Main(string[] args)
    {
        var userInfo = await GetUserInfo.CreateProgramInstance();
        if (userInfo.telegramUserId == 0)
        {
            Console.ReadKey();
            return;
        }

        var getWebpage = new GetWebpage(userInfo);
        getWebpage.SetHttpClient();
        await SetupSqlite.SetDatabase();

        var commands = new TelegramCommands(userInfo);
        userInfo.telegramBotInstance!.OnMessage += commands.DecideMessage;

        while (true)
        {
            string? sWebpage = await getWebpage.GetContents();
            if (string.IsNullOrEmpty(sWebpage)) { System.Console.WriteLine("Получена пустая страница."); continue; }
            else await getWebpage.FetchWebpage(sWebpage);
            userInfo.linkChoice++;
        }
    }
}