-- PInfo, Copyright (c) 2026 Theo Bebekis, see License.
-- Upgrades schema version 2 to 3; the initializer owns the transaction and version update.
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
