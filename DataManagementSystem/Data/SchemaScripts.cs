namespace DataManagementSystem.Data;

internal static class SchemaScripts
{
    public const int CurrentVersion = 1;

    public static IReadOnlyList<(int Version, string Sql)> Scripts { get; } =
    [
        (1, V1),
    ];

    private const string V1 = """
        PRAGMA journal_mode=WAL;
        PRAGMA foreign_keys=ON;

        CREATE TABLE IF NOT EXISTS SchemaVersion (
            Version   INTEGER NOT NULL PRIMARY KEY,
            AppliedOn TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Users (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Username     TEXT    NOT NULL UNIQUE,
            PasswordHash TEXT    NOT NULL,
            IsActive     INTEGER NOT NULL DEFAULT 1,
            CreatedOn    TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS DataCapture (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            SerialNumber TEXT    NOT NULL,
            CaptureDate  TEXT    NOT NULL,
            ModelNumber  TEXT    NOT NULL,
            IsActive     INTEGER NOT NULL DEFAULT 1,
            CreatedOn    TEXT    NOT NULL,
            CreatedBy    INTEGER NOT NULL REFERENCES Users(Id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS IX_DataCapture_Serial_MonthYear
            ON DataCapture (SerialNumber, substr(CaptureDate, 3, 6))
            WHERE IsActive = 1;
        """;
}
