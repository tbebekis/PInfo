-- PInfo, Copyright (c) 2026 Theo Bebekis, see License.
-- Upgrades schema version 1 to 2; the initializer owns the transaction and version update.

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

