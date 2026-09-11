using System.Runtime.InteropServices;

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

        while (true)
        {
            string? sWebpage = await getWebpage.GetContents();
            if (string.IsNullOrEmpty(sWebpage)) { System.Console.WriteLine("Получена пустая страница."); continue; }
            else await getWebpage.FetchWebpage(sWebpage);
            userInfo.linkChoice++;
        }
    }
}