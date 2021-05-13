using System.Net;

namespace TriggerEDUChatRouletteBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var webRequest = WebRequest.Create($"https://demo-checkin-bot.azurewebsites.net/api/processnow/A5C9B42B-375F-4C19-9B12-C4DFCE265813");
            webRequest.GetResponse();
        }
    }
}
