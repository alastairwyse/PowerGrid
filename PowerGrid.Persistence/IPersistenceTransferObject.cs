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

using System;

namespace PowerGrid.Persistence
{
    /// <summary>
    /// Defines properties for an object which can be transported to and temporally persisted in a data store (i.e. with a timespan of the validity of the object).
    /// </summary>
    public interface IPersistenceTransferObject
    {
        /// <summary>A numeric unique id for the object.</summary>
        public Int64 Id { get; }

        /// <summary>The date and time that the object became valid.</summary>
        public DateTime TransactionFrom { get; }

        /// <summary>The date and time that the object ceased to be valid.</summary>
        public DateTime TransactionTo { get; }
    }
}
