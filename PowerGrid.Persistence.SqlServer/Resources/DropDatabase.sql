-- NOTE: If executing through SQL Server Management Studio, set 'SQKCMD Mode' via the 'Query' menu

:Setvar DatabaseName PowerGrid

USE $(DatabaseName);
GO 

--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop Tables
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP TABLE $(DatabaseName).dbo.WeatherForecastGrids;
DROP TABLE $(DatabaseName).dbo.WeatherForecasts;
DROP TABLE $(DatabaseName).dbo.StockPriceGrids;
DROP TABLE $(DatabaseName).dbo.StockPrices;
