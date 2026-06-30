// SPDX-License-Identifier: MIT
// AgentEval × MAF — the travel domain tools the agent under test can call.

using System.ComponentModel;

namespace AgentEvalMafEvals.Tools;

/// <summary>
/// Pure, deterministic stand-in tools. They return canned data so the demo is offline-stable —
/// the point is the agent's <i>behaviour</i> (does it call the right tool, with sane args?), which
/// is exactly what the evaluation measures. No console side effects: a tool's job is to return data.
/// </summary>
public static class TravelTools
{
    public const string SearchFlightsName = "SearchFlights";
    public const string SearchHotelsName = "SearchHotels";

    [Description("Search for available flights between two cities on a given date.")]
    public static string SearchFlights(
        [Description("Departure city")] string origin,
        [Description("Arrival city")] string destination,
        [Description("Travel date")] string date)
        => $"Found 3 flights {origin}→{destination} on {date}: " +
           "AA101 ($450, 10h), DL205 ($520, 9h), UA309 ($480, 11h).";

    [Description("Search for available hotels in a city for given dates.")]
    public static string SearchHotels(
        [Description("City to search")] string city,
        [Description("Check-in date")] string checkIn,
        [Description("Check-out date")] string checkOut)
        => $"Found 3 hotels in {city}: Hotel Le Marais ($180/night, 4★), " +
           "Ibis Paris ($95/night, 3★), Ritz Paris ($650/night, 5★).";
}
