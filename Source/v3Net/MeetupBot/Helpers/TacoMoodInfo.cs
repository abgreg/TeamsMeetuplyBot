using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using System;

namespace MeetupBot.Helpers
{
	public class TacoMoodInfo : Document
	{
		[JsonProperty("tenantId")]
		public string TenantId { get; set; }

		[JsonProperty("teamId")]
		public string TeamId { get; set; }

		[JsonProperty("userId")]
		public string UserId { get; set; }

		[JsonProperty("date")]
		public DateTimeOffset Date { get; set; }

		[JsonProperty("mood")]
		public string Mood { get; set; }
	}
}