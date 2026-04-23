-- NOTE: If executing through SQL Server Management Studio, set 'SQKCMD Mode' via the 'Query' menu

:Setvar DatabaseName PowerGrid

USE $(DatabaseName);
GO 


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop Functions / Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP FUNCTION dbo.GetTemporalMaxDate;
DROP FUNCTION dbo.SubtractTemporalMinimumTimeUnit;

--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop Tables
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP TABLE $(DatabaseName).dbo.StockPriceGrids;
DROP TABLE $(DatabaseName).dbo.StockPrices;
