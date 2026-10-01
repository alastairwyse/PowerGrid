
:Setvar DatabaseName PowerGrid

USE $(DatabaseName);
GO 



--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create User-defined Types
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------


CREATE TYPE dbo.GridItemTableType 
AS TABLE
(
    GridItemData1    nvarchar(max), 
    GridItemData2    nvarchar(max), 
    GridItemData3    nvarchar(max), 
    GridItemData4    nvarchar(max), 
    GridItemData5    nvarchar(max), 
    GridItemData6    nvarchar(max), 
    GridItemData7    nvarchar(max), 
    GridItemData8    nvarchar(max), 
    GridItemData9    nvarchar(max), 
    GridItemData10   nvarchar(max) 
);
GO


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

--------------------------------------------------------------------------------
-- dbo.ProcessEvents

CREATE PROCEDURE dbo.BulkInsertStockPrices
(
    @Tag                   nvarchar(max), 
    @Datasource            nvarchar(max), 
    @Date                  date, 
    @GridItems             GridItemTableType  READONLY, 
    @TransactionTimestamp  datetime2
)
AS
BEGIN

    DECLARE @CurrentCompany        nvarchar(max);
    DECLARE @CurrentPriceAsString  nvarchar(max);

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  GridItemData1,
            GridItemData2
    FROM    @GridItems;

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        @CurrentCompany, 
                @CurrentPriceAsString;

  -- Use https://github.com/alastairwyse/ApplicationAccess/blob/main/ApplicationAccess.Persistence.Sql.SqlServer/Resources/CreateDatabase.sql#L2053
  --   as a guide
  -- Foreach TGridItem in @GridItems
  --   Insert into the StockPrices table
  --     Tag = tag;
  --     DataSource = dataSource;
  --     Date = date;
  --     Company = company;
  --     Price = price;

END
GO