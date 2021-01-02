BEGIN TRANSACTION;
DROP TABLE IF EXISTS "ChargePoint";
CREATE TABLE IF NOT EXISTS "ChargePoint" (
	"ChargePointId"	TEXT NOT NULL UNIQUE,
	"Name"	TEXT,
	"Comment"	TEXT,
	PRIMARY KEY("ChargePointId")
);
DROP TABLE IF EXISTS "ChargeTags";
CREATE TABLE IF NOT EXISTS "ChargeTags" (
	"TagId"	TEXT NOT NULL UNIQUE,
	"TagName"	TEXT,
	"ParentTagId"	TEXT,
	"ExpiryDate"	TEXT,
	"Blocked"	INTEGER,
	PRIMARY KEY("TagId")
);
DROP TABLE IF EXISTS "MessageLog";
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
DROP TABLE IF EXISTS "Transactions";
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
COMMIT;
