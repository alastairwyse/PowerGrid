--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Database
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

-- NOTE: If executing through SQL Server Management Studio, set 'SQKCMD Mode' via the 'Query' menu

:Setvar DatabaseName PowerGrid

--CREATE DATABASE $(DatabaseName);
--GO

USE $(DatabaseName);
GO 


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Tables
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

CREATE TABLE $(DatabaseName).dbo.StockPrices
(
    Id               bigint        IDENTITY(1,1) PRIMARY KEY NOT NULL, 
    DataSource       nvarchar(50)  NOT NULL, 
    [Date]           date          NOT NULL, 
    Company          nvarchar(50)  NOT NULL, 
    Price            money         NOT NULL, 
    TransactionFrom  datetime2     NOT NULL, 
    TransactionTo    datetime2     NOT NULL
);

CREATE INDEX StockPricesOuterKeysIndex ON $(DatabaseName).dbo.StockPrices (DataSource, [Date], TransactionTo);
CREATE INDEX StockPricesTransactionIndex ON $(DatabaseName).dbo.StockPrices (TransactionTo, TransactionFrom);

CREATE TABLE $(DatabaseName).dbo.StockPriceGrids
(
    Id                    bigint        IDENTITY(1,1) PRIMARY KEY  NOT NULL, 
    DataSource            nvarchar(50)  NOT NULL, 
    [Date]                date          NOT NULL, 
    [Version]             int           NOT NULL, 
    TransactionTimestamp  datetime2     NOT NULL
);

CREATE INDEX StockPriceGridsOuterKeysIndex ON $(DatabaseName).dbo.StockPriceGrids (DataSource, [Date], [Version]);


-- Temporary for connection/command reuse testing
CREATE TABLE $(DatabaseName).dbo.Test
(
    Id              bigint        IDENTITY(1,1) PRIMARY KEY  NOT NULL, 
    DataSource      nvarchar(50)  NOT NULL, 
    Company         nvarchar(50)  NOT NULL, 
    AuditTimestamp  datetime2     NOT NULL
);

CREATE INDEX StockPriceGridsOuterKeysIndex ON $(DatabaseName).dbo.Test (DataSource);