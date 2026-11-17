USE [transjap_horimetros_tests];
-- Compara cada coluna persistida nas tabelas operacionais da origem e da cópia.
DECLARE @table sysname, @statement nvarchar(max);
DECLARE snapshot_tables CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND is_ms_shipped = 0;
OPEN snapshot_tables;
FETCH NEXT FROM snapshot_tables INTO @table;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @source nvarchar(270) = 'dbo.' + QUOTENAME(@table);
    DECLARE @copy nvarchar(300) = 'fase15_recovery_tests.' + @source;
    SET @statement = N'IF EXISTS (SELECT * FROM '+@source+' EXCEPT SELECT * FROM '+@copy+')
        OR EXISTS (SELECT * FROM '+@copy+' EXCEPT SELECT * FROM '+@source+')
        THROW 51002, ''A copia divergiu da origem.'', 1;';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM snapshot_tables INTO @table;
END;
CLOSE snapshot_tables;
DEALLOCATE snapshot_tables;
PRINT 'Tabelas operacionais preservadas.';
