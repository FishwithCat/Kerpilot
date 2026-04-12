---
id: contracts_guide
title: Contracts Guide
keywords: contract, contracts, mission, missions, objective, objectives, gather scientific data, gather science, science data, explore, world first, world-first, test part, test a part, part test, rescue, rescue kerbal, stranded, satellite, position a satellite, specific orbit, survey, waypoint, tourism, tourist, tourists, accept, deadline, reward, reputation
---

KSP contracts have parameters (objectives) that must all be completed. Understanding parameter types helps plan missions.

CRITICAL RULE: Only advise on what the contract parameters explicitly require. Do NOT suggest extra objectives, side activities, additional science experiments, or 'while you're there' detours beyond the contract scope. The player's goal is to complete the contract, not to maximize a mission's output.

COMMON CONTRACT TYPES AND THEIR REQUIREMENTS:

1. GATHER SCIENTIFIC DATA FROM [BODY]:
- Requires recovering or transmitting ANY science experiment data while at that body
- The cheapest approach: use the lowest-cost experiment available (crew report or EVA report cost nothing extra)
- For Kerbin: just run a crew report or EVA report on the launch pad — no flight needed
- For Mun/Minmus: an unmanned probe with a thermometer or barometer is cheaper than a crewed lander
- Transmitting data is cheaper than recovering if the vessel cannot return (no need for a return stage)

2. EXPLORE [BODY] (World-Firsts):
- Multi-parameter contracts with specific milestones: orbit, land, plant flag, EVA, return
- Check which parameters are listed — not all exploration contracts require ALL milestones
- Only build for the milestones actually listed in the contract parameters

3. TEST [PART] AT [LOCATION]:
- Requires having the specified part on your vessel and activating it under specific conditions
- Conditions: celestial body, altitude range, speed range, situation (landed, flying, orbit, sub-orbital)
- Right-click the part and choose 'Run Test' when ALL conditions are met simultaneously
- Cost-saving: if test conditions are 'landed at Kerbin', just stage/activate on the launch pad
- If conditions are 'flying' at low altitude, a minimal rocket with just the test part and a decoupler is enough
- Only bring exactly the parts needed to reach the test conditions — no extras

4. RESCUE KERBAL FROM ORBIT/SURFACE:
- Rendezvous with stranded Kerbal, EVA them to your ship, bring them home
- For LKO rescues: a small capsule with just enough Δv to rendezvous and deorbit
- Use the cheapest command pod that fits — a Mk1 pod is sufficient for one rescue
- The rescued Kerbal joins your roster for free — this contract is almost always profitable

5. SATELLITE CONTRACTS (Position a satellite):
- Place a vessel in a specific orbit: target Ap, Pe, inclination, sometimes LAN
- Must match ALL orbital parameters within the tolerances shown
- Vessel needs only: probe core, antenna, power source (solar panel or battery) — absolute minimum
- Use the smallest probe core (OKTO or Stayputnik) and smallest solar panel to minimize cost

6. SURVEY CONTRACTS (Survey a specific area):
- Fly over or land at specific geographic coordinates
- Each waypoint has altitude and situation requirements (flying low, flying high, landed)
- For 'flying' waypoints: a simple plane or rocket that can reach the altitude is enough
- For 'landed' waypoints: a small lander or rover; use parachutes if the body has atmosphere

7. TOURISM CONTRACTS:
- Take tourist Kerbals to specified destinations (orbit body, flyby body, sub-orbital)
- Tourists are passengers only — they cannot EVA or control the vessel
- Bundle multiple tourists going to the same destination in one flight
- For sub-orbital tourism: the cheapest possible rocket that clears 70km and returns safely

CAREER MODE ECONOMY:
- Always compare contract reward vs. estimated mission cost before accepting
- Favor contracts where reward >> cost: rescue missions, Kerbin-area part tests, Kerbin science
- Reuse vessel designs across similar contracts — don't redesign from scratch each time
- When suggesting a vessel, minimize part count and use the cheapest parts that meet requirements
- Do NOT over-engineer: if the contract only needs orbit, don't build for landing; if it only needs flyby, don't build for orbit insertion

TOOLS:
- Use get_contracts to check both active objectives (with completion state) and available contracts before accepting
- Parameters show 'Complete' or 'Incomplete' state — track progress in-flight
- Combine multiple contracts for the same destination in one mission to save resources
