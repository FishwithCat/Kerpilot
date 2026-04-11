using System;

namespace Kerpilot
{
    public static class ToolDefinitions
    {
        private const string FlightToolsJson =
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_vessel_parts\"," +
                "\"description\":\"Get the active vessel's part list with counts, masses, and onboard resources. Use when the player asks about their rocket, ship, or vessel composition.\"," +
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
                "\"description\":\"Get all currently active contracts with their objectives, completion state, and rewards. Use when the player asks about their missions or contracts.\"," +
                "\"parameters\":{\"type\":\"object\",\"properties\":{},\"required\":[]}" +
            "}}," +
            "{\"type\":\"function\",\"function\":{" +
                "\"name\":\"get_vessel_delta_v\"," +
                "\"description\":\"Get the vessel's delta-v budget per stage: delta-v (vacuum/ASL/actual), TWR, ISP, thrust, burn time, and mass. Essential for determining if the vessel can reach orbit, escape atmosphere, or reach another body.\"," +
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
            "}}";

        public static string GetToolsJsonArray()
        {
            return "[" + FlightToolsJson + "]";
        }

        public static string GetToolStatusLabel(string name)
        {
            switch (name)
            {
                case "get_vessel_parts": return "Analysing vessel...";
                case "get_part_info": return "Looking up part info...";
                case "get_celestial_body": return "Querying celestial body...";
                case "get_active_contracts": return "Checking contracts...";
                case "get_vessel_delta_v": return "Calculating delta-v...";
                case "get_vessel_orbit": return "Reading orbit data...";
                case "get_vessel_status": return "Reading flight status...";
                case "get_atmosphere_data": return "Querying atmosphere data...";
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

                    case "get_vessel_delta_v":
                        return GameDataTools.GetVesselDeltaV();

                    case "get_vessel_orbit":
                        return GameDataTools.GetVesselOrbit();

                    case "get_vessel_status":
                        return GameDataTools.GetVesselStatus();

                    case "get_atmosphere_data":
                        string atmBody = JsonHelper.ExtractJsonStringValue(argumentsJson, "body_name");
                        return GameDataTools.GetAtmosphereData(atmBody);

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
