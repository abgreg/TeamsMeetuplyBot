﻿namespace MeetupBot
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
	using global::MeetupBot.Helpers;
	using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;
    using Properties;  

    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                string replyText = null;

                var optOutRequst = false;

                if (activity.Value != null && ((dynamic)activity.Value).optout == true)
                {
                    optOutRequst = true;
                }

                try
                {
                    var senderAadId = activity.From.AsTeamsChannelAccount().Properties["aadObjectId"].ToString();

                    if (activity.Value != null && ((dynamic)activity.Value).mood != null)
                    {
                        var mood = ((dynamic)activity.Value).mood.ToString();
                        var tacomood = new TacoMoodInfo
                        {
                            TenantId = activity.GetChannelData<TeamsChannelData>().Tenant.Id,
                            UserId = senderAadId,
                            Date = DateTimeOffset.UtcNow,
                            Mood = mood
                        };
                        await MeetupBotDataProvider.SaveUserTacoMood(tacomood);
                        if (mood == "happy")
                        {
                            replyText = "We love to see a happy taco. Have a great day! 🌮🙌";
                        }
                        else if (mood == "sad")
                        {
                            replyText = "Aw no... we all have those days. Hang in there! ❤️🥺";
                        }
                        else
                        {
                            replyText = Resources.IDontKnow;
                        }
                    }
                    else if (activity.Text.Contains("<at>Check-In Demo</at> How's everyone doing today?"))
                    {
                        try
						{
                            // Let the application layer handle this.
                            var teamsChannelData = activity.GetChannelData<TeamsChannelData>();
                            await MeetupBot.SendTeamSummary(teamsChannelData.Team.Id, teamsChannelData.Channel.Id);
                            return Request.CreateResponse(HttpStatusCode.OK);
                        }
                        catch (Exception e)
						{
                            replyText = "My robot brain doesn't know how to handle that yet... try asking later!";
                        }
                    }
                    else
                    {
                        replyText = Resources.IDontKnow;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                    replyText = Resources.ErrorOccured;
                }

                using (var connectorClient = new ConnectorClient(new Uri(activity.ServiceUrl)))
                {
                    var replyActivity = activity.CreateReply(replyText);

                    await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
                }

            }
            else
            {
                await HandleSystemMessage(activity);
            }

            var response = Request.CreateResponse(HttpStatusCode.OK);

            return response;
        }

        private async Task<Activity> HandleSystemMessage(Activity message)
        {
            System.Diagnostics.Trace.TraceInformation("Processing system message");

            try
            {
                var channelData = message.GetChannelData<TeamsChannelData>();

                if (message.Type == ActivityTypes.ConversationUpdate)
                {
                    // conversation-update fires whenever a new 1:1 gets created between us and someone else as well
                    // only process the Teams ones.
                    var teamsChannelData = message.GetChannelData<TeamsChannelData>();

                    if (teamsChannelData.Team == null || string.IsNullOrEmpty(teamsChannelData.Team.Id))
                    {
                        // conversation-update is for 1:1 chat. Just ignore.
                        return null;
                    }

                    string memberAddedId = string.Empty;

                    if (message.MembersAdded.Count > 0)
                    {
                        memberAddedId = message.MembersAdded.First().Id;
                    }

                    string memberRemovedId = string.Empty;
                    if (message.MembersRemoved.Count > 0)
                    {
                        memberRemovedId = message.MembersRemoved.First().Id;
                    }

                    string myId = message.Recipient.Id;

                    if (memberAddedId.Equals(myId))
                    {
                        // we were just added to team   
                        await MeetupBot.SaveAddedToTeam(message.ServiceUrl, message.Conversation.Id, channelData.Tenant.Id);

                        // TODO: post activity.from has who added the bot. Can record it in schema.
                    }
                    else if (memberRemovedId.Equals(myId))
                    {
                        // we were just removed from a team
                        await MeetupBot.SaveRemoveFromTeam(message.ServiceUrl, message.Conversation.Id, channelData.Tenant.Id);
                    }
                    else if (!string.IsNullOrEmpty(memberAddedId)) // If I wasn't added or removed, then someome else must have been added to team
                    {
                        // someone else was added
                        // send them a welcome message
                        await MeetupBot.WelcomeUser(message.ServiceUrl, memberAddedId, channelData.Tenant.Id, channelData.Team.Id);

                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.Message);
                throw;
            }
           
        }
    }
}