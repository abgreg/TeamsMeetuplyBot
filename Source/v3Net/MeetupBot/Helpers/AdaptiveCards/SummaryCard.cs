namespace MeetupBot.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    public static class SummaryCard
    {
        public static string GetCard(string responsesCount, string happytacos, string sadtacos)
        {
            var variablesToValues = new Dictionary<string, string>()
            {
                { "responseCount", responsesCount },
                { "happytacos", happytacos },
                { "sadtacos", sadtacos}
            };

            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/SummaryCard.json");
            var cardTemplate = File.ReadAllText(cardJsonFilePath);

            var cardBody = cardTemplate;

            foreach (var kvp in variablesToValues)
            {
                cardBody = cardBody.Replace($"%{kvp.Key}%", kvp.Value);
            }

            return cardBody;
        }
    }
}