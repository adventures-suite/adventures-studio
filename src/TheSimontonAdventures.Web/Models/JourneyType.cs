using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter<JourneyType>))]
public enum JourneyType
{
    Unknown = 0,
    Editorial = 1,
    Recommended = 2,
    Customer = 3,
    Template = 4
}