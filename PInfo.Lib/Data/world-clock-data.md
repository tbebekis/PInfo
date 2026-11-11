# World Clock country titles

- Source: IANA Time Zone Database, version 2026f, retrieved 2026-11-11.
- Sources: https://data.iana.org/time-zones/tzdb/zone.tab , https://data.iana.org/time-zones/tzdb/iso3166.tab , https://data.iana.org/time-zones/tzdb/backward .
- These source files are in the public domain: https://data.iana.org/time-zones/tzdb/LICENSE .
- world-clock-titles.json maps identifiers to display titles only; operating-system timezone rules still supply all clock calculations.
- zone.tab is deliberately used for the country containing each named location, rather than interpreting shared post-1970 rules as the location belonging to multiple countries. Direct zone.tab entries take precedence over backward links. Geographic aliases without a direct entry inherit the mapped target title. Generic identifiers without a slash (e.g. EET/CET) and Etc/* identifiers are excluded from country mapping. Unmapped/global/fixed-offset zones retain their original identifier as a readable title.
- Regenerate by joining zone.tab country codes to iso3166.tab names, retaining area and location path components, then resolving backward links. If a path already repeats the country after its area, omit that duplicate path component.
