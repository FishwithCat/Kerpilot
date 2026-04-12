---
id: delta_v_budget
title: Delta-v Budget
keywords: delta-v, deltav, delta v, dv, budget, fuel, enough fuel, how much fuel, reach, get to, travel to, fly to, mun, minmus, duna, eve, jool, moho, eeloo, dres, laythe, tylo, vall, pol, bop, map, mission plan, round trip
---

DELTA-V BUDGETING PRINCIPLES:
Use get_celestial_body to query actual body parameters (gravity, SOI, atmosphere) for accurate calculations. Planet parameters may differ from stock if mods are installed.

HOW TO ESTIMATE Δv REQUIREMENTS:
- Surface to LKO: depends on body's gravity and atmosphere. Use get_celestial_body for surface gravity, get_atmosphere_data for atmosphere height. Higher gravity and thicker atmosphere = more Δv.
- Transfer Δv: depends on orbital velocities of departure and destination bodies. Use get_celestial_body on both bodies to compare orbital parameters.
- Landing Δv: roughly proportional to surface gravity × descent height. Airless bodies need full powered descent; bodies with atmosphere allow aerobraking (check with get_celestial_body).

BUDGETING TIPS:
- Add 10-15% safety margin to all estimates
- Atmospheric ascent losses: typically 10-15% of total ascent Δv
- Round trips: sum all legs (ascent + transfer + insertion + landing + ascent + return transfer + capture)
- Use aerobraking at bodies with atmosphere to save Δv on orbit insertion
- Bi-elliptic transfers can save Δv for very high orbit changes (ratio > 11.94)
- Use get_vessel_delta_v to check the vessel's current Δv budget per stage

KEY FORMULA: Δv = g₀ × Isp × ln(m_wet / m_dry)
For transfer between circular orbits: Δv ≈ |v_transfer - v_circular| at each end of the transfer ellipse.
