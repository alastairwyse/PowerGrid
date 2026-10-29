-- Note - double brace symbols ('{{' and '}}') are used to denote where custom code should be inserted


--------------------------------------------------------------------------------
-- Create Table Template

CREATE TABLE $(DatabaseName).dbo.{{ Table Name }}
(
    Id               bigint        IDENTITY(1,1) PRIMARY KEY NOT NULL, 
    {{ Definitions for columns mapping to outer and inner key properties, and value properties }}
    TransactionFrom  datetime2     NOT NULL, 
    TransactionTo    datetime2     NOT NULL
);

CREATE INDEX {{ Table Name }}OuterKeysIndex ON $(DatabaseName).dbo.{{ Table Name }} ({{ outer and inner key property columns }}, TransactionTo);
CREATE INDEX {{ Table Name }}TransactionIndex ON $(DatabaseName).dbo.{{ Table Name }} (TransactionTo, TransactionFrom);


--------------------------------------------------------------------------------
-- Bulk Insert Stored Procedure Template

CREATE PROCEDURE dbo.BulkInsert{{ Table Name }}
(
    @GridItems             GridItemTableType  READONLY, 
    @TransactionTimestamp  datetime2
)
AS
BEGIN

    DECLARE @ErrorMessage  nvarchar(max);

    {{ Definitions of variables which hold each column of each row of parameter @GridItems }}

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  GridItemData1,
            GridItemData2, 
            GridItemData3, 
            GridItemData4, 
            {{ Add or remove columns depending on width of grid to be read }}
    FROM    @GridItems;

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        {{ Variables defined above }}
                ;

    WHILE (@@FETCH_STATUS = 0)
        BEGIN

            BEGIN TRY
                INSERT 
                INTO    {{ Table Name }} 
                        (
                            {{ Outer and inner key, and value property columns }}
                            TransactionFrom, 
                            TransactionTo 
                        )
                VALUES  (
                            {{ Variables defined above including any CAST/CONVERT functions required to convert to column types }}
                            @TransactionTimestamp, 
                            CONVERT(datetime2, '9999-12-31T23:59:59.9999999', 126)
                        );
            END TRY
            BEGIN CATCH
                SET @ErrorMessage = N'Error occurred when inserting {{ Grid item name }} for ''' {{ column variable names and values, see commented example below }} '''; ' + ERROR_MESSAGE();
                -- SET @ErrorMessage = N'Error occurred when inserting StockPrice for ''' + ISNULL(@CurrentTag, '(null)') + ''', ''' + ISNULL(@CurrentDataSource, '(null)') + ''', ''' + ISNULL(@CurrentDateAsString, '(null)') + ''', ''' + ISNULL(@CurrentCompany, '(null)') + ''' and ''' + ISNULL(@CurrentPriceAsString, '(null)') + '''; ' + ERROR_MESSAGE();
                THROW 50001, @ErrorMessage, 1;
            END CATCH

            FETCH NEXT 
            FROM        InputTableCursor
            INTO        {{ Variables defined above }}
                        ;
        END;

    CLOSE InputTableCursor;
    DEALLOCATE InputTableCursor;

END
GO


--------------------------------------------------------------------------------
-- Bulk Delete Stored Procedure Template

CREATE PROCEDURE dbo.BulkDelete{{ Table Name }}
(
    @GridItemIds           GridIdTableType  READONLY, 
    @TransactionTimestamp  datetime2
)
AS
BEGIN

    DECLARE @ErrorMessage  nvarchar(max);

    BEGIN TRY
        UPDATE  {{ Table Name }} 
        SET     TransactionTo = DATEADD(nanosecond, -100, @TransactionTimestamp)
        WHERE   Id IN  
                (
                    SELECT  Id 
                    FROM    @GridItemIds
                );
    END TRY
    BEGIN CATCH
        SET @ErrorMessage = N'Error occurred when deleting {{ Grid item name }}; ' + ERROR_MESSAGE();
        THROW 50001, @ErrorMessage, 1;
    END CATCH

END
GO