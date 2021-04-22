/**** New with V1.1.0 ****/

CREATE TABLE IF NOT EXISTS "ConnectorStatus" (
	"ChargePointId"	TEXT NOT NULL UNIQUE,
	"ConnectorId" INTEGER,
	"ConnectorName"	TEXT,
	"LastStatus"	TEXT,
	"LastStatusTime"	TEXT,
	"LastMeter"	TEXT,
	"LastMeterTime"	TEXT,
	PRIMARY KEY("ChargePointId", "ConnectorId")
);

CREATE VIEW IF NOT EXISTS "ConnectorStatusView"
AS
SELECT cs.ChargePointId, cs.ConnectorId, cs.ConnectorName, cs.LastStatus, cs.LastStatusTime, cs.LastMeter, cs.LastMeterTime, t.TransactionId, t.StartTagId, t.StartTime, t.MeterStart, t.StartResult, t.StopTagId, t.StopTime, t.MeterStop, t.StopReason
FROM ConnectorStatus AS cs LEFT OUTER JOIN
     Transactions AS t ON t.ChargePointId = cs.ChargePointId AND t.ConnectorId = cs.ConnectorId
WHERE  (t.TransactionId IS NULL) OR
                  (t.TransactionId IN
                      (SELECT MAX(TransactionId) AS Expr1
                       FROM     Transactions
                       GROUP BY ChargePointId, ConnectorId));
					   

/**** End ****/