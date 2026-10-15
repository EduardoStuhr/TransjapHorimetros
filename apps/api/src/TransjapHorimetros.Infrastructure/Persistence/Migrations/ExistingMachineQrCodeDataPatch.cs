namespace TransjapHorimetros.Infrastructure.Persistence.Migrations;

public static class ExistingMachineQrCodeDataPatch
{
    public const string PrerequisiteMigration = "20261015131547_AddMachineQrCodes";
    public const string MigrationId = "20261015140153_SeedExistingMachineQrCodes";
    public const int ExpectedQrCodeCount = 51;

    public static readonly IReadOnlyList<(int FleetNumber, string Token)> Mappings =
    [
        (68, "0068"),
        (70, "0070"),
        (74, "0074"),
        (84, "0084"),
        (88, "0088"),
        (90, "0090"),
        (112, "0112"),
        (118, "0118"),
        (122, "0122"),
        (124, "0124"),
        (128, "0128"),
        (142, "0142"),
        (156, "0156"),
        (164, "0164"),
        (168, "0168"),
        (182, "0182"),
        (192, "0192"),
        (194, "0194"),
        (200, "0200"),
        (204, "0204"),
        (212, "0212"),
        (214, "0214"),
        (218, "0218"),
        (228, "0228"),
        (230, "0230"),
        (232, "0232"),
        (236, "0236"),
        (238, "0238"),
        (240, "0240"),
        (242, "0242"),
        (244, "0244"),
        (246, "0246"),
        (248, "0248"),
        (250, "0250"),
        (254, "0254"),
        (256, "0256"),
        (258, "0258"),
        (260, "0260"),
        (262, "0262"),
        (264, "0264"),
        (266, "0266"),
        (268, "0268"),
        (270, "0270"),
        (274, "0274"),
        (278, "0278"),
        (280, "0280"),
        (282, "0282"),
        (284, "0284"),
        (290, "0290"),
        (292, "0292"),
        (294, "0294"),
    ];

    public const string Sql =
        """
        SET NOCOUNT ON;

        DECLARE @qr_mappings TABLE
        (
            fleet_number int NOT NULL PRIMARY KEY,
            token nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL UNIQUE
        );

        INSERT INTO @qr_mappings (fleet_number, token)
        VALUES
            (68, N'0068'),
            (70, N'0070'),
            (74, N'0074'),
            (84, N'0084'),
            (88, N'0088'),
            (90, N'0090'),
            (112, N'0112'),
            (118, N'0118'),
            (122, N'0122'),
            (124, N'0124'),
            (128, N'0128'),
            (142, N'0142'),
            (156, N'0156'),
            (164, N'0164'),
            (168, N'0168'),
            (182, N'0182'),
            (192, N'0192'),
            (194, N'0194'),
            (200, N'0200'),
            (204, N'0204'),
            (212, N'0212'),
            (214, N'0214'),
            (218, N'0218'),
            (228, N'0228'),
            (230, N'0230'),
            (232, N'0232'),
            (236, N'0236'),
            (238, N'0238'),
            (240, N'0240'),
            (242, N'0242'),
            (244, N'0244'),
            (246, N'0246'),
            (248, N'0248'),
            (250, N'0250'),
            (254, N'0254'),
            (256, N'0256'),
            (258, N'0258'),
            (260, N'0260'),
            (262, N'0262'),
            (264, N'0264'),
            (266, N'0266'),
            (268, N'0268'),
            (270, N'0270'),
            (274, N'0274'),
            (278, N'0278'),
            (280, N'0280'),
            (282, N'0282'),
            (284, N'0284'),
            (290, N'0290'),
            (292, N'0292'),
            (294, N'0294');

        IF (SELECT COUNT(*) FROM @qr_mappings) <> 51
        BEGIN
            THROW 51001, N'O patch deve conter exatamente 51 QR codes físicos.', 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM @qr_mappings AS mapping
            LEFT JOIN machines AS machine ON machine.fleet_number = mapping.fleet_number
            WHERE machine.id IS NULL
        )
        BEGIN
            THROW 51002, N'Uma ou mais máquinas esperadas para os QR codes físicos não existem.', 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM @qr_mappings AS mapping
            INNER JOIN machines AS machine ON machine.fleet_number = mapping.fleet_number
            INNER JOIN machine_qr_codes AS qr_code
                ON qr_code.token = mapping.token COLLATE Latin1_General_100_BIN2
            WHERE qr_code.machine_id <> machine.id OR qr_code.is_active = 0
        )
        BEGIN
            THROW 51003, N'Um token físico já existe com associação ou estado incompatível.', 1;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM @qr_mappings AS mapping
            INNER JOIN machines AS machine ON machine.fleet_number = mapping.fleet_number
            INNER JOIN machine_qr_codes AS qr_code
                ON qr_code.machine_id = machine.id AND qr_code.is_active = 1
            WHERE qr_code.token <> mapping.token COLLATE Latin1_General_100_BIN2
        )
        BEGIN
            THROW 51004, N'Uma máquina já possui QR ativo diferente do adesivo físico oficial.', 1;
        END;

        DECLARE @now datetime2(7) = SYSUTCDATETIME();

        INSERT INTO machine_qr_codes
        (
            id,
            machine_id,
            token,
            is_active,
            deactivated_at,
            created_at,
            updated_at
        )
        SELECT
            NEWID(),
            machine.id,
            mapping.token,
            1,
            NULL,
            @now,
            @now
        FROM @qr_mappings AS mapping
        INNER JOIN machines AS machine ON machine.fleet_number = mapping.fleet_number
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM machine_qr_codes AS qr_code
            WHERE qr_code.token = mapping.token COLLATE Latin1_General_100_BIN2
        );
        """;
}
