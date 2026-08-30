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
    Id               bigint        IDENTITY(1,1) PRIMARY KEY NOT NULL, 
    Tag              nvarchar(50)  NOT NULL, 
    DataSource       nvarchar(50)  NOT NULL, 
    [Date]           date          NOT NULL, 
    Company          nvarchar(50)  NOT NULL, 
    Price            money         NOT NULL, 
    TransactionFrom  datetime2     NOT NULL, 
    TransactionTo    datetime2     NOT NULL
);

CREATE INDEX StockPricesOuterKeysIndex ON $(DatabaseName).dbo.StockPrices (Tag, DataSource, [Date], TransactionTo);
CREATE INDEX StockPricesTransactionIndex ON $(DatabaseName).dbo.StockPrices (TransactionTo, TransactionFrom);

CREATE TABLE $(DatabaseName).dbo.StockPriceGrids
(
    Id                    bigint        IDENTITY(1,1) PRIMARY KEY  NOT NULL, 
    Tag                   nvarchar(50)  NOT NULL, 
    DataSource            nvarchar(50)  NOT NULL, 
    [Date]                date          NOT NULL, 
    [Version]             int           NOT NULL, 
    TransactionTimestamp  datetime2     NOT NULL
);

CREATE INDEX StockPriceGridsOuterKeysIndex ON $(DatabaseName).dbo.StockPriceGrids (Tag, DataSource, [Date], [Version]);

CREATE TABLE $(DatabaseName).dbo.WeatherForecasts
(
    Id               bigint        IDENTITY(1,1) PRIMARY KEY NOT NULL, 
    Tag              nvarchar(50)  NOT NULL, 
    [Date]           date          NOT NULL, 
    [Time]           time          NOT NULL, 
    Country          nvarchar(50)  NOT NULL, 
    City             nvarchar(50)  NOT NULL, 
    Temperature      int           NOT NULL, 
    TransactionFrom  datetime2     NOT NULL, 
    TransactionTo    datetime2     NOT NULL
);

CREATE INDEX WeatherForecastsOuterKeysIndex ON $(DatabaseName).dbo.WeatherForecasts (Tag, [Date], [Time], TransactionTo);
CREATE INDEX WeatherForecastsTransactionIndex ON $(DatabaseName).dbo.WeatherForecasts (TransactionTo, TransactionFrom);

CREATE TABLE $(DatabaseName).dbo.WeatherForecastGrids
(
    Id                    bigint        IDENTITY(1,1) PRIMARY KEY  NOT NULL, 
    Tag                   nvarchar(50)  NOT NULL, 
    [Date]                date          NOT NULL, 
    [Time]                time          NOT NULL, 
    [Version]             int           NOT NULL, 
    TransactionTimestamp  datetime2     NOT NULL
);

CREATE INDEX WeatherForecastGridsOuterKeysIndex ON $(DatabaseName).dbo.WeatherForecastGrids (Tag, [Date], [Time], [Version]);