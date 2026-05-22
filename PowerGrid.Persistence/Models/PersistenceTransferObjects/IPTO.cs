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

namespace PowerGrid.Persistence.Models.PersistenceTransferObjects
{
    /// <summary>
    /// Defines properties of a persistence transfer object which allows transfer to persistent storage of a container/model class, which is persisted temporally.
    /// </summary>
    public interface IPTO
    {
        /// <summary>A unique id for the object within persistent storage.</summary>
        public Int64 Id { get; }
        /// <summary>The date and time that the object became active.</summary>
        public DateTime TransactionFrom { get; }
        /// <summary>The date and time that the object was superseded or deleted.</summary>
        public DateTime TransactionTo { get; }
    }
}
