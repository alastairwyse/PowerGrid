### PowerGrid

A prototype for a system persisting grids of data to a database, with advanced storage features...

* Temporality and versioning of grids
* Immutability of grids
* Comparison of new vs existing grids and persistent storage of only delta changes to avoid duplicated/redundant storage
* All data available via CRUD methods in a REST API
* Simple UI allowing complete CRUD operations on grids (and data points within grids)

#### Immediate TODO
* Is exception handling correct??.. is there a need for exceptions to be thrown in protected methods?.. review.
* CHANGE PUBLIC METHODS ON PERSISTER TO BE ASYNC

* Validation in persister should be an abstract method
* Use new .NET Lock class (https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock?view=net-10.0&viewFallbackFrom=net-8.0) if implementing in .NET 9.0+
* Review StockPriceGridLockKey... abstract, protected properties, and private members... could these be done better?
* Review XML comments on IGridLockKey.  Could these be made better after 2nd read?
* Should 'StockPriceGrids' table in database just be 'Grids' and have a column which denotes the grid type (e.g. 'StockPrice')?
* Should PersistenceConcurrencyManager accept a Func rather than Action?
* Interface for GridPersistence (i.e. in addition to current IGridItemPersister)...
  * Get grid by outer key properties (how to have these an an interface since they'll differ for each grid implementation)
  * Get list of grids by some combination of those key properties
  * Have to somehow have the outer key properties defined as a T type?
* Validator and ordering 'chain' in StockPricePersister... would be easier to read if creating extension methods in LINQ style.  Find a way to do this, but limit the scope (don't let it be global)... maybe by adding a T type constraint to be be IGridItem, or limiting via defined namespace??

#### Longer Term TODO
* 2x grid params.. upsert only + full sync
* Admin endpoint to hard delete by an outer key or common key
* Make the persister inner DataBaseOperationEmitter class into a buffer with configurable size (on persister constructor).  Can set to 1 to minimize memory usage and stream things through... OR set huge to effectively write once (or close to once) to DB.  If coupled with option to write in bulk via a temp table or TVP, you would then have the option of high performance/throughput at the cost of memory usage.
* Have an option to 'delete' (via setting TransactionTo) all current rows, before doing the compare... then it basically will insert everything new every time, and performance becomes similar to a straight insert/overwrite with no compare (since comparer will retrieve 0 existing rows).
* Add another type of grid item (other than StockPrice), and use to push common functionality into 'Core' project, generic classes and methods, etc... (weather forecast)
* StockPricePersister (and any other persister classes) should have logging and metrics.
* Add a 'connectionRetryAction' Action to any persister classes which support transient error retries (possibly already done?).
* Include validation filters which reject datasources etc which don't match a known whitelist... alternative to lookup tables and foreign key constraints (implement as some extension to base?)
* Do deadlock testing (hammer test instance with concurrent reads and writes)
* Add parameter to StockPricePersister.PersistGrid() method to specify comparison type i.e. likely...
  * Full compare
  * Add/update only
  * Delete only (is this possible? since we delete the things that are not in the set?)
* Need to make a note in doco to ensure that DB sorting matches .NET sorting, or that filters are in place to only allow characters that will sort the same

#### Terminology
* Grid - A collection of data points which are stored and managed as a set.  Equivalent to a set of rows in a relational database/
* Grid Item - I single data point within a grid.  Equivalent to a single row in a relational database.
* Key Property - 
* Value Property
* Outer Key Property - (might change this terminology)
