--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Database
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

-- NOTE: If executing through SQL Server Management Studio, set 'SQKCMD Mode' via the 'Query' menu

:Setvar DatabaseName PowerGrid

CREATE DATABASE $(DatabaseName);
GO

USE $(DatabaseName);
GO 

--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Tables
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

CREATE TABLE $(DatabaseName).dbo.StockPrices
(
    Id               bigint        AUTO_INCREMENT PRIMARY KEY NOT NULL, 
    DataSource       nvarchar(50)  NOT NULL, 
    Date             date          NOT NULL, 
    Company          nvarchar(50)  NOT NULL, 
    Price            money         NOT NULL, -- Could use numeric
    TransactionFrom  datetime2     NOT NULL, 
    TransactionTo    datetime2     NOT NULL
);

-- Indexes on temporal columns and others

CREATE TABLE $(DatabaseName).dbo.StockPriceGrids
(
    Id                    bigint        AUTO_INCREMENT PRIMARY KEY NOT NULL, 
    DataSource            nvarchar(50)  NOT NULL, 
    Date                  date          NOT NULL, 
    Version               int           NOT NULL, 
    TransactionTimestamp  datetime2     NOT NULL
);