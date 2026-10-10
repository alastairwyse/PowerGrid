-- NOTE: If executing through SQL Server Management Studio, set 'SQKCMD Mode' via the 'Query' menu

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


CREATE TYPE dbo.GridIdTableType 
AS TABLE
(
    Id  bigint  NOT NULL 
);
GO


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

--------------------------------------------------------------------------------
-- dbo.BulkInsertStockPrices

CREATE PROCEDURE dbo.BulkInsertStockPrices
(
    @GridItems             GridItemTableType  READONLY, 
    @TransactionTimestamp  datetime2
)
AS
BEGIN

    DECLARE @ErrorMessage  nvarchar(max);

    DECLARE @CurrentTag            nvarchar(max);
    DECLARE @CurrentDataSource     nvarchar(max);
    DECLARE @CurrentDateAsString   nvarchar(max);
    DECLARE @CurrentCompany        nvarchar(max);
    DECLARE @CurrentPriceAsString  nvarchar(max);

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  GridItemData1,
            GridItemData2, 
            GridItemData3, 
            GridItemData4, 
            GridItemData5
    FROM    @GridItems;

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        @CurrentTag, 
                @CurrentDataSource, 
                @CurrentDateAsString, 
                @CurrentCompany, 
                @CurrentPriceAsString;

    WHILE (@@FETCH_STATUS = 0)
        BEGIN

            BEGIN TRY
                INSERT 
                INTO    StockPrices 
                        (
                            Tag, 
                            DataSource, 
                            [Date], 
                            Company, 
                            Price, 
                            TransactionFrom, 
                            TransactionTo 
                        )
                VALUES  (
                            @CurrentTag, 
                            @CurrentDataSource, 
                            CONVERT(date, @CurrentDateAsString, 23), 
                            @CurrentCompany, 
                            CONVERT(money, @CurrentPriceAsString, 0), 
                            @TransactionTimestamp, 
                            CONVERT(datetime2, '9999-12-31T23:59:59.9999999', 126)
                        );
            END TRY
            BEGIN CATCH
                SET @ErrorMessage = N'Error occurred when inserting StockPrice for ''' + ISNULL(@CurrentTag, '(null)') + ''', ''' + ISNULL(@CurrentDataSource, '(null)') + ''', ''' + ISNULL(@CurrentDateAsString, '(null)') + ''', ''' + ISNULL(@CurrentCompany, '(null)') + ''' and ''' + ISNULL(@CurrentPriceAsString, '(null)') + '''; ' + ERROR_MESSAGE();
                THROW 50001, @ErrorMessage, 1;
            END CATCH

            FETCH NEXT 
            FROM        InputTableCursor
            INTO        @CurrentTag, 
                        @CurrentDataSource, 
                        @CurrentDateAsString, 
                        @CurrentCompany, 
                        @CurrentPriceAsString;
        END;

    CLOSE InputTableCursor;
    DEALLOCATE InputTableCursor;

END
GO


--------------------------------------------------------------------------------
-- dbo.BulkDeleteStockPrices

CREATE PROCEDURE dbo.BulkDeleteStockPrices
(
    @GridItemIds           GridIdTableType  READONLY, 
    @TransactionTimestamp  datetime2
)
AS
BEGIN

    DECLARE @ErrorMessage  nvarchar(max);

    BEGIN TRY
        UPDATE  StockPrices 
        SET     TransactionTo = DATEADD(nanosecond, -100, @TransactionTimestamp)
        WHERE   Id IN  
                (
                    SELECT  Id 
                    FROM    @GridItemIds
                );
    END TRY
    BEGIN CATCH
        SET @ErrorMessage = N'Error occurred when deleting StockPrices; ' + ERROR_MESSAGE();
        THROW 50001, @ErrorMessage, 1;
    END CATCH

END
GO