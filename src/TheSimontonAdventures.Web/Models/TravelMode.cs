using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TravelMode>))]
public enum TravelMode
{
    Unknown = 0,
    Flight = 1,
    Train = 2,
    Cruise = 3,
    Ferry = 4,
    WaterTaxi = 5,
    Taxi = 6,
    Car = 7,
    Bus = 8,
    Walking = 9,
    Other = 10
}