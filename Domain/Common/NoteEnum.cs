using System.Text.Json.Serialization;

namespace Domain.Common
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NoteEnum
    {
        Contact,
        Deal,
        Task
    }
}
