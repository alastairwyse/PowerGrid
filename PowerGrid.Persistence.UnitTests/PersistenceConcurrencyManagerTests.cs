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
using System.Collections.Generic;
using System.Threading;
using PowerGrid.Core.UnitTests;
using PowerGrid.Grids;
using PowerGrid.Persistence;
using NUnit.Framework;

namespace PowerGrid.Persistence.UnitTests
{
    /// <summary>
    /// Unit tests for the  PowerGrid.Persistence.PersistenceConcurrencyManager class.
    /// </summary>
    [TestFixture]
    public class PersistenceConcurrencyManagerTests
    {
        private const String bloombergDataSource = "Bloomberg";
        private const String refinitivDataSource = "Refinitiv";
        private const String canonCompany = "Canon";
        private const String hitachiCompany = "Hitachi";
        private const String sonyCompany = "Sony";
        private const String toyotaCompany = "Toyota";

        private TestUtilities utils;
        private PersistenceConcurrencyManager testPersistenceConcurrencyManager;

        [SetUp]
        protected void SetUp()
        {
            utils = new TestUtilities();
            testPersistenceConcurrencyManager = new PersistenceConcurrencyManager();
        }

        [Test]
        public void AcquireLockAndInvokeAction_LockObjectsDontExist()
        {
            StockPrice stockPrice1 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440);
            StockPrice stockPrice2 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732);
            StockPrice stockPrice3 = new(bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPrice stockPrice4 = new(refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridLockKey stockPriceGridLockKey1 = new(stockPrice1);
            StockPriceGridLockKey stockPriceGridLockKey2 = new(stockPrice2);
            StockPriceGridLockKey stockPriceGridLockKey3 = new(stockPrice3);
            StockPriceGridLockKey stockPriceGridLockKey4 = new(stockPrice4);
            List<String> writeLog = new();
            using (AutoResetEvent completeSignal = new(false))
            {
                Thread thread1 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey1, () =>
                    {
                        Thread.Sleep(250);
                        writeLog.Add(nameof(stockPriceGridLockKey1));
                        completeSignal.Set();
                    });
                });
                Thread thread2 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey2, () => { writeLog.Add(nameof(stockPriceGridLockKey2)); });
                });
                Thread thread3 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey3, () => { writeLog.Add(nameof(stockPriceGridLockKey3)); });
                });
                Thread thread4 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey4, () => { writeLog.Add(nameof(stockPriceGridLockKey4)); });
                });

                thread1.Start();
                thread2.Start();
                thread3.Start();
                thread4.Start();


                Console.WriteLine(stockPriceGridLockKey1.GetHashCode());
                Console.WriteLine(stockPriceGridLockKey2.GetHashCode());


                completeSignal.WaitOne();
                Assert.That(writeLog.Count == 4);
                Assert.That(writeLog[0] == nameof(stockPriceGridLockKey3));
                Assert.That(writeLog[1] == nameof(stockPriceGridLockKey4));
                Assert.That(writeLog[2] == nameof(stockPriceGridLockKey1));
                Assert.That(writeLog[3] == nameof(stockPriceGridLockKey2));
            }
        }

        [Test]
        public void AcquireLockAndInvokeAction_LockObjectAlreadyExists()
        {
            throw new NotImplementedException();
        }
    }
}
