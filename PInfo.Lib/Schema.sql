-- PInfo, Copyright (c) 2026 Theo Bebekis, see License.
-- Country, GeoNames place and favorites schema (version 3).
-- The database initializer must enable foreign_keys on every connection before starting a transaction.
-- Execute this script and set PRAGMA user_version = 3 in the same initialization transaction.
-- Refresh countries with UPSERT, not INSERT OR REPLACE, to preserve related records.

-- Country identifiers connect countries.dev records with future GeoNames alpha-2 records.
CREATE TABLE Country
(
    Alpha3Code TEXT NOT NULL PRIMARY KEY
        CHECK (length(Alpha3Code) = 3 AND Alpha3Code NOT GLOB '*[^A-Z]*'),
    Alpha2Code TEXT NOT NULL UNIQUE
        CHECK (length(Alpha2Code) = 2 AND Alpha2Code NOT GLOB '*[^A-Z]*'),
    Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
    NumericCode TEXT,
    Cioc TEXT,
    NativeName TEXT,
    Capital TEXT,
    Region TEXT,
    Subregion TEXT,
    Demonym TEXT,
    Independent INTEGER NOT NULL CHECK (Independent IN (0, 1)),
    Population INTEGER NOT NULL CHECK (Population >= 0),
    Area REAL CHECK (Area IS NULL OR Area >= 0),
    PopulationDensity REAL CHECK (PopulationDensity IS NULL OR PopulationDensity >= 0),
    Gini REAL CHECK (Gini IS NULL OR Gini BETWEEN 0 AND 100),
    Latitude REAL CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
    Longitude REAL CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
    Flag TEXT,
    FlagPngUrl TEXT,
    FlagSvgUrl TEXT,
    GoogleMapsUrl TEXT,
    OpenStreetMapsUrl TEXT
);

CREATE INDEX IX_Country_Name ON Country (Name COLLATE NOCASE);
CREATE INDEX IX_Country_Region_Subregion ON Country (Region, Subregion);

-- GeoNames country dump records; administrative codes remain text to preserve leading zeros.
-- AlternateNames retains the comma-separated source text, which has no language identifiers.
-- CountryCodes retains the source cc2 field containing additional country codes.
CREATE TABLE Place
(
    GeoNameId INTEGER NOT NULL PRIMARY KEY CHECK (GeoNameId > 0),
    CountryCode TEXT NOT NULL,
    Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
    AsciiName TEXT,
    AlternateNames TEXT,
    Latitude REAL NOT NULL CHECK (Latitude BETWEEN -90 AND 90),
    Longitude REAL NOT NULL CHECK (Longitude BETWEEN -180 AND 180),
    FeatureClass TEXT NOT NULL CHECK (length(FeatureClass) = 1),
    FeatureCode TEXT NOT NULL,
    CountryCodes TEXT,
    Admin1Code TEXT,
    Admin2Code TEXT,
    Admin3Code TEXT,
    Admin4Code TEXT,
    Population INTEGER NOT NULL CHECK (Population >= 0),
    Elevation INTEGER,
    Dem INTEGER,
    Timezone TEXT,
    ModificationDate TEXT NOT NULL,
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha2Code) ON DELETE CASCADE
);

-- Support country lists, name filtering, and selection by GeoNames feature classification.
CREATE INDEX IX_Place_CountryCode_Name ON Place (CountryCode, Name COLLATE NOCASE);
CREATE INDEX IX_Place_CountryCode_Feature ON Place (CountryCode, FeatureClass, FeatureCode);

-- All CountryCode columns below contain Alpha3Code, not Alpha2Code.
-- Collection rows may be replaced within a refresh transaction without replacing the country itself.

-- Country names indexed by language code.
CREATE TABLE CountryTranslation
(
    CountryCode TEXT NOT NULL,
    LanguageCode TEXT NOT NULL,
    Name TEXT NOT NULL,
    PRIMARY KEY (CountryCode, LanguageCode),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Currency details as supplied for each country; distinct names may share a currency code.
CREATE TABLE CountryCurrency
(
    CountryCode TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Symbol TEXT,
    PRIMARY KEY (CountryCode, Code, Name),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Languages as supplied for each country; the alpha-2 language code can be absent.
CREATE TABLE CountryLanguage
(
    CountryCode TEXT NOT NULL,
    Iso639_2 TEXT NOT NULL,
    Iso639_1 TEXT,
    Name TEXT,
    NativeName TEXT,
    PRIMARY KEY (CountryCode, Iso639_2),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Reported neighboring alpha-3 codes; neighbors need not exist in a partial country import.
CREATE TABLE CountryBorder
(
    CountryCode TEXT NOT NULL,
    NeighborAlpha3Code TEXT NOT NULL CHECK (length(NeighborAlpha3Code) = 3 AND NeighborAlpha3Code NOT GLOB '*[^A-Z]*'),
    PRIMARY KEY (CountryCode, NeighborAlpha3Code),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- All source timezone strings associated with a country; a country may have many.
CREATE TABLE CountryTimezone
(
    CountryCode TEXT NOT NULL,
    Timezone TEXT NOT NULL,
    PRIMARY KEY (CountryCode, Timezone),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- All top-level domains associated with a country.
CREATE TABLE CountryDomain
(
    CountryCode TEXT NOT NULL,
    Domain TEXT NOT NULL,
    PRIMARY KEY (CountryCode, Domain),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Calling codes remain text to preserve their source representation.
CREATE TABLE CountryCallingCode
(
    CountryCode TEXT NOT NULL,
    CallingCode TEXT NOT NULL,
    PRIMARY KEY (CountryCode, CallingCode),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Alternative country names and spellings.
CREATE TABLE CountryAlternativeName
(
    CountryCode TEXT NOT NULL,
    Name TEXT NOT NULL,
    PRIMARY KEY (CountryCode, Name),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Regional organizations associated with each country.
CREATE TABLE CountryRegionalBloc
(
    CountryCode TEXT NOT NULL,
    Acronym TEXT NOT NULL,
    Name TEXT,
    PRIMARY KEY (CountryCode, Acronym),
    FOREIGN KEY (CountryCode) REFERENCES Country (Alpha3Code) ON DELETE CASCADE
);

-- Alternative names of a country-associated regional organization.
CREATE TABLE CountryRegionalBlocAlternativeName
(
    CountryCode TEXT NOT NULL,
    Acronym TEXT NOT NULL,
    Name TEXT NOT NULL,
    PRIMARY KEY (CountryCode, Acronym, Name),
    FOREIGN KEY (CountryCode, Acronym)
        REFERENCES CountryRegionalBloc (CountryCode, Acronym) ON DELETE CASCADE
);

-- Alternative acronyms of a country-associated regional organization.
CREATE TABLE CountryRegionalBlocAlternativeAcronym
(
    CountryCode TEXT NOT NULL,
    Acronym TEXT NOT NULL,
    AlternativeAcronym TEXT NOT NULL,
    PRIMARY KEY (CountryCode, Acronym, AlternativeAcronym),
    FOREIGN KEY (CountryCode, Acronym)
        REFERENCES CountryRegionalBloc (CountryCode, Acronym) ON DELETE CASCADE
);

-- Index values used to find countries through their associated data.
CREATE INDEX IX_CountryCurrency_Code ON CountryCurrency (Code);
CREATE INDEX IX_CountryLanguage_Iso639_2 ON CountryLanguage (Iso639_2);
CREATE INDEX IX_CountryBorder_NeighborAlpha3Code ON CountryBorder (NeighborAlpha3Code);
CREATE INDEX IX_CountryTimezone_Timezone ON CountryTimezone (Timezone);
CREATE INDEX IX_CountryDomain_Domain ON CountryDomain (Domain);
CREATE INDEX IX_CountryCallingCode_CallingCode ON CountryCallingCode (CallingCode);

-- Tracks the last successful import, updated atomically with the corresponding dataset.
-- Scope is empty for Countries, or an alpha-2 country code for a country-specific dataset.
-- UpdatedUtc is an ISO 8601 UTC timestamp written consistently using the .NET "O" format.
CREATE TABLE DataRefresh
(
    Dataset TEXT NOT NULL CHECK (length(trim(Dataset)) > 0),
    Scope TEXT NOT NULL DEFAULT '',
    UpdatedUtc TEXT NOT NULL CHECK (length(trim(UpdatedUtc)) > 0),
    PRIMARY KEY (Dataset, Scope)
);

-- Favorites deliberately have no foreign keys to imported datasets: refreshes must not remove saved references.
CREATE TABLE Favorite
(
    Kind INTEGER NOT NULL CHECK (Kind BETWEEN 0 AND 3),
    TargetKey TEXT NOT NULL CHECK (length(trim(TargetKey)) > 0),
    Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
    Context TEXT NOT NULL DEFAULT '',
    DataJson TEXT NOT NULL CHECK (length(trim(DataJson)) > 0),
    AddedUtc TEXT NOT NULL,
    PRIMARY KEY (Kind, TargetKey)
);
