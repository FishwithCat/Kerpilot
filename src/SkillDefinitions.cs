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
                    "- Launch (sea level): 1.2–1.7 TWR (with SRBs up to 2.0)\n" +
                    "- Upper atmosphere: ~1.0 TWR\n" +
                    "- Vacuum stages: 0.5+ TWR is sufficient\n" +
                    "- Never exceed 4G for crewed missions (crew safety)\n" +
                    "- Above 2.0 at launch: diminishing returns, wastes fuel fighting drag\n" +
                    "- Use get_vessel_delta_v to check actual TWR per stage\n\n" +
                    "TSIOLKOVSKY ROCKET EQUATION:\n" +
                    "Δv = g₀ × Isp × ln(m_wet / m_dry)\n" +
                    "- g₀ = 9.81 m/s²\n" +
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
            }
        };

        public static Skill[] GetAllSkills()
        {
            return AllSkills;
        }
    }
}
