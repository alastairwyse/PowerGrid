### PowerGrid

A prototype for a system persisting grids of data to a database, with advanced storage features...

* Temporality and versioning of grids
* Immutability of grids
* Comparison of new vs existing grids and persistent storage of only delta changes to avoid duplicated/redundant storage
* All data available via CRUD methods in a REST API
* Simple UI allowing complete CRUD operations on grids (and data points within grids)

#### Immediate TODO
* Add tests for GridLockKeyBase (likely via StockPriceGridLockKey)
* Implement GridLockManager
* Use new .NET Lock class (https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock?view=net-10.0&viewFallbackFrom=net-8.0) if implementing in .NET 9.0+
* Should we introduce an IGridItem&lt;T&gt; interface which implements both IKeyPropertyComparable&lt;T&gt; and IValuePropertyEquatable&lt;T&gt;?
* Review StockPriceGridLockKey... abstract, protected properties, and private members... could these be done better?
* Review XML comments on IGridLockKey.  Could these be made better after 2nd read?
* Should 'StockPriceGrids' table in database just be 'Grids' and have a column which denotes the grid type (e.g. 'StockPrice')?
* Should PersistenceConcurrencyManager accept a Func rather than Action?

#### Longer Term TODO
* Add another type of grid item (other than StockPrice), and use to push common functionality into 'Core' project, generic classes and methods, etc...
* With above other type of grid item mentioned above, have a 'key', 'tag' or 'set id' property which is part of the IKeyPropertyComparable implementation (but not a natural/real-world property of the class).  This additional property should exist only in a derived class (e.g. if the property mentioned applied to StockPrice, the derived class could be called something like GridStockPrice), and the IKeyPropertyComparable and IValuePropertyEquatable implementations on that derived class should call the base class implementations (and extend them).
