IF OBJECT_ID('dbo.PayerProducts', 'U') IS NOT NULL
    DROP TABLE dbo.PayerProducts;

IF OBJECT_ID('dbo.PayerReceipts', 'U') IS NOT NULL
    DROP TABLE dbo.PayerReceipts;

IF OBJECT_ID('dbo.ReceiptProducts', 'U') IS NOT NULL
    DROP TABLE dbo.ReceiptProducts;

IF OBJECT_ID('dbo.ProductDiscounts', 'U') IS NOT NULL
    DROP TABLE dbo.ProductDiscounts;

IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
    DROP TABLE dbo.Products;

IF OBJECT_ID('dbo.Payers', 'U') IS NOT NULL
    DROP TABLE dbo.Payers;

IF OBJECT_ID('dbo.Receipts', 'U') IS NOT NULL
    DROP TABLE dbo.Receipts;

IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
    DROP TABLE dbo.Users;


CREATE TABLE Users
(
	UserId NVARCHAR(50) PRIMARY KEY,
	UserName NVARCHAR(50),
	CurrentState NVARCHAR(50)
)

CREATE TABLE Receipts
(
	SessionId INT PRIMARY KEY IDENTITY,
	UserId NVARCHAR(50) NOT NULL,
	Hash NVARCHAR(100) NOT NULL UNIQUE,
	FileName NVARCHAR(100) NOT NULL,
	SessionSuccessful BIT NOT NULL,
    ShopName NVARCHAR(100),
	RawTotalCost DECIMAL(19,4),
	FOREIGN KEY (UserId) REFERENCES Users(UserId)
)
CREATE INDEX IDX_Receipts_UserId ON Receipts(UserId);
CREATE INDEX IDX_Receipts_Hash ON Receipts(Hash);

CREATE TABLE Payers
(
	PayerId INT PRIMARY KEY IDENTITY, 
	UserId NVARCHAR(50),
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
)

CREATE TABLE Products
(
	ProductId INT PRIMARY KEY IDENTITY,
	Cost DECIMAL(19,4)
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

CREATE TABLE PayerReceipts (
    PayerId INT NOT NULL,
    SessionId INT NOT NULL,
    FOREIGN KEY (PayerId) REFERENCES Payers(PayerId),
    FOREIGN KEY (SessionId) REFERENCES Receipts(SessionId),
    PRIMARY KEY (PayerId, SessionId)
)
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

