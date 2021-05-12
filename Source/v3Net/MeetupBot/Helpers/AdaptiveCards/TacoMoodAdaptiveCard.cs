namespace MeetupBot.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    public static class TacoMoodAdaptiveCard
    {
        public static string GetCard(string receiverName, string teamId)
        {
            var variablesToValues = new Dictionary<string, string>()
            {
                { "receiverName", receiverName },
                { "teamId", teamId }
            };

            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/TacoMoodAdaptiveCard.json");
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