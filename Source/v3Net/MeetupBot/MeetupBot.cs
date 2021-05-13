﻿namespace MeetupBot
{
    using Helpers;
    using Helpers.AdaptiveCards;
    using Microsoft.Azure;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public static class MeetupBot
    {
        public static async Task<int> Notify()
        {
            // Recall all the teams where we have been added
            // For each team where I have been added:
            //     Pull the roster of each team where I have been added
            //     Remove the members who have opted out of pairs
            //     Match each member with someone else
            //     Save this pair
            // Now notify each pair found in 1:1 and ask them to reach out to the other person
            // When contacting the user in 1:1, give them the button to opt-out.

            var teams = MeetupBotDataProvider.GetInstalledTeams();

            var countPairsNotified = 0;
            var maxPairUpsPerTeam = Convert.ToInt32(CloudConfigurationManager.GetSetting("MaxPairUpsPerTeam"));

            foreach (var team in teams)
            {
                try
                {
                    var members = await GetTeamMembers(team.ServiceUrl, team.TeamId, team.TenantId);
                    foreach (var member in members)
                    {
                        await NotifyPerson(team.ServiceUrl, team.TenantId, member);
                    }
                }
                catch (UnauthorizedAccessException uae)
                {
                    System.Diagnostics.Trace.TraceError($"Failed to process a team: {team.ToString()} due to error {uae.ToString()}");
                }
            }

            System.Diagnostics.Trace.TraceInformation($"{countPairsNotified} pairs notified");

            return countPairsNotified;
        }

        private static async Task<string> GetTeamNameAsync(string serviceUrl, string teamId)
        {
            using (var client = new ConnectorClient(new Uri(serviceUrl)))
            {
                var teamsConnectorClient = client.GetTeamsConnectorClient();
                var teamDetailsResult = await teamsConnectorClient.Teams.FetchTeamDetailsAsync(teamId);
                return teamDetailsResult.Name;
            }
        }

        private static async Task NotifyPerson(string serviceUrl, string tenantId, ChannelAccount user)
        {
            var person = user.AsTeamsChannelAccount();

            var card = TacoMoodAdaptiveCard.GetCard(person.GivenName);

            await NotifyUser(serviceUrl, card, person, tenantId);
        }

        private static async Task NotifyPair(string serviceUrl, string tenantId, string teamName, Tuple<ChannelAccount, ChannelAccount> pair)
        {
            var teamsPerson1 = pair.Item1.AsTeamsChannelAccount();
            var teamsPerson2 = pair.Item2.AsTeamsChannelAccount();

            // Fill in person1's info in the card for person2
            var cardForPerson2 = PairUpNotificationAdaptiveCard.GetCard(teamName, teamsPerson1.Name, teamsPerson1.GivenName, teamsPerson2.GivenName, teamsPerson2.UserPrincipalName);

            // Fill in person2's info in the card for person1
            var cardForPerson1 = PairUpNotificationAdaptiveCard.GetCard(teamName, teamsPerson2.Name, teamsPerson2.GivenName, teamsPerson1.GivenName, teamsPerson1.UserPrincipalName);

            await NotifyUser(serviceUrl, cardForPerson1, teamsPerson1, tenantId);
            await NotifyUser(serviceUrl, cardForPerson2, teamsPerson2, tenantId);
        }

        public static async Task WelcomeUser(string serviceUrl, string memberAddedId, string tenantId, string teamId)
        {
            var teamName = await GetTeamNameAsync(serviceUrl, teamId);
            
            var allMembers = await GetTeamMembers(serviceUrl, teamId, tenantId);

            TeamsChannelAccount userThatJustJoined = null;

            foreach (var m in allMembers)
            {
                // both values are 29: values
                if (m.Id == memberAddedId)
                {
                    userThatJustJoined = m;
                }
            }

            var welcomeMessageCard = WelcomeNewMemberCard.GetCard(teamName, userThatJustJoined.Name);

            if (userThatJustJoined != null)
            {
                await NotifyUser(serviceUrl, welcomeMessageCard, userThatJustJoined, tenantId);
            }
            
        }

        private static async Task NotifyUser(string serviceUrl, string cardToSend, ChannelAccount user, string tenantId)
        {
            var me = new ChannelAccount()
            {
                Id = CloudConfigurationManager.GetSetting("MicrosoftAppId"),
                Name = "MeetupBot"
            };

            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

            // Create 1:1 with user
            using (var connectorClient = new ConnectorClient(new Uri(serviceUrl)))
            {
                // ensure conversation exists
                var response = connectorClient.Conversations.CreateOrGetDirectConversation(me, user, tenantId);

                // construct the activity we want to post
                var activity = new Activity()
                {
                    Type = ActivityTypes.Message,
                    Conversation = new ConversationAccount()
                    {
                        Id = response.Id,
                    },
                    Attachments = new List<Attachment>()
                    {
                        new Attachment()
                        {
                            ContentType = "application/vnd.microsoft.card.adaptive",
                            Content = JsonConvert.DeserializeObject(cardToSend),
                        }
                    }
                };

                var isTesting = Boolean.Parse(CloudConfigurationManager.GetSetting("Testing"));

                if (! isTesting)
                {
                    // shoot the activity over
                    await connectorClient.Conversations.SendToConversationAsync(activity, response.Id);
                }
                
            }
        }

        public static async Task NotifyChannel(string serviceUrl, string cardToSend, string channelId)
        {
            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

            using (var connectorClient = new ConnectorClient(new Uri(serviceUrl)))
            {
                // construct the activity we want to post
                var activity = new Activity()
                {
                    Type = ActivityTypes.Message,
                    Attachments = new List<Attachment>()
                    {
                        new Attachment()
                        {
                            ContentType = "application/vnd.microsoft.card.adaptive",
                            Content = JsonConvert.DeserializeObject(cardToSend),
                        }
                    }
                };

                var conversationParameters = new ConversationParameters
                {
                    IsGroup = true,
                    ChannelData = new TeamsChannelData
                    {
                        Channel = new ChannelInfo(channelId)
                    },
                    Activity = activity
                };

                // ensure conversation exists
                await connectorClient.Conversations.CreateConversationAsync(conversationParameters);
            }
        }

        public static async Task SendTeamSummary(string teamId, string channelId)
		{
            var team = MeetupBotDataProvider.GetTeamInstallStatus(teamId);
            var members = await GetTeamMembers(team.ServiceUrl, team.TeamId, team.TenantId);
            var userIds = new List<TeamsChannelAccount>(members).Select(x => x.ObjectId).ToList();

            var dailymoods = MeetupBotDataProvider.GetTodaysTacoMoodsForUsers(team.TenantId, userIds);
            var responseCount = dailymoods.Count;
            var happytacos = dailymoods.Where(x => x.Mood == "happy").Count();
            var sadtacos = dailymoods.Where(x => x.Mood == "sad").Count();

            var card = SummaryCard.GetCard(responseCount.ToString(), happytacos.ToString(), sadtacos.ToString());
            await NotifyChannel(team.ServiceUrl, card, channelId);
        }

        public static async Task SaveAddedToTeam(string serviceUrl, string teamId, string tenantId)
        {
            await MeetupBotDataProvider.SaveTeamInstallStatus(new TeamInstallInfo() { ServiceUrl = serviceUrl, TeamId = teamId, TenantId = tenantId }, true);
        }

        public static async Task SaveRemoveFromTeam(string serviceUrl, string teamId, string tenantId)
        {
            await MeetupBotDataProvider.SaveTeamInstallStatus(new TeamInstallInfo() { ServiceUrl = serviceUrl, TeamId = teamId, TenantId = tenantId }, false);
        }

        public static async Task OptOutUser(string tenantId, string userId, string serviceUrl)
        {
            await MeetupBotDataProvider.SetUserOptInStatus(tenantId, userId, false, serviceUrl);
        }

        public static async Task OptInUser(string tenantId, string userId, string serviceUrl)
        {
            await MeetupBotDataProvider.SetUserOptInStatus(tenantId, userId, true, serviceUrl);
        }

        private static async Task<TeamsChannelAccount[]> GetTeamMembers(string serviceUrl, string teamId, string tenantId)
        {
            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

            using (var connector = new ConnectorClient(new Uri(serviceUrl)))
            {
                // Pull the roster of specified team and then remove everyone who has opted out explicitly
#pragma warning disable CS0618 // Type or member is obsolete
                var members = await connector.Conversations.GetTeamsConversationMembersAsync(teamId, tenantId);
#pragma warning restore CS0618 // Type or member is obsolete
                return members;
            }
        }

        private static async Task<List<ChannelAccount>> GetOptedInUsers(TeamInstallInfo teamInfo)
        {
            var optedInUsers = new List<ChannelAccount>();

            var members = await GetTeamMembers(teamInfo.ServiceUrl, teamInfo.TeamId, teamInfo.TenantId);

            foreach (var member in members)
            {
                var optInStatus = MeetupBotDataProvider.GetUserOptInStatus(teamInfo.TenantId, member.ObjectId);
                var isBot = string.IsNullOrEmpty(member.Surname);

                if ((optInStatus == null || optInStatus.OptedIn) && !isBot)
                {
                    optedInUsers.Add(member);
                }
            }

            return optedInUsers;
            
        }

        private static List<Tuple<ChannelAccount, ChannelAccount>> MakePairs(List<ChannelAccount> users)
        {
            var pairs = new List<Tuple<ChannelAccount, ChannelAccount>>();

            Randomize<ChannelAccount>(users);

            for (int i = 0; i < users.Count - 1; i += 2)
            {
                pairs.Add(new Tuple<ChannelAccount, ChannelAccount>(users[i], users[i + 1]));
            }
            
            return pairs;
        }

        public static void Randomize<T>(IList<T> items)
        {
            Random rand = new Random(Guid.NewGuid().GetHashCode());

            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Count - 1; i++)
            {
                int j = rand.Next(i, items.Count);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }
    
}