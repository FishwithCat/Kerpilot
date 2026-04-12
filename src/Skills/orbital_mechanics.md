---
id: orbital_mechanics
title: Orbital Mechanics
description: Patched conics, burn directions, Hohmann transfers, gravity turns, inclination changes, rendezvous, and aerobraking
---

KSP uses patched conics — each vessel is on a Keplerian orbit around one body at a time, switching at SOI boundaries.

BURN DIRECTIONS:
- Prograde: raises opposite side of orbit (burn at Pe to raise Ap)
- Retrograde: lowers opposite side (burn at Ap to lower Pe)
- Normal/Anti-normal: changes orbital inclination (cheapest at AN/DN nodes)
- Radial: rotates orbit in-plane (rarely efficient, use for fine-tuning)

HOHMANN TRANSFER: Most efficient circular-to-circular transfer. Two burns: (1) burn prograde at departure orbit to enter transfer ellipse, (2) burn prograde at arrival to circularize. Requires correct phase angle — target body must be ahead/behind by a specific angle depending on destination.

GRAVITY TURN (ascent to LKO):
- Use get_celestial_body and get_atmosphere_data to check the body's atmosphere height for target orbit altitude
- Launch vertically, begin pitching east immediately after clearing the pad
- Follow the prograde marker on the navball — keep angle of attack near zero
- Gradually pitch over; by ~15% of atmosphere height aim for ~45°, nearly horizontal by ~60%
- Cut engines when Ap reaches target altitude, coast to Ap, then circularize
- Eastward launch saves Δv from the body's rotation

INCLINATION CHANGES: Very expensive in Δv. Best done at AN/DN of current orbit relative to target plane. Combine with other burns (e.g., raise Ap and change inclination in one burn at AN).

RENDEZVOUS: Match target orbit first, then adjust phase angle. When close (<2km), switch to target-relative velocity mode and kill relative velocity, then dock.

AEROBRAKING: Use atmosphere to slow down for free. Pe inside atmosphere = drag reduces Ap. Use get_celestial_body to check which bodies have atmospheres. Risk: too low Pe = uncontrolled reentry.
