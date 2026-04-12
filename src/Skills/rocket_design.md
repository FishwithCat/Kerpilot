---
id: rocket_design
title: Rocket Design
description: Staging strategy, TWR guidelines per stage, Tsiolkovsky equation, aerodynamic stability, and engine selection
---

STAGING: Discard empty mass as early as possible. Each stage should burn out and separate before the next ignites. Lower stages: high thrust, acceptable Isp. Upper stages: high Isp, lower thrust is fine.

THRUST-TO-WEIGHT RATIO (TWR):
- TWR must be evaluated per stage IN ITS ACTUAL OPERATING ENVIRONMENT, not all at sea level.
- Lower stages fire at sea level → use ASL TWR. Upper stages fire at altitude or vacuum → use vacuum TWR.
- Launch stage (bottom, highest stage number): 1.2–1.7 ASL TWR (with SRBs up to 2.0)
- Upper atmosphere stages: ~1.0 TWR is fine
- Vacuum stages: 0.5+ vacuum TWR is sufficient (e.g. Terrier with 0.4 ASL TWR has ~1.0 vacuum TWR — perfectly adequate)
- IMPORTANT: A low ASL TWR on an upper stage is NOT a problem if that stage fires above the atmosphere. Many vacuum-optimized engines (Terrier, Poodle, Nerv) have poor ASL TWR but excellent vacuum TWR.
- The analyze_vessel tool provides stage_profile with estimated ignition altitude and effective TWR for each stage — use this to assess the full flight.
- Never exceed 4G for crewed missions (crew safety)
- Above 2.0 at launch: diminishing returns, wastes fuel fighting drag

TSIOLKOVSKY ROCKET EQUATION:
Δv = g₀ × Isp × ln(m_wet / m_dry)
- Isp = specific impulse (seconds) — engine efficiency
- m_wet = total mass with fuel, m_dry = mass without fuel
- Higher Isp or higher fuel fraction → more Δv

AERODYNAMIC STABILITY:
- Center of Lift (CoL) must be BEHIND Center of Mass (CoM) for stable flight
- Check stability with both full and empty tanks (CoM shifts as fuel burns)
- Add fins at the bottom or weight at the top if unstable
- Use fairings to cover payload and reduce drag

FAIRINGS: Enclose irregular payloads to reduce aerodynamic drag. The mass penalty is usually worth the drag savings in the lower atmosphere. Use get_atmosphere_data to check atmosphere density profile.

ENGINE SELECTION: Use get_part_info to look up specific engine stats (Isp, thrust, mass). General principles:
- High-thrust, lower-Isp engines for launch stages (need to overcome gravity)
- High-Isp, lower-thrust engines for vacuum stages (efficiency matters more)
- Nuclear engines: very high Isp but heavy — best for large interplanetary transfers
- Ion engines: highest Isp, tiny thrust — probes and long-duration burns only
