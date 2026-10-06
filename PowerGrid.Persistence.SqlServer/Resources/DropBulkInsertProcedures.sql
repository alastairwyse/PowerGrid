-- NOTE: If executing through SQL Server Management Studio, set 'SQKCMD Mode' via the 'Query' menu

:Setvar DatabaseName PowerGrid

USE $(DatabaseName);
GO 

--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop Functions / Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP PROCEDURE dbo.BulkDeleteStockPrices;
DROP PROCEDURE dbo.BulkInsertStockPrices;


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop User-defined Types
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP TYPE dbo.GridIdTableType;
DROP TYPE dbo.GridItemTableType;