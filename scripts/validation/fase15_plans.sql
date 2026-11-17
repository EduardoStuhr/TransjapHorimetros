USE [fase15_recovery_tests];
DECLARE @machine uniqueidentifier = (SELECT TOP (1) id FROM dbo.machines);
DECLARE @start datetimeoffset = DATEADD(day,-1,SYSDATETIMEOFFSET());
WITH seq AS (
    SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO dbo.hour_meter_readings
    (id,machine_id,value,reading_type,status,captured_at_device,received_at_server,
     synced_at,client_event_id,meter_unit,created_at,updated_at)
SELECT NEWID(),@machine,n,'INTERMEDIATE','VALIDATED',DATEADD(second,n,@start),
    @start,@start,NEWID(),'HOURS',@start,@start FROM seq;
DECLARE @event uniqueidentifier = (SELECT TOP (1) client_event_id FROM dbo.hour_meter_readings WHERE machine_id=@machine);
SET STATISTICS XML ON;
SELECT id FROM dbo.hour_meter_readings WHERE client_event_id=@event;
SELECT TOP (20) id, value FROM dbo.hour_meter_readings WHERE machine_id=@machine AND meter_unit='HOURS'
ORDER BY captured_at_device DESC, received_at_server DESC, id DESC;
SET STATISTICS XML OFF;
