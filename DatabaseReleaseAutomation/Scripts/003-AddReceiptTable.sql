-- Dynamically drop foreign key constraints
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql += 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + 
    ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
FROM sys.foreign_keys;

EXEC sp_executesql @sql;


IF OBJECT_ID('dbo.GroupPayers', 'U') IS NOT NULL
    DROP TABLE dbo.GroupPayers;

IF OBJECT_ID('dbo.PayerProducts', 'U') IS NOT NULL
    DROP TABLE dbo.PayerProducts;

--IF OBJECT_ID('dbo.PayerReceipts', 'U') IS NOT NULL
--    DROP TABLE dbo.PayerReceipts;

IF OBJECT_ID('dbo.ReceiptProducts', 'U') IS NOT NULL
    DROP TABLE dbo.ReceiptProducts;

IF OBJECT_ID('dbo.ProductDiscounts', 'U') IS NOT NULL
    DROP TABLE dbo.ProductDiscounts;

IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
    DROP TABLE dbo.Products;

IF OBJECT_ID('dbo.Payers', 'U') IS NOT NULL
    DROP TABLE dbo.Payers;

IF OBJECT_ID('dbo.Groups', 'U') IS NOT NULL
    DROP TABLE dbo.Groups;

IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
    DROP TABLE dbo.Users;

IF OBJECT_ID('dbo.Receipts', 'U') IS NOT NULL
    DROP TABLE dbo.Receipts;



CREATE TABLE Receipts
(
	SessionId INT PRIMARY KEY IDENTITY,
	Hash NVARCHAR(100) NOT NULL UNIQUE,
	FileName NVARCHAR(100) NOT NULL,
    CurrentProduct INT,
    ProductsInTotal INT,
	SessionSuccessful BIT NOT NULL,
    ShopName NVARCHAR(100),
	RawTotalCost DECIMAL(19,4),
    GroupMode BIT NOT NULL,
    
)
CREATE INDEX IDX_Receipts_Hash ON Receipts(Hash);

CREATE TABLE Users
(
    UserId NVARCHAR(50) PRIMARY KEY,
    UserName NVARCHAR(50),
    CurrentState NVARCHAR(50),
    CurrentSession INT,
    FOREIGN KEY (CurrentSession) REFERENCES Receipts(SessionId)
)

CREATE TABLE Payers
(
	PayerId INT PRIMARY KEY IDENTITY, 
	SessionId INT NOT NULL,
    Name NVARCHAR(50) NOT NULL,
    FOREIGN KEY (SessionId) REFERENCES Receipts(SessionId),
)

CREATE TABLE Products
(
	ProductId INT PRIMARY KEY IDENTITY,
    ProductNumber INT NOT NULL,
    Name NVARCHAR(50) NOT NULL,
	Cost DECIMAL(19,4) NOT NULL
)

CREATE TABLE ProductDiscounts (
	ProductId INT NOT NULL,
    Discount DECIMAL(19,4),
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
)
CREATE INDEX IDX_ProductDiscounts_ProductId ON ProductDiscounts(ProductId);

CREATE TABLE ReceiptProducts
(
	SessionId INT NOT NULL,
	ProductId INT NOT NULL,
	DividedCost	DECIMAL(19,4),
    FOREIGN KEY (SessionId) REFERENCES Receipts(SessionId),
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    PRIMARY KEY (SessionId, ProductId)
)
--CREATE INDEX IDX_ReceiptProducts_SessionId ON ReceiptProducts(SessionId);
--CREATE INDEX IDX_ReceiptProducts_ProductId ON ReceiptProducts(ProductId);

--CREATE TABLE PayerReceipts (
--    PayerId INT NOT NULL,
--    SessionId INT NOT NULL,
--    FOREIGN KEY (PayerId) REFERENCES Payers(PayerId),
--    FOREIGN KEY (SessionId) REFERENCES Receipts(SessionId),
--    PRIMARY KEY (PayerId, SessionId)
--)
--CREATE INDEX IDX_PayerReceipts_PayerId ON PayerReceipts(PayerId);
--CREATE INDEX IDX_PayerReceipts_SessionId ON PayerReceipts(Sessionid);

CREATE TABLE PayerProducts (
    PayerId INT NOT NULL,
    ProductId INT NOT NULL,
    FOREIGN KEY (PayerId) REFERENCES Payers(PayerId),
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId),
    PRIMARY KEY (PayerId, ProductId)
)
--CREATE INDEX IDX_PayerProducts_PayerId ON PayerProducts(PayerId);
--CREATE INDEX IDX_PayerProducts_ProductId ON PayerProducts(ProductId);

CREATE TABLE Groups
(
    GroupId INT PRIMARY KEY IDENTITY,
    GroupName NVARCHAR(50),
    SessionId INT NOT NULL,
    FOREIGN KEY (SessionId) REFERENCES Receipts(SessionId),
)

CREATE TABLE GroupPayers
(
    GroupId INT NOT NULL,
    PayerId INT NOT NULL,
    FOREIGN KEY (GroupId) REFERENCES Groups(GroupId),
    FOREIGN KEY (PayerId) REFERENCES Payers(PayerId),
    PRIMARY KEY (GroupId, PayerId)
)