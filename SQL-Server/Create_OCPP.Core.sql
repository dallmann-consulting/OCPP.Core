USE [OCPP.Core]
GO
/****** Object:  Table [dbo].[ChargePoint]    Script Date: 20.12.2020 22:54:30 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ChargePoint](
	[ChargePointId] [nvarchar](100) NOT NULL,
	[Name] [nvarchar](100) NULL,
	[Comment] [nvarchar](200) NULL,
	[Username] [nvarchar](50) NULL,
	[Password] [nvarchar](200) NULL,
	[ClientCertThumb] [nvarchar](100) NULL,
 CONSTRAINT [PK_ChargePoint_1] PRIMARY KEY CLUSTERED 
(
	[ChargePointId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ChargeTags]    Script Date: 20.12.2020 22:54:30 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ChargeTags](
	[TagId] [nvarchar](255) NOT NULL,
	[TagName] [nvarchar](200) NULL,
	[ParentTagId] [nvarchar](255) NULL,
	[ExpiryDate] [datetime2](7) NULL,
	[Blocked] [bit] NULL,
 CONSTRAINT [PK_ChargeKeys] PRIMARY KEY CLUSTERED 
(
	[TagId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MessageLog]    Script Date: 20.12.2020 22:54:30 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MessageLog](
	[LogId] [int] IDENTITY(1,1) NOT NULL,
	[LogTime] [datetime2](7) NOT NULL,
	[ChargePointId] [nvarchar](100) NOT NULL,
	[ConnectorId] [int] NULL,
	[Message] [nvarchar](100) NOT NULL,
	[Result] [nvarchar](max) NULL,
	[ErrorCode] [nvarchar](100) NULL,
 CONSTRAINT [PK_MessageLog] PRIMARY KEY CLUSTERED 
(
	[LogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Transactions]    Script Date: 20.12.2020 22:54:30 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Transactions](
	[TransactionId] [int] IDENTITY(1,1) NOT NULL,
	[Uid] [nvarchar](50) NULL,
	[ChargePointId] [nvarchar](100) NOT NULL,
	[ConnectorId] [int] NOT NULL,
	[StartTagId] [nvarchar](255) NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[MeterStart] [float] NOT NULL,
	[StartResult] [nvarchar](100) NULL,
	[StopTagId] [nvarchar](255) NULL,
	[StopTime] [datetime2](7) NULL,
	[MeterStop] [float] NULL,
	[StopReason] [nvarchar](100) NULL,
 CONSTRAINT [PK_Transactions] PRIMARY KEY CLUSTERED 
(
	[TransactionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

/**** New with V1.1.0 ****/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ConnectorStatus](
	[ChargePointId] [nvarchar](100) NOT NULL,
	[ConnectorId] [int] NOT NULL,
	[ConnectorName] [nvarchar](100) NULL,
	[LastStatus] [nvarchar](100) NULL,
	[LastStatusTime] [datetime2](7) NULL,
	[LastMeter] [float] NULL,
	[LastMeterTime] [datetime2](7) NULL,
 CONSTRAINT [PK_ConnectorStatus] PRIMARY KEY CLUSTERED 
(
	[ChargePointId] ASC,
	[ConnectorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[ConnectorStatusView]
AS
SELECT cs.ChargePointId, cs.ConnectorId, cs.ConnectorName, cs.LastStatus, cs.LastStatusTime, cs.LastMeter, cs.LastMeterTime, t.TransactionId, t.StartTagId, t.StartTime, t.MeterStart, t.StartResult, t.StopTagId, t.StopTime, t.MeterStop, 
                  t.StopReason
FROM     dbo.ConnectorStatus AS cs LEFT OUTER JOIN
                  dbo.Transactions AS t ON t.ChargePointId = cs.ChargePointId AND t.ConnectorId = cs.ConnectorId
WHERE  (t.TransactionId IS NULL) OR
                  (t.TransactionId IN
                      (SELECT MAX(TransactionId) AS Expr1
                       FROM      dbo.Transactions
                       GROUP BY ChargePointId, ConnectorId))
GO
/**** End ****/


SET ANSI_PADDING ON
GO
/****** Object:  Index [ChargePoint_Identifier]    Script Date: 20.12.2020 22:54:30 ******/
CREATE UNIQUE NONCLUSTERED INDEX [ChargePoint_Identifier] ON [dbo].[ChargePoint]
(
	[ChargePointId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/**** V1.9.0: index on LogTime renamed (was "IX_MessageLog_ChargePointId"), new index on ChargePointId (migration "TagLengthMessageLogIndex") ****/
CREATE NONCLUSTERED INDEX [IX_MessageLog_LogTime] ON [dbo].[MessageLog]
(
	[LogTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_MessageLog_ChargePointId] ON [dbo].[MessageLog]
(
	[ChargePointId] ASC,
	[LogTime] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Transactions]  WITH CHECK ADD  CONSTRAINT [FK_Transactions_ChargePoint] FOREIGN KEY([ChargePointId])
REFERENCES [dbo].[ChargePoint] ([ChargePointId])
GO
ALTER TABLE [dbo].[Transactions] CHECK CONSTRAINT [FK_Transactions_ChargePoint]
GO
ALTER TABLE [dbo].[Transactions]  WITH CHECK ADD  CONSTRAINT [FK_Transactions_Transactions] FOREIGN KEY([TransactionId])
REFERENCES [dbo].[Transactions] ([TransactionId])
GO
ALTER TABLE [dbo].[Transactions] CHECK CONSTRAINT [FK_Transactions_Transactions]
GO
CREATE NONCLUSTERED INDEX [IX_Transactions_ChargePointId_ConnectorId] ON [dbo].[Transactions]
(
	[ChargePointId] ASC,
	[ConnectorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/**** New with V1.9.0 (migration "TransactionsUidIndex") ****/
CREATE NONCLUSTERED INDEX [IX_Transactions_ChargePointId_Uid] ON [dbo].[Transactions]
(
	[ChargePointId] ASC,
	[Uid] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO


/**** New with V1.7.0 (migration "ConnectorStatusForeignKey") ****/
ALTER TABLE [dbo].[ConnectorStatus] ADD CONSTRAINT [FK_ConnectorStatus_ChargePoint_ChargePointId] FOREIGN KEY ([ChargePointId]) REFERENCES [dbo].[ChargePoint] ([ChargePointId]) ON DELETE CASCADE;
GO

/**** New with V1.8.0: users and roles of the Management-UI (migration "AddIdentity") ****/
CREATE TABLE [dbo].[AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

/**** New with V1.9.0: business events of the charge points (migration "EventLog") ****/
CREATE TABLE [dbo].[EventLog] (
    [EventId] bigint NOT NULL IDENTITY,
    [EventTime] datetime2 NOT NULL,
    [ChargePointId] nvarchar(100) NOT NULL,
    [ConnectorId] int NULL,
    [Category] nvarchar(20) NOT NULL,
    [Severity] nvarchar(10) NOT NULL,
    [EventCode] nvarchar(50) NOT NULL,
    [Parameters] nvarchar(max) NULL,
    [Details] nvarchar(max) NULL,
    CONSTRAINT [PK_EventLog] PRIMARY KEY ([EventId]),
    CONSTRAINT [FK_EventLog_ChargePoint] FOREIGN KEY ([ChargePointId]) REFERENCES [dbo].[ChargePoint] ([ChargePointId]) ON DELETE CASCADE
);
GO
CREATE INDEX [IX_EventLog_ChargePointId_EventTime] ON [dbo].[EventLog] ([ChargePointId], [EventTime]);
GO
CREATE INDEX [IX_EventLog_EventTime] ON [dbo].[EventLog] ([EventTime]);
GO

/****** Object:  Table [dbo].[__EFMigrationsHistory]    Script Date: 15.06.2025 20:07:07 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/**** the script contains all migrations => mark them as applied (otherwise they are reported as pending) ****/
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20240405204318_TransactionsIndex','8.0.3')
GO
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20260503000001_ConnectorStatusForeignKey','10.0.12')
GO
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20260610192144_AddIdentity','10.0.12')
GO
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20261025101244_ChargePointPasswordHash','10.0.12')
GO
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20261025120424_TransactionsUidIndex','10.0.12')
GO
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20261025182008_TagLengthMessageLogIndex','10.0.12')
GO
INSERT INTO [dbo].[__EFMigrationsHistory]
values ('20261026215936_EventLog','10.0.12')
GO

USE [master]
GO
ALTER DATABASE [OCPP.Core] SET  READ_WRITE 
GO
