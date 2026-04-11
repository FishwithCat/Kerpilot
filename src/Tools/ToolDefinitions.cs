using System;

namespace Kerpilot
{
    public static class ToolDefinitions
    {
        private const string ToolsJson =
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_vessel_parts\"," +
                "\"description\":\"Get the vessel's part list with counts, masses, costs, and onboard resources. Works in both flight and VAB/SPH editor. Use when the player asks about their rocket, ship, or vessel composition.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_part_info\"," +
                "\"description\":\"Get detailed info for a specific part type: description, mass, cost, category, manufacturer, resource capacities, and engine performance (thrust, Isp vacuum/sea-level, propellants) if the part is an engine. Use when the player asks about a particular part or engine stats.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{\"part_name\":{\"type\":\"string\",\"description\":\"Name of the part (e.g. FL-T400 Fuel Tank, LV-T30 Reliant Engine)\"}},\"required\":[\"part_name\"]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_celestial_body\"," +
                "\"description\":\"Get parameters of a celestial body: mass, radius, gravity, atmosphere, sphere of influence, and orbital parameters. Use when the player asks about a planet or moon.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{\"body_name\":{\"type\":\"string\",\"description\":\"Name of the celestial body (e.g. Kerbin, Mun, Duna, Eve, Jool)\"}},\"required\":[\"body_name\"]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_active_contracts\"," +
                "\"description\":\"Get all currently active (accepted) contracts with their objectives, completion state, and rewards. Use when the player asks about their current missions or accepted contracts.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_offered_contracts\"," +
                "\"description\":\"Get all currently offered (available) contracts that the player can accept, with their objectives, rewards, deadlines, and prestige level. Use when the player asks what contracts are available, what missions they can take, or wants to choose a new contract.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_vessel_delta_v\"," +
                "\"description\":\"Get the vessel's delta-v budget per stage: delta-v (vacuum/ASL/actual), TWR, ISP, thrust, burn time, and mass. Works in both flight and VAB/SPH editor. Essential for determining if the vessel can reach orbit, escape atmosphere, or reach another body.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_vessel_orbit\"," +
                "\"description\":\"Get the vessel's current orbital parameters: apoapsis, periapsis, inclination, eccentricity, orbital period, true anomaly, time to Ap/Pe, and orbital velocity. Use to assess current trajectory and plan maneuvers.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_vessel_status\"," +
                "\"description\":\"Get the vessel's real-time flight status: altitude, vertical/horizontal speed, G-force, electric charge, CommNet signal, atmosphere pressure/density at current position. Use for situational awareness.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_atmosphere_data\"," +
                "\"description\":\"Get detailed atmosphere data for a celestial body: pressure, temperature, and density at multiple altitudes. Useful for planning ascent profiles, aerobraking, and estimating drag losses. Defaults to current body if in flight.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{\"body_name\":{\"type\":\"string\",\"description\":\"Name of the celestial body (e.g. Kerbin, Eve, Duna, Laythe). Optional — defaults to current body if in flight.\"}},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"list_vessels\"," +
                "\"description\":\"List all vessels in the current game: name, type (Ship, Probe, Debris, etc.), orbital situation, parent body, apoapsis/periapsis, and mass. Use when the player asks about other rockets, ships, debris, or any non-active vessels.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"analyze_vessel\"," +
                "\"description\":\"Analyze the vessel's capabilities using actual game physics: can it lift off, reach orbit, escape SOI? Works in both flight and VAB/SPH editor (assumes Kerbin launch in editor). Computes Δv requirements from real body parameters. Provides per-stage flight profile with estimated ignition altitude and environment-appropriate TWR. Lists reachable destinations with transfer Δv. Use this FIRST when the player asks if their rocket is good enough, can reach somewhere, or has enough fuel.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"search_available_parts\"," +
                "\"description\":\"Search for parts available to the player, filtered by tech tree progress in Career/Science mode. Returns part name, category, cost, mass, tech node, and engine stats if applicable. Use when the player asks what parts they can use, wants to find an engine or fuel tank, or is building a rocket and needs part recommendations.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{" +
                    "\"category\":{\"type\":\"string\",\"description\":\"Filter by category: Pods, FuelTank, Engine, Command, Structural, Aero, Utility, Science, Coupling, Electrical, Ground, Thermal, Cargo, Robotics, Communication, none. Optional.\"}," +
                    "\"search\":{\"type\":\"string\",\"description\":\"Text search in part name, description, and manufacturer. Optional.\"}" +
                "},\"required\":[]}" +
            "}}";

        public static string GetToolsJsonArray()
        {
            return "[" + ToolsJson + "]";
        }

        public static string GetToolStatusLabel(string name)
        {
            switch (name)
            {
                case "get_vessel_parts": return "Analysing vessel...";
                case "get_part_info": return "Looking up part info...";
                case "get_celestial_body": return "Querying celestial body...";
                case "get_active_contracts": return "Checking contracts...";
                case "get_offered_contracts": return "Checking available contracts...";
                case "get_vessel_delta_v": return "Calculating delta-v...";
                case "get_vessel_orbit": return "Reading orbit data...";
                case "get_vessel_status": return "Reading flight status...";
                case "get_atmosphere_data": return "Querying atmosphere data...";
                case "list_vessels": return "Listing vessels...";
                case "analyze_vessel": return "Analyzing vessel capabilities...";
                case "search_available_parts": return "Searching available parts...";
                default: return "Looking up game data...";
            }
        }

        public static string ExecuteTool(string name, string argumentsJson)
        {
            try
            {
                switch (name)
                {
                    case "get_vessel_parts":
                        return GameDataTools.GetVesselParts();

                    case "get_part_info":
                        string partName = JsonHelper.ExtractJsonStringValue(argumentsJson, "part_name");
                        return GameDataTools.GetPartInfo(partName);

                    case "get_celestial_body":
                        string bodyName = JsonHelper.ExtractJsonStringValue(argumentsJson, "body_name");
                        return GameDataTools.GetCelestialBody(bodyName);

                    case "get_active_contracts":
                        return GameDataTools.GetActiveContracts();

                    case "get_offered_contracts":
                        return GameDataTools.GetOfferedContracts();

                    case "get_vessel_delta_v":
                        return GameDataTools.GetVesselDeltaV();

                    case "get_vessel_orbit":
                        return GameDataTools.GetVesselOrbit();

                    case "get_vessel_status":
                        return GameDataTools.GetVesselStatus();

                    case "get_atmosphere_data":
                        string atmBody = JsonHelper.ExtractJsonStringValue(argumentsJson, "body_name");
                        return GameDataTools.GetAtmosphereData(atmBody);

                    case "list_vessels":
                        return GameDataTools.ListVessels();

                    case "analyze_vessel":
                        return GameDataTools.AnalyzeVessel();

                    case "search_available_parts":
                        string partCategory = JsonHelper.ExtractJsonStringValue(argumentsJson, "category");
                        if (!string.IsNullOrEmpty(partCategory) && !GameDataTools.IsValidPartCategory(partCategory))
                            return "{\"error\":\"Unknown category '" + JsonHelper.EscapeJsonString(partCategory) +
                                "'. Valid: " + GameDataTools.GetValidCategoriesList() + "\"}";
                        string partSearch = JsonHelper.ExtractJsonStringValue(argumentsJson, "search");
                        return GameDataTools.SearchAvailableParts(partCategory, partSearch);

                    default:
                        return "{\"error\":\"Unknown tool: " + JsonHelper.EscapeJsonString(name) + "\"}";
                }
            }
            catch (Exception ex)
            {
                return "{\"error\":\"Tool execution failed: " + JsonHelper.EscapeJsonString(ex.Message) + "\"}";
            }
        }
    }
}
