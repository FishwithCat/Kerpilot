namespace Kerpilot
{
    public static class SkillDefinitions
    {
        public struct Skill
        {
            public string Id;
            public string Title;
            public string Content;
            public string[] Keywords;
        }

        private static readonly Skill[] AllSkills = new[]
        {
            new Skill
            {
                Id = "orbital_mechanics",
                Title = "Orbital Mechanics",
                Content =
                    "KSP uses patched conics — each vessel is on a Keplerian orbit around one body at a time, switching at SOI boundaries.\n\n" +
                    "BURN DIRECTIONS:\n" +
                    "- Prograde: raises opposite side of orbit (burn at Pe to raise Ap)\n" +
                    "- Retrograde: lowers opposite side (burn at Ap to lower Pe)\n" +
                    "- Normal/Anti-normal: changes orbital inclination (cheapest at AN/DN nodes)\n" +
                    "- Radial: rotates orbit in-plane (rarely efficient, use for fine-tuning)\n\n" +
                    "HOHMANN TRANSFER: Most efficient circular-to-circular transfer. Two burns: " +
                    "(1) burn prograde at departure orbit to enter transfer ellipse, (2) burn prograde at arrival to circularize. " +
                    "Requires correct phase angle — target body must be ahead/behind by a specific angle depending on destination.\n\n" +
                    "GRAVITY TURN (ascent to LKO):\n" +
                    "- Use get_celestial_body and get_atmosphere_data to check the body's atmosphere height for target orbit altitude\n" +
                    "- Launch vertically, begin pitching east immediately after clearing the pad\n" +
                    "- Follow the prograde marker on the navball — keep angle of attack near zero\n" +
                    "- Gradually pitch over; by ~15% of atmosphere height aim for ~45°, nearly horizontal by ~60%\n" +
                    "- Cut engines when Ap reaches target altitude, coast to Ap, then circularize\n" +
                    "- Eastward launch saves Δv from the body's rotation\n\n" +
                    "INCLINATION CHANGES: Very expensive in Δv. Best done at AN/DN of current orbit relative to target plane. " +
                    "Combine with other burns (e.g., raise Ap and change inclination in one burn at AN).\n\n" +
                    "RENDEZVOUS: Match target orbit first, then adjust phase angle. " +
                    "When close (<2km), switch to target-relative velocity mode and kill relative velocity, then dock.\n\n" +
                    "AEROBRAKING: Use atmosphere to slow down for free. Pe inside atmosphere = drag reduces Ap. " +
                    "Use get_celestial_body to check which bodies have atmospheres. Risk: too low Pe = uncontrolled reentry.",
                Keywords = new[]
                {
                    "orbit", "orbital", "apoapsis", "periapsis", "hohmann", "transfer",
                    "maneuver", "node", "inclination", "prograde", "retrograde",
                    "normal", "antinormal", "radial", "gravity turn", "circularize",
                    "rendezvous", "dock", "docking", "aerobrake", "aerobraking",
                    "soi", "sphere of influence", "encounter", "intercept", "burn",
                    "phase angle", "ascending node", "descending node"
                }
            },
            new Skill
            {
                Id = "rocket_design",
                Title = "Rocket Design",
                Content =
                    "STAGING: Discard empty mass as early as possible. Each stage should burn out and separate before the next ignites. " +
                    "Lower stages: high thrust, acceptable Isp. Upper stages: high Isp, lower thrust is fine.\n\n" +
                    "THRUST-TO-WEIGHT RATIO (TWR):\n" +
                    "- TWR must be evaluated per stage IN ITS ACTUAL OPERATING ENVIRONMENT, not all at sea level.\n" +
                    "- Lower stages fire at sea level → use ASL TWR. Upper stages fire at altitude or vacuum → use vacuum TWR.\n" +
                    "- Launch stage (bottom, highest stage number): 1.2–1.7 ASL TWR (with SRBs up to 2.0)\n" +
                    "- Upper atmosphere stages: ~1.0 TWR is fine\n" +
                    "- Vacuum stages: 0.5+ vacuum TWR is sufficient (e.g. Terrier with 0.4 ASL TWR has ~1.0 vacuum TWR — perfectly adequate)\n" +
                    "- IMPORTANT: A low ASL TWR on an upper stage is NOT a problem if that stage fires above the atmosphere. " +
                    "Many vacuum-optimized engines (Terrier, Poodle, Nerv) have poor ASL TWR but excellent vacuum TWR.\n" +
                    "- The analyze_vessel tool provides stage_profile with estimated ignition altitude and effective TWR for each stage — use this to assess the full flight.\n" +
                    "- Never exceed 4G for crewed missions (crew safety)\n" +
                    "- Above 2.0 at launch: diminishing returns, wastes fuel fighting drag\n\n" +
                    "TSIOLKOVSKY ROCKET EQUATION:\n" +
                    "Δv = g₀ × Isp × ln(m_wet / m_dry)\n" +
                    "- Isp = specific impulse (seconds) — engine efficiency\n" +
                    "- m_wet = total mass with fuel, m_dry = mass without fuel\n" +
                    "- Higher Isp or higher fuel fraction → more Δv\n\n" +
                    "AERODYNAMIC STABILITY:\n" +
                    "- Center of Lift (CoL) must be BEHIND Center of Mass (CoM) for stable flight\n" +
                    "- Check stability with both full and empty tanks (CoM shifts as fuel burns)\n" +
                    "- Add fins at the bottom or weight at the top if unstable\n" +
                    "- Use fairings to cover payload and reduce drag\n\n" +
                    "FAIRINGS: Enclose irregular payloads to reduce aerodynamic drag. " +
                    "The mass penalty is usually worth the drag savings in the lower atmosphere. " +
                    "Use get_atmosphere_data to check atmosphere density profile.\n\n" +
                    "ENGINE SELECTION: Use get_part_info to look up specific engine stats (Isp, thrust, mass). " +
                    "General principles:\n" +
                    "- High-thrust, lower-Isp engines for launch stages (need to overcome gravity)\n" +
                    "- High-Isp, lower-thrust engines for vacuum stages (efficiency matters more)\n" +
                    "- Nuclear engines: very high Isp but heavy — best for large interplanetary transfers\n" +
                    "- Ion engines: highest Isp, tiny thrust — probes and long-duration burns only",
                Keywords = new[]
                {
                    "design", "build", "stage", "staging", "twr", "thrust",
                    "mass ratio", "engine", "isp", "specific impulse",
                    "fairing", "aerodynamic", "drag", "stability",
                    "center of mass", "center of lift", "center of pressure",
                    "strut", "fuel tank", "booster", "rocket", "asparagus"
                }
            },
            new Skill
            {
                Id = "delta_v_budget",
                Title = "Delta-v Budget",
                Content =
                    "DELTA-V BUDGETING PRINCIPLES:\n" +
                    "Use get_celestial_body to query actual body parameters (gravity, SOI, atmosphere) for accurate calculations. " +
                    "Planet parameters may differ from stock if mods are installed.\n\n" +
                    "HOW TO ESTIMATE Δv REQUIREMENTS:\n" +
                    "- Surface to LKO: depends on body's gravity and atmosphere. Use get_celestial_body for surface gravity, " +
                    "get_atmosphere_data for atmosphere height. Higher gravity and thicker atmosphere = more Δv.\n" +
                    "- Transfer Δv: depends on orbital velocities of departure and destination bodies. " +
                    "Use get_celestial_body on both bodies to compare orbital parameters.\n" +
                    "- Landing Δv: roughly proportional to surface gravity × descent height. Airless bodies need full powered descent; " +
                    "bodies with atmosphere allow aerobraking (check with get_celestial_body).\n\n" +
                    "BUDGETING TIPS:\n" +
                    "- Add 10-15% safety margin to all estimates\n" +
                    "- Atmospheric ascent losses: typically 10-15% of total ascent Δv\n" +
                    "- Round trips: sum all legs (ascent + transfer + insertion + landing + ascent + return transfer + capture)\n" +
                    "- Use aerobraking at bodies with atmosphere to save Δv on orbit insertion\n" +
                    "- Bi-elliptic transfers can save Δv for very high orbit changes (ratio > 11.94)\n" +
                    "- Use get_vessel_delta_v to check the vessel's current Δv budget per stage\n\n" +
                    "KEY FORMULA: Δv = g₀ × Isp × ln(m_wet / m_dry)\n" +
                    "For transfer between circular orbits: Δv ≈ |v_transfer - v_circular| at each end of the transfer ellipse.",
                Keywords = new[]
                {
                    "delta-v", "deltav", "delta v", "dv", "budget",
                    "fuel", "enough fuel", "how much fuel",
                    "reach", "get to", "travel to", "fly to",
                    "mun", "minmus", "duna", "eve", "jool", "moho", "eeloo",
                    "dres", "laythe", "tylo", "vall", "pol", "bop",
                    "map", "mission plan", "round trip"
                }
            },
            new Skill
            {
                Id = "contracts_guide",
                Title = "Contracts Guide",
                Content =
                    "KSP contracts have parameters (objectives) that must all be completed. Understanding parameter types helps plan missions.\n\n" +
                    "CRITICAL RULE: Only advise on what the contract parameters explicitly require. Do NOT suggest extra objectives, " +
                    "side activities, additional science experiments, or 'while you're there' detours beyond the contract scope. " +
                    "The player's goal is to complete the contract, not to maximize a mission's output.\n\n" +
                    "COMMON CONTRACT TYPES AND THEIR REQUIREMENTS:\n\n" +
                    "1. GATHER SCIENTIFIC DATA FROM [BODY]:\n" +
                    "- Requires recovering or transmitting ANY science experiment data while at that body\n" +
                    "- The cheapest approach: use the lowest-cost experiment available (crew report or EVA report cost nothing extra)\n" +
                    "- For Kerbin: just run a crew report or EVA report on the launch pad — no flight needed\n" +
                    "- For Mun/Minmus: an unmanned probe with a thermometer or barometer is cheaper than a crewed lander\n" +
                    "- Transmitting data is cheaper than recovering if the vessel cannot return (no need for a return stage)\n\n" +
                    "2. EXPLORE [BODY] (World-Firsts):\n" +
                    "- Multi-parameter contracts with specific milestones: orbit, land, plant flag, EVA, return\n" +
                    "- Check which parameters are listed — not all exploration contracts require ALL milestones\n" +
                    "- Only build for the milestones actually listed in the contract parameters\n\n" +
                    "3. TEST [PART] AT [LOCATION]:\n" +
                    "- Requires having the specified part on your vessel and activating it under specific conditions\n" +
                    "- Conditions: celestial body, altitude range, speed range, situation (landed, flying, orbit, sub-orbital)\n" +
                    "- Right-click the part and choose 'Run Test' when ALL conditions are met simultaneously\n" +
                    "- Cost-saving: if test conditions are 'landed at Kerbin', just stage/activate on the launch pad\n" +
                    "- If conditions are 'flying' at low altitude, a minimal rocket with just the test part and a decoupler is enough\n" +
                    "- Only bring exactly the parts needed to reach the test conditions — no extras\n\n" +
                    "4. RESCUE KERBAL FROM ORBIT/SURFACE:\n" +
                    "- Rendezvous with stranded Kerbal, EVA them to your ship, bring them home\n" +
                    "- For LKO rescues: a small capsule with just enough Δv to rendezvous and deorbit\n" +
                    "- Use the cheapest command pod that fits — a Mk1 pod is sufficient for one rescue\n" +
                    "- The rescued Kerbal joins your roster for free — this contract is almost always profitable\n\n" +
                    "5. SATELLITE CONTRACTS (Position a satellite):\n" +
                    "- Place a vessel in a specific orbit: target Ap, Pe, inclination, sometimes LAN\n" +
                    "- Must match ALL orbital parameters within the tolerances shown\n" +
                    "- Vessel needs only: probe core, antenna, power source (solar panel or battery) — absolute minimum\n" +
                    "- Use the smallest probe core (OKTO or Stayputnik) and smallest solar panel to minimize cost\n\n" +
                    "6. SURVEY CONTRACTS (Survey a specific area):\n" +
                    "- Fly over or land at specific geographic coordinates\n" +
                    "- Each waypoint has altitude and situation requirements (flying low, flying high, landed)\n" +
                    "- For 'flying' waypoints: a simple plane or rocket that can reach the altitude is enough\n" +
                    "- For 'landed' waypoints: a small lander or rover; use parachutes if the body has atmosphere\n\n" +
                    "7. TOURISM CONTRACTS:\n" +
                    "- Take tourist Kerbals to specified destinations (orbit body, flyby body, sub-orbital)\n" +
                    "- Tourists are passengers only — they cannot EVA or control the vessel\n" +
                    "- Bundle multiple tourists going to the same destination in one flight\n" +
                    "- For sub-orbital tourism: the cheapest possible rocket that clears 70km and returns safely\n\n" +
                    "CAREER MODE ECONOMY:\n" +
                    "- Always compare contract reward vs. estimated mission cost before accepting\n" +
                    "- Favor contracts where reward >> cost: rescue missions, Kerbin-area part tests, Kerbin science\n" +
                    "- Reuse vessel designs across similar contracts — don't redesign from scratch each time\n" +
                    "- When suggesting a vessel, minimize part count and use the cheapest parts that meet requirements\n" +
                    "- Do NOT over-engineer: if the contract only needs orbit, don't build for landing; if it only needs flyby, don't build for orbit insertion\n\n" +
                    "TOOLS:\n" +
                    "- Use get_active_contracts to check current objectives and their completion state\n" +
                    "- Use get_offered_contracts to browse available contracts before accepting\n" +
                    "- Parameters show 'Complete' or 'Incomplete' state — track progress in-flight\n" +
                    "- Combine multiple contracts for the same destination in one mission to save resources",
                Keywords = new[]
                {
                    "contract", "contracts", "mission", "missions", "objective", "objectives",
                    "gather scientific data", "gather science", "science data",
                    "explore", "world first", "world-first",
                    "test part", "test a part", "part test",
                    "rescue", "rescue kerbal", "stranded",
                    "satellite", "position a satellite", "specific orbit",
                    "survey", "waypoint",
                    "tourism", "tourist", "tourists",
                    "accept", "deadline", "reward", "reputation"
                }
            }
        };

        public static Skill[] GetAllSkills()
        {
            return AllSkills;
        }
    }
}
