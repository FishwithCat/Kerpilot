using System;
using System.Collections.Generic;
using System.Text;

namespace Kerpilot
{
    public static class GameDataTools
    {
        public static string GetVesselParts()
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
                return "{\"error\":\"No active vessel. You must be in flight scene with an active vessel.\"}";

            var partCounts = new Dictionary<string, int>();
            var partMasses = new Dictionary<string, double>();
            double totalMass = vessel.totalMass;

            var resources = new Dictionary<string, double[]>();
            foreach (var part in vessel.parts)
            {
                string title = part.partInfo.title;
                if (partCounts.ContainsKey(title))
                {
                    partCounts[title]++;
                    partMasses[title] += part.mass + part.GetResourceMass();
                }
                else
                {
                    partCounts[title] = 1;
                    partMasses[title] = part.mass + part.GetResourceMass();
                }

                foreach (PartResource res in part.Resources)
                {
                    if (resources.ContainsKey(res.resourceName))
                    {
                        resources[res.resourceName][0] += res.amount;
                        resources[res.resourceName][1] += res.maxAmount;
                    }
                    else
                    {
                        resources[res.resourceName] = new double[] { res.amount, res.maxAmount };
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append("{\"vessel_name\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.vesselName));
            sb.Append("\",\"total_parts\":");
            sb.Append(vessel.parts.Count);
            sb.Append(",\"total_mass_tons\":");
            sb.Append(totalMass.ToString("F2"));
            sb.Append(",\"situation\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.situation.ToString()));
            sb.Append("\",\"parts\":[");

            bool first = true;
            foreach (var kv in partCounts)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("{\"name\":\"");
                sb.Append(JsonHelper.EscapeJsonString(kv.Key));
                sb.Append("\",\"count\":");
                sb.Append(kv.Value);
                sb.Append(",\"mass_tons\":");
                sb.Append(partMasses[kv.Key].ToString("F3"));
                sb.Append("}");
            }

            sb.Append("],\"resources\":[");
            first = true;
            foreach (var kv in resources)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("{\"name\":\"");
                sb.Append(JsonHelper.EscapeJsonString(kv.Key));
                sb.Append("\",\"amount\":");
                sb.Append(kv.Value[0].ToString("F1"));
                sb.Append(",\"max_amount\":");
                sb.Append(kv.Value[1].ToString("F1"));
                sb.Append("}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string GetPartInfo(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return "{\"error\":\"part_name parameter is required.\"}";

            string searchLower = partName.ToLowerInvariant();
            AvailablePart found = null;

            foreach (var ap in PartLoader.LoadedPartsList)
            {
                if (ap.title.ToLowerInvariant() == searchLower)
                {
                    found = ap;
                    break;
                }
            }

            // Fallback: partial match
            if (found == null)
            {
                foreach (var ap in PartLoader.LoadedPartsList)
                {
                    if (ap.title.ToLowerInvariant().Contains(searchLower))
                    {
                        found = ap;
                        break;
                    }
                }
            }

            if (found == null)
            {
                // Suggest some available parts
                var suggestions = new StringBuilder();
                suggestions.Append("{\"error\":\"Part '");
                suggestions.Append(JsonHelper.EscapeJsonString(partName));
                suggestions.Append("' not found.\",\"suggestions\":[");
                int count = 0;
                foreach (var ap in PartLoader.LoadedPartsList)
                {
                    if (count >= 10) break;
                    if (ap.title.ToLowerInvariant().Contains(searchLower.Length > 2 ? searchLower.Substring(0, 2) : searchLower))
                    {
                        if (count > 0) suggestions.Append(",");
                        suggestions.Append("\"");
                        suggestions.Append(JsonHelper.EscapeJsonString(ap.title));
                        suggestions.Append("\"");
                        count++;
                    }
                }
                suggestions.Append("]}");
                return suggestions.ToString();
            }

            var sb = new StringBuilder();
            sb.Append("{\"title\":\"");
            sb.Append(JsonHelper.EscapeJsonString(found.title));
            sb.Append("\",\"description\":\"");
            sb.Append(JsonHelper.EscapeJsonString(found.description));
            sb.Append("\",\"cost\":");
            sb.Append(found.cost);
            sb.Append(",\"mass_tons\":");
            sb.Append(found.partPrefab != null ? found.partPrefab.mass.ToString("F3") : "0");
            sb.Append(",\"category\":\"");
            sb.Append(JsonHelper.EscapeJsonString(found.category.ToString()));
            sb.Append("\",\"manufacturer\":\"");
            sb.Append(JsonHelper.EscapeJsonString(found.manufacturer));
            sb.Append("\"");

            // Resource capacities from prefab
            if (found.partPrefab != null && found.partPrefab.Resources.Count > 0)
            {
                sb.Append(",\"resources\":[");
                bool first = true;
                foreach (PartResource res in found.partPrefab.Resources)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{\"name\":\"");
                    sb.Append(JsonHelper.EscapeJsonString(res.resourceName));
                    sb.Append("\",\"max_amount\":");
                    sb.Append(res.maxAmount.ToString("F1"));
                    sb.Append("}");
                }
                sb.Append("]");
            }

            // Engine performance data from ModuleEngines
            if (found.partPrefab != null)
            {
                var engine = found.partPrefab.GetComponent<ModuleEngines>();
                if (engine != null)
                {
                    sb.Append(",\"engine\":{");
                    sb.Append("\"max_thrust_kN\":");
                    sb.Append(engine.maxThrust.ToString("F1"));
                    sb.Append(",\"min_thrust_kN\":");
                    sb.Append(engine.minThrust.ToString("F1"));
                    sb.Append(",\"isp_vacuum\":");
                    sb.Append(engine.atmosphereCurve.Evaluate(0f).ToString("F1"));
                    sb.Append(",\"isp_sea_level\":");
                    sb.Append(engine.atmosphereCurve.Evaluate(1f).ToString("F1"));
                    sb.Append(",\"throttleable\":");
                    sb.Append(engine.throttleLocked ? "false" : "true");

                    // Propellants
                    sb.Append(",\"propellants\":[");
                    bool firstProp = true;
                    foreach (var prop in engine.propellants)
                    {
                        if (!firstProp) sb.Append(",");
                        firstProp = false;
                        sb.Append("{\"name\":\"");
                        sb.Append(JsonHelper.EscapeJsonString(prop.name));
                        sb.Append("\",\"ratio\":");
                        sb.Append(prop.ratio.ToString("F2"));
                        sb.Append("}");
                    }
                    sb.Append("]");

                    sb.Append("}");
                }
            }

            sb.Append("}");
            return sb.ToString();
        }

        public static string GetCelestialBody(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName))
                return "{\"error\":\"body_name parameter is required.\"}";

            string searchLower = bodyName.ToLowerInvariant();
            CelestialBody found = null;

            foreach (var body in FlightGlobals.Bodies)
            {
                if (body.bodyName.ToLowerInvariant() == searchLower)
                {
                    found = body;
                    break;
                }
            }

            if (found == null)
            {
                var sb2 = new StringBuilder();
                sb2.Append("{\"error\":\"Body '");
                sb2.Append(JsonHelper.EscapeJsonString(bodyName));
                sb2.Append("' not found.\",\"available_bodies\":[");
                for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                {
                    if (i > 0) sb2.Append(",");
                    sb2.Append("\"");
                    sb2.Append(JsonHelper.EscapeJsonString(FlightGlobals.Bodies[i].bodyName));
                    sb2.Append("\"");
                }
                sb2.Append("]}");
                return sb2.ToString();
            }

            var sb = new StringBuilder();
            sb.Append("{\"name\":\"");
            sb.Append(JsonHelper.EscapeJsonString(found.bodyName));
            sb.Append("\",\"mass_kg\":");
            sb.Append(found.Mass.ToString("E4"));
            sb.Append(",\"radius_m\":");
            sb.Append(found.Radius.ToString("F0"));
            sb.Append(",\"surface_gravity_m_s2\":");
            sb.Append(found.GeeASL.ToString("F3"));
            sb.Append(",\"has_atmosphere\":");
            sb.Append(found.atmosphere ? "true" : "false");

            if (found.atmosphere)
            {
                sb.Append(",\"atmosphere_depth_m\":");
                sb.Append(found.atmosphereDepth.ToString("F0"));
                sb.Append(",\"atmosphere_contains_oxygen\":");
                sb.Append(found.atmosphereContainsOxygen ? "true" : "false");
            }

            sb.Append(",\"soi_radius_m\":");
            sb.Append(found.sphereOfInfluence.ToString("F0"));
            sb.Append(",\"has_ocean\":");
            sb.Append(found.ocean ? "true" : "false");
            sb.Append(",\"tidallyLocked\":");
            sb.Append(found.tidallyLocked ? "true" : "false");

            if (found.orbit != null)
            {
                sb.Append(",\"orbit\":{\"semi_major_axis_m\":");
                sb.Append(found.orbit.semiMajorAxis.ToString("E4"));
                sb.Append(",\"eccentricity\":");
                sb.Append(found.orbit.eccentricity.ToString("F4"));
                sb.Append(",\"inclination_deg\":");
                sb.Append(found.orbit.inclination.ToString("F2"));
                sb.Append(",\"orbital_period_s\":");
                sb.Append(found.orbit.period.ToString("F0"));
                sb.Append(",\"referenceBody\":\"");
                sb.Append(JsonHelper.EscapeJsonString(found.orbit.referenceBody.bodyName));
                sb.Append("\"}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        public static string GetActiveContracts()
        {
            var contractSystem = Contracts.ContractSystem.Instance;
            if (contractSystem == null)
                return "{\"error\":\"Contract system not available. It may not be loaded yet.\"}";

            var contracts = contractSystem.Contracts;
            if (contracts == null || contracts.Count == 0)
                return "{\"contracts\":[]}";

            var sb = new StringBuilder();
            sb.Append("{\"contracts\":[");

            bool first = true;
            foreach (var contract in contracts)
            {
                if (contract.ContractState != Contracts.Contract.State.Active)
                    continue;

                if (!first) sb.Append(",");
                first = false;

                sb.Append("{\"title\":\"");
                sb.Append(JsonHelper.EscapeJsonString(contract.Title));
                sb.Append("\",\"description\":\"");
                sb.Append(JsonHelper.EscapeJsonString(contract.Description));
                sb.Append("\",\"rewards\":{\"funds\":");
                sb.Append(contract.FundsCompletion.ToString("F0"));
                sb.Append(",\"science\":");
                sb.Append(contract.ScienceCompletion.ToString("F1"));
                sb.Append(",\"reputation\":");
                sb.Append(contract.ReputationCompletion.ToString("F1"));
                sb.Append("},\"parameters\":[");

                bool firstParam = true;
                foreach (var param in contract.AllParameters)
                {
                    if (!firstParam) sb.Append(",");
                    firstParam = false;
                    sb.Append("{\"title\":\"");
                    sb.Append(JsonHelper.EscapeJsonString(param.Title));
                    sb.Append("\",\"state\":\"");
                    sb.Append(JsonHelper.EscapeJsonString(param.State.ToString()));
                    sb.Append("\"}");
                }

                sb.Append("]}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string GetVesselDeltaV()
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
                return "{\"error\":\"No active vessel.\"}";

            var dvInfo = vessel.VesselDeltaV;
            if (dvInfo == null)
                return "{\"error\":\"Delta-v data not available. The vessel may not be fully loaded.\"}";

            var sb = new StringBuilder();
            sb.Append("{\"vessel_name\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.vesselName));
            sb.Append("\",\"total_delta_v_vacuum_m_s\":");
            sb.Append(dvInfo.TotalDeltaVVac.ToString("F1"));
            sb.Append(",\"total_delta_v_asl_m_s\":");
            sb.Append(dvInfo.TotalDeltaVASL.ToString("F1"));
            sb.Append(",\"total_delta_v_actual_m_s\":");
            sb.Append(dvInfo.TotalDeltaVActual.ToString("F1"));
            sb.Append(",\"total_burn_time_s\":");
            sb.Append(dvInfo.TotalBurnTime.ToString("F1"));
            sb.Append(",\"stages\":[");

            var stages = dvInfo.OperatingStageInfo;
            if (stages != null)
            {
                bool first = true;
                foreach (var stage in stages)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{\"stage\":");
                    sb.Append(stage.stage);
                    sb.Append(",\"delta_v_vacuum_m_s\":");
                    sb.Append(stage.deltaVinVac.ToString("F1"));
                    sb.Append(",\"delta_v_asl_m_s\":");
                    sb.Append(stage.deltaVatASL.ToString("F1"));
                    sb.Append(",\"delta_v_actual_m_s\":");
                    sb.Append(stage.deltaVActual.ToString("F1"));
                    sb.Append(",\"twr_vacuum\":");
                    sb.Append(stage.TWRVac.ToString("F2"));
                    sb.Append(",\"twr_asl\":");
                    sb.Append(stage.TWRASL.ToString("F2"));
                    sb.Append(",\"twr_actual\":");
                    sb.Append(stage.TWRActual.ToString("F2"));
                    sb.Append(",\"isp_vacuum_s\":");
                    sb.Append(stage.ispVac.ToString("F1"));
                    sb.Append(",\"isp_asl_s\":");
                    sb.Append(stage.ispASL.ToString("F1"));
                    sb.Append(",\"thrust_vacuum_kn\":");
                    sb.Append(stage.thrustVac.ToString("F1"));
                    sb.Append(",\"thrust_asl_kn\":");
                    sb.Append(stage.thrustASL.ToString("F1"));
                    sb.Append(",\"burn_time_s\":");
                    sb.Append(stage.stageBurnTime.ToString("F1"));
                    sb.Append(",\"start_mass_tons\":");
                    sb.Append(stage.startMass.ToString("F3"));
                    sb.Append(",\"end_mass_tons\":");
                    sb.Append(stage.endMass.ToString("F3"));
                    sb.Append("}");
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string GetVesselOrbit()
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
                return "{\"error\":\"No active vessel.\"}";

            var orbit = vessel.orbit;
            if (orbit == null)
                return "{\"error\":\"No orbit data available.\"}";

            var sb = new StringBuilder();
            sb.Append("{\"vessel_name\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.vesselName));
            sb.Append("\",\"situation\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.situation.ToString()));
            sb.Append("\",\"reference_body\":\"");
            sb.Append(JsonHelper.EscapeJsonString(orbit.referenceBody.bodyName));
            sb.Append("\",\"apoapsis_m\":");
            sb.Append(orbit.ApA.ToString("F0"));
            sb.Append(",\"periapsis_m\":");
            sb.Append(orbit.PeA.ToString("F0"));
            sb.Append(",\"semi_major_axis_m\":");
            sb.Append(orbit.semiMajorAxis.ToString("F0"));
            sb.Append(",\"eccentricity\":");
            sb.Append(orbit.eccentricity.ToString("F6"));
            sb.Append(",\"inclination_deg\":");
            sb.Append(orbit.inclination.ToString("F2"));
            sb.Append(",\"orbital_period_s\":");
            sb.Append(orbit.period.ToString("F1"));
            sb.Append(",\"true_anomaly_deg\":");
            sb.Append((orbit.trueAnomaly * (180.0 / Math.PI)).ToString("F2"));
            sb.Append(",\"argument_of_periapsis_deg\":");
            sb.Append(orbit.argumentOfPeriapsis.ToString("F2"));
            sb.Append(",\"lan_deg\":");
            sb.Append(orbit.LAN.ToString("F2"));
            sb.Append(",\"time_to_apoapsis_s\":");
            sb.Append(orbit.timeToAp.ToString("F1"));
            sb.Append(",\"time_to_periapsis_s\":");
            sb.Append(orbit.timeToPe.ToString("F1"));
            sb.Append(",\"orbital_velocity_m_s\":");
            sb.Append(orbit.orbitalSpeed.ToString("F1"));
            sb.Append("}");
            return sb.ToString();
        }

        public static string GetVesselStatus()
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
                return "{\"error\":\"No active vessel.\"}";

            var sb = new StringBuilder();
            sb.Append("{\"vessel_name\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.vesselName));
            sb.Append("\",\"situation\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.situation.ToString()));
            sb.Append("\",\"reference_body\":\"");
            sb.Append(JsonHelper.EscapeJsonString(vessel.mainBody.bodyName));

            // Altitude and speed
            sb.Append("\",\"altitude_m\":");
            sb.Append(vessel.altitude.ToString("F1"));
            sb.Append(",\"height_from_terrain_m\":");
            sb.Append(vessel.heightFromTerrain.ToString("F1"));
            sb.Append(",\"vertical_speed_m_s\":");
            sb.Append(vessel.verticalSpeed.ToString("F2"));
            sb.Append(",\"horizontal_speed_m_s\":");
            sb.Append(vessel.horizontalSrfSpeed.ToString("F2"));
            sb.Append(",\"orbital_speed_m_s\":");
            sb.Append(vessel.obt_velocity.magnitude.ToString("F2"));
            sb.Append(",\"surface_speed_m_s\":");
            sb.Append(vessel.srfSpeed.ToString("F2"));

            // G-force
            sb.Append(",\"g_force\":");
            sb.Append(vessel.geeForce.ToString("F2"));

            // Mass
            sb.Append(",\"total_mass_tons\":");
            sb.Append(vessel.totalMass.ToString("F3"));

            // Electric charge
            double elecAmount = 0, elecMax = 0;
            foreach (var part in vessel.parts)
            {
                foreach (PartResource res in part.Resources)
                {
                    if (res.resourceName == "ElectricCharge")
                    {
                        elecAmount += res.amount;
                        elecMax += res.maxAmount;
                    }
                }
            }
            sb.Append(",\"electric_charge\":");
            sb.Append(elecAmount.ToString("F1"));
            sb.Append(",\"electric_charge_max\":");
            sb.Append(elecMax.ToString("F1"));

            // CommNet status
            if (vessel.Connection != null)
            {
                sb.Append(",\"comm_connected\":");
                sb.Append(vessel.Connection.IsConnected ? "true" : "false");
                sb.Append(",\"comm_signal_strength\":");
                sb.Append(vessel.Connection.SignalStrength.ToString("F3"));
            }

            // Atmosphere info at current position
            if (vessel.mainBody.atmosphere && vessel.altitude < vessel.mainBody.atmosphereDepth)
            {
                sb.Append(",\"in_atmosphere\":true");
                double pressure = vessel.mainBody.GetPressure(vessel.altitude);
                double density = vessel.mainBody.GetDensity(pressure,
                    vessel.mainBody.GetTemperature(vessel.altitude));
                sb.Append(",\"atmosphere_pressure_atm\":");
                sb.Append((pressure / 101.325).ToString("F4"));
                sb.Append(",\"atmosphere_density_kg_m3\":");
                sb.Append(density.ToString("F6"));
            }
            else
            {
                sb.Append(",\"in_atmosphere\":false");
            }

            sb.Append("}");
            return sb.ToString();
        }

        public static string GetAtmosphereData(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName))
            {
                // Default to current body if in flight
                var vessel = FlightGlobals.ActiveVessel;
                if (vessel != null)
                    bodyName = vessel.mainBody.bodyName;
                else
                    return "{\"error\":\"body_name parameter is required when not in flight.\"}";
            }

            string searchLower = bodyName.ToLowerInvariant();
            CelestialBody found = null;
            foreach (var body in FlightGlobals.Bodies)
            {
                if (body.bodyName.ToLowerInvariant() == searchLower)
                {
                    found = body;
                    break;
                }
            }

            if (found == null)
                return "{\"error\":\"Body '" + JsonHelper.EscapeJsonString(bodyName) + "' not found.\"}";

            if (!found.atmosphere)
                return "{\"body\":\"" + JsonHelper.EscapeJsonString(found.bodyName) + "\",\"has_atmosphere\":false}";

            var sb = new StringBuilder();
            sb.Append("{\"body\":\"");
            sb.Append(JsonHelper.EscapeJsonString(found.bodyName));
            sb.Append("\",\"has_atmosphere\":true");
            sb.Append(",\"atmosphere_depth_m\":");
            sb.Append(found.atmosphereDepth.ToString("F0"));
            sb.Append(",\"contains_oxygen\":");
            sb.Append(found.atmosphereContainsOxygen ? "true" : "false");
            sb.Append(",\"surface_pressure_kpa\":");
            sb.Append(found.GetPressure(0).ToString("F3"));
            sb.Append(",\"surface_temperature_k\":");
            sb.Append(found.GetTemperature(0).ToString("F1"));
            sb.Append(",\"surface_density_kg_m3\":");
            double surfPressure = found.GetPressure(0);
            double surfTemp = found.GetTemperature(0);
            double surfDensity = found.GetDensity(surfPressure, surfTemp);
            sb.Append(surfDensity.ToString("F6"));

            // Sample atmosphere at key altitudes
            sb.Append(",\"altitude_profile\":[");
            double maxAlt = found.atmosphereDepth;
            double[] sampleFractions = { 0, 0.05, 0.1, 0.2, 0.3, 0.5, 0.7, 0.9, 1.0 };
            bool first = true;
            foreach (double frac in sampleFractions)
            {
                double alt = maxAlt * frac;
                if (!first) sb.Append(",");
                first = false;

                double pressure = found.GetPressure(alt);
                double temp = found.GetTemperature(alt);
                double density = found.GetDensity(pressure, temp);

                sb.Append("{\"altitude_m\":");
                sb.Append(alt.ToString("F0"));
                sb.Append(",\"pressure_kpa\":");
                sb.Append(pressure.ToString("F4"));
                sb.Append(",\"temperature_k\":");
                sb.Append(temp.ToString("F1"));
                sb.Append(",\"density_kg_m3\":");
                sb.Append(density.ToString("F6"));
                sb.Append("}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string ListVessels()
        {
            var vessels = FlightGlobals.Vessels;
            if (vessels == null || vessels.Count == 0)
                return "{\"error\":\"No vessels found. You must be in flight scene.\"}";

            var sb = new StringBuilder();
            sb.Append("{\"vessel_count\":");
            sb.Append(vessels.Count);
            sb.Append(",\"active_vessel\":\"");
            var active = FlightGlobals.ActiveVessel;
            sb.Append(active != null ? JsonHelper.EscapeJsonString(active.vesselName) : "");
            sb.Append("\",\"vessels\":[");

            bool first = true;
            foreach (var v in vessels)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("{\"name\":\"");
                sb.Append(JsonHelper.EscapeJsonString(v.vesselName));
                sb.Append("\",\"type\":\"");
                sb.Append(v.vesselType.ToString());
                sb.Append("\",\"situation\":\"");
                sb.Append(v.situation.ToString());
                sb.Append("\",\"body\":\"");
                sb.Append(JsonHelper.EscapeJsonString(v.mainBody.bodyName));
                sb.Append("\"");

                if (v.orbit != null)
                {
                    sb.Append(",\"apoapsis_m\":");
                    sb.Append(v.orbit.ApA.ToString("F0"));
                    sb.Append(",\"periapsis_m\":");
                    sb.Append(v.orbit.PeA.ToString("F0"));
                }

                sb.Append(",\"mass_tons\":");
                sb.Append(v.totalMass.ToString("F2"));
                sb.Append(",\"is_active\":");
                sb.Append(v == active ? "true" : "false");
                sb.Append("}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

    }
}
