/*
* Copyright 2026 Alastair Wyse (https://github.com/alastairwyse/PowerGrid/)
* 
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
* 
*     http://www.apache.org/licenses/LICENSE-2.0
* 
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Manages locks to allow concurrent persistence of grids.
    /// </summary>
    public class PersistenceConcurrencyManager
    {
        /*
... can we have a Dictionary whose key is somethign that represents the 'outside set' field... 'outer key values' and inside is a lock object
        The fully qualified name of the class, and the outer key values of class being persisted

acquire lock
  If they key doesn't exist needs to be added
  If it does exist need to lock the lock object and do action... maybe needs to be a Func<>?... in real case return a ComparisonStatistics?
         */
    }
}
