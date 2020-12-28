BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "ChargePoint" (
	"ChargePointId"	TEXT NOT NULL UNIQUE,
	"Name"	TEXT,
	"Comment"	TEXT,
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
	"ChargePointId"	TEXT,
	"ConnectorId"	INTEGER,
	"StartTagId"	TEXT,
	"StartTime"	TEXT,
	"MeterStart"	INTEGER,
	"StartResult"	TEXT,
	"StopTagId"	TEXT,
	"StopTime"	TEXT,
	"MeterStop"	INTEGER,
	"StopReason"	TEXT,
	PRIMARY KEY("TransactionId" AUTOINCREMENT)
);
COMMIT;
