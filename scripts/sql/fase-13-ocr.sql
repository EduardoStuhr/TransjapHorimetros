SET XACT_ABORT ON;
BEGIN TRANSACTION;
ALTER TABLE [photo_ocr_results] ADD [AlgorithmVersion] nvarchar(64) NULL;

ALTER TABLE [photo_ocr_results] ADD [BelowPreviousReading] bit NULL;

ALTER TABLE [photo_ocr_results] ADD [CandidatesJson] nvarchar(4000) NULL;

ALTER TABLE [photo_ocr_results] ADD [PreviousReadingValue] decimal(12,2) NULL;

ALTER TABLE [photo_ocr_results] ADD [RequestId] uniqueidentifier NULL;

ALTER TABLE [photo_ocr_results] ADD [ReviewReason] nvarchar(64) NULL;

EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [ux_photo_ocr_request] ON [photo_ocr_results] ([RequestId]) WHERE [RequestId] IS NOT NULL;';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261112180316_AddOcrQualityMetadata', N'10.0.12');

COMMIT;
