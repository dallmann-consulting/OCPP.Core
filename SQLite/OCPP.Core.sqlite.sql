BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "ChargePoint" (
	"ChargePointId"	TEXT NOT NULL UNIQUE,
	"Name"	TEXT,
	"Comment"	TEXT,
	"Username"	TEXT,
	"Password"	TEXT,
	"ClientCertThumb"	TEXT,
	PRIMARY KEY("ChargePointId")
);
CREATE TABLE IF NOT EXISTS "ChargeTags" (
	"TagId"	TEXT NOT NULL UNIQUE,
	"TagName"	TEXT,
	"ParentTagId"	TEXT,
	"ExpiryDate"	TEXT,
	"Blocked"	INTEGER,
	PRIMARY KEY("TagId")
);
CREATE TABLE IF NOT EXISTS "MessageLog" (
	"LogId"	INTEGER NOT NULL UNIQUE,
	"LogTime"	TEXT,
	"ChargePointId"	TEXT,
	"ConnectorId"	INTEGER,
	"Message"	TEXT,
	"Result"	TEXT,
	"ErrorCode"	TEXT,
	PRIMARY KEY("LogId" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Transactions" (
	"TransactionId"	INTEGER NOT NULL UNIQUE,
	"Uid"	TEXT,
	"ChargePointId"	TEXT,
	"ConnectorId"	INTEGER,
	"StartTagId"	TEXT,
	"StartTime"	TEXT,
	"MeterStart"	REAL,
	"StartResult"	TEXT,
	"StopTagId"	TEXT,
	"StopTime"	TEXT,
	"MeterStop"	REAL,
	"StopReason"	TEXT,
	PRIMARY KEY("TransactionId" AUTOINCREMENT)
);

/**** New with V1.1.0 ****/
CREATE TABLE IF NOT EXISTS "ConnectorStatus" (
	"ChargePointId"	TEXT NOT NULL,
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

COMMIT;
