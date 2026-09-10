using System.Runtime.InteropServices;

namespace AvitoMonitoring;

class Program
{
    static async Task Main(string[] args)
    {
        var userInfo = await GetUserInfo.CreateProgramInstance();

        var getWebpage = new GetWebpage(userInfo);
        getWebpage.SetHttpClient();
        await SetupSqlite.SetDatabase();

        while (true)
        {
            await Task.Delay(1000);
            string? sWebpage = await getWebpage.GetContents();
            if (string.IsNullOrEmpty(sWebpage)) { System.Console.WriteLine("страницы типо нет"); continue; }
            else await getWebpage.FetchWebpage(sWebpage);
        }
    }
}