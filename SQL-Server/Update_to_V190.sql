USE [OCPP.Core]
GO

/**** New with V1.9.0: charge point passwords are stored as hash => longer column ****/
/**** (migration "ChargePointPasswordHash" - the same as "AutoMigrateDB" would do) ****/

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025101244_ChargePointPasswordHash'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChargePoint]') AND [c].[name] = N'Password');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [ChargePoint] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [ChargePoint] ALTER COLUMN [Password] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025101244_ChargePointPasswordHash'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025101244_ChargePointPasswordHash', N'10.0.12');
END;

COMMIT;
GO

