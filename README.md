### PowerGrid

A prototype for a system persisting grids of data to a database, with advanced storage features...

* Temporality and versioning of grids
* Immutability of grids
* Comparison of new vs existing grids and persistent storage of only delta changes to avoid duplicated/redundant storage
* All data available via CRUD methods in a REST API
* Simple UI allowing complete CRUD operations on grids (and data points within grids)

#### Immediate TODO
* Use new .NET Lock class (https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock?view=net-10.0&viewFallbackFrom=net-8.0) if implementing in .NET 9.0+
* Should we introduce an IGridItem&lt;T&gt; interface which implements both IKeyPropertyComparable&lt;T&gt; and IValuePropertyEquatable&lt;T&gt;?
* Review StockPriceGridLockKey... abstract, protected properties, and private members... could these be done better?
* Review XML comments on IGridLockKey.  Could these be made better after 2nd read?
* Should 'StockPriceGrids' table in database just be 'Grids' and have a column which denotes the grid type (e.g. 'StockPrice')?
* Should PersistenceConcurrencyManager accept a Func rather than Action?
* Interface for GridPersistence (i.e. in addition to current IGridItemPersister)...
  * Get grid by outer key properties (how to have these an an interface since they'll differ for each grid implementation)
  * Get list of grids by some combination of those key properties
  * Have to somehow have the outer key properties defined as a T type?
* GridComparer&lt;T&gt; may need to specify 2 generic types... one for the new data source and one for the existing (since existing will have DB specific PTO properties like Id and temporal dates)... and there should be a constraint so that the existing derives from the new.  OR, another alternative could be to put the query results (new/PTO version) into a Dictionary, and the emitted rows for update/insert (emitted from the GridComparer) could be retrieved from the Dictionary based on key equality.  Second option probably keeps the GridComparer more sinple and generic.
* IGridPersister probably needs 2x T types... one for the base grid item class and the other for the PTO equiv.  Same as what I was suggesting for GridComparer above.

#### Longer Term TODO
* Add another type of grid item (other than StockPrice), and use to push common functionality into 'Core' project, generic classes and methods, etc...
* With above other type of grid item mentioned above, have a 'key', 'tag' or 'set id' property which is part of the IKeyPropertyComparable implementation (but not a natural/real-world property of the class).  This additional property should exist only in a derived class (e.g. if the property mentioned applied to StockPrice, the derived class could be called something like GridStockPrice), and the IKeyPropertyComparable and IValuePropertyEquatable implementations on that derived class should call the base class implementations (and extend them).
* StockPricePersister (and any other persister classes) should have logging and metrics.
* Add a 'connectionRetryAction' Action to any persister classes which support transient error retries.
* Include validation filters which reject datasources etc which don't match a known whitelist... alternative to lookup tables and foreign key constraints

#### Terminology
* Grid - A collection of data points which are stored and managed as a set.  Equivalent to a set of rows in a relational database/
* Grid Item - I single data point within a grid.  Equivalent to a single row in a relational database.
* Key Property - 
* Value Property
* Outer Key Property - (might change this terminology)
