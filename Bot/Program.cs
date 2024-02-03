using Bot.Telegram;
using OpenSeaClient;
using RestSharp;

namespace Bot // Note: actual namespace depends on the project name.
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            var tokenSource = new CancellationTokenSource();
            var midJourney = new MidJourney();
            var telegramService = new TelegramService(tokenSource, midJourney);
            await telegramService.StartReceiving();
        }
    }
}