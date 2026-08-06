using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

/// <summary>Identifies the primary transportation used by a journey segment.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TravelMode>))]
public enum TravelMode
{
    /// <summary>Indicates that transportation has not been classified.</summary>
    Unknown = 0,
    /// <summary>Represents travel by aircraft.</summary>
    Flight = 1,
    /// <summary>Represents travel by passenger rail.</summary>
    Train = 2,
    /// <summary>Represents travel aboard a cruise ship.</summary>
    Cruise = 3,
    /// <summary>Represents travel aboard a ferry.</summary>
    Ferry = 4,
    /// <summary>Represents local transportation by water taxi.</summary>
    WaterTaxi = 5,
    /// <summary>Represents local transportation by taxi.</summary>
    Taxi = 6,
    /// <summary>Represents transportation by car.</summary>
    Car = 7,
    /// <summary>Represents transportation by bus or coach.</summary>
    Bus = 8,
    /// <summary>Represents movement on foot.</summary>
    Walking = 9,
    /// <summary>Represents a mode not covered by another value.</summary>
    Other = 10
}
