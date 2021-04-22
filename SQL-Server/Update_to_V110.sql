USE [OCPP.Core]
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