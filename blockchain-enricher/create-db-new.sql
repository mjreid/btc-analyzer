CREATE TABLE [AddressQueue]
(
	Id BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	AddressHash VARCHAR(256) NOT NULL,
	IsDirectFind BIT NOT NULL,
	IsProcessed BIT NOT NULL,
	DateAdded DATETIME2 NOT NULL
		CONSTRAINT DF_AddressQueueDateAdded DEFAULT GETUTCDATE()	
)

CREATE TABLE [Address]
(
	Id BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	AddressHash VARCHAR(256) NOT NULL,
	NumberOfTransactions BIGINT NOT NULL,
	AmountReceived BIGINT NOT NULL,
	AmountSent BIGINT NOT NULL,
	FinalBalance BIGINT NOT NULL,
	EarliestTransactionDate DATETIME2 NULL,
	IsDirectFind BIT NOT NULL
)

CREATE TABLE [Transaction]
(
	Id BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	TransactionHash VARCHAR(256) NOT NULL,
	IsBitpayTransaction BIT NOT NULL,
	TransactionDate DATETIME2 NOT NULL,
	ValueIn BIGINT NOT NULL,
	ValueOut BIGINT NOT NULL,
	Fees BIGINT NOT NULL
)

CREATE TABLE [TransactionInput]
(
	Id BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	TransactionHash VARCHAR(256) NOT NULL,
	AddressHash VARCHAR(256) NOT NULL,
	Amount BIGINT NOT NULL,
)

CREATE TABLE [TransactionOutput]
(
	Id BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1),
	Transactionhash VARCHAR(256) NOT NULL,
	AddressHash VARCHAR(256) NOT NULL,
	Amount BIGINT NOT NULL,
)

CREATE NONCLUSTERED INDEX IX_AddressQueueUnprocessed ON [AddressQueue] ([IsProcessed]) INCLUDE ([AddressHash], [IsDirectFind])
CREATE NONCLUSTERED INDEX IX_AddressQueueHash ON [AddressQueue] ([AddressHash])
CREATE UNIQUE NONCLUSTERED INDEX IX_AddressAddressHash ON [Address] ([AddressHash]) INCLUDE ([NumberOfTransactions], [AmountReceived], [AmountSent], [EarliestTransactionDate])
CREATE UNIQUE NONCLUSTERED INDEX IX_TransactionTransactionHash ON [Transaction] ([TransactionHash]) INCLUDE ([TransactionDate], [Fees])
CREATE NONCLUSTERED INDEX IX_TransactionInputTransactionHash ON [TransactionInput] ([TransactionHash]) INCLUDE ([AddressHash], [Amount])