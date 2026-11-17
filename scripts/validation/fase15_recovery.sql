-- Executado pelo sqlcmd dentro do contêiner SQL efêmero da CI.
USE [transjap_horimetros_tests];
IF DB_ID('fase15_recovery_tests') IS NOT NULL
    THROW 51000, 'A base de recuperação ja existe.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.machines)
    THROW 51001, 'A origem precisa conter dados sinteticos.', 1;

BACKUP DATABASE [transjap_horimetros_tests]
    TO DISK = '/var/opt/mssql/data/fase15-recovery.bak' WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = '/var/opt/mssql/data/fase15-recovery.bak' WITH CHECKSUM;

DECLARE @data sysname = (SELECT name FROM sys.database_files WHERE type = 0);
DECLARE @log sysname = (SELECT name FROM sys.database_files WHERE type = 1);
RESTORE DATABASE [fase15_recovery_tests]
    FROM DISK = '/var/opt/mssql/data/fase15-recovery.bak'
    WITH MOVE @data TO '/var/opt/mssql/data/fase15-recovery.mdf',
         MOVE @log TO '/var/opt/mssql/data/fase15-recovery.ldf', CHECKSUM;
GO
