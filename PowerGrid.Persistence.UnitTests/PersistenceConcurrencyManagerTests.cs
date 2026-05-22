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
        private const String marketTag = "Market";
        private const String calibratedTag = "Calibrated";
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
            StockPriceGridItem stockPrice1 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440);
            StockPriceGridItem stockPrice2 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732);
            StockPriceGridItem stockPrice3 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridItem stockPrice4 = new(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridItem stockPrice5 = new(calibratedTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridItemGridLockKey stockPriceGridLockKey1 = new(stockPrice1);
            StockPriceGridItemGridLockKey stockPriceGridLockKey2 = new(stockPrice2);
            StockPriceGridItemGridLockKey stockPriceGridLockKey3 = new(stockPrice3);
            StockPriceGridItemGridLockKey stockPriceGridLockKey4 = new(stockPrice4);
            StockPriceGridItemGridLockKey stockPriceGridLockKey5 = new(stockPrice5);
            List<String> writeLog = new();
            using (AutoResetEvent completeSignal = new(false))
            {
                Thread thread1 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey1, () =>
                    {
                        Thread.Sleep(300);
                        writeLog.Add(nameof(stockPriceGridLockKey1));
                    });
                });
                Thread thread2 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey2, () => 
                    { 
                        writeLog.Add(nameof(stockPriceGridLockKey2));
                        completeSignal.Set();
                    });
                });
                Thread thread3 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey3, () => { writeLog.Add(nameof(stockPriceGridLockKey3)); });
                });
                Thread thread4 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey4, () => { writeLog.Add(nameof(stockPriceGridLockKey4)); });
                });
                Thread thread5 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey5, () => { writeLog.Add(nameof(stockPriceGridLockKey5)); });
                });

                thread1.Start();
                Thread.Sleep(50);
                thread2.Start();
                Thread.Sleep(50);
                thread3.Start();
                Thread.Sleep(50);
                thread4.Start();
                Thread.Sleep(50);
                thread5.Start();

                completeSignal.WaitOne();
                Assert.That(writeLog.Count == 5);
                Assert.That(writeLog[0] == nameof(stockPriceGridLockKey3));
                Assert.That(writeLog[1] == nameof(stockPriceGridLockKey4));
                Assert.That(writeLog[2] == nameof(stockPriceGridLockKey5));
                Assert.That(writeLog[3] == nameof(stockPriceGridLockKey1));
                Assert.That(writeLog[4] == nameof(stockPriceGridLockKey2));
            }
        }

        [Test]
        public void AcquireLockAndInvokeAction_LockObjectsAlreadyExists()
        {
            StockPriceGridItem stockPrice1 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), canonCompany, 4440);
            StockPriceGridItem stockPrice2 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"), hitachiCompany, 4732);
            StockPriceGridItem stockPrice3 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridItem stockPrice4 = new(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridItem stockPrice5 = new(calibratedTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-22"), canonCompany, 4440);
            StockPriceGridItemGridLockKey stockPriceGridLockKey1 = new(stockPrice1);
            StockPriceGridItemGridLockKey stockPriceGridLockKey2 = new(stockPrice2);
            StockPriceGridItemGridLockKey stockPriceGridLockKey3 = new(stockPrice3);
            StockPriceGridItemGridLockKey stockPriceGridLockKey4 = new(stockPrice4);
            StockPriceGridItemGridLockKey stockPriceGridLockKey5 = new(stockPrice5);
            // Call AcquireLockAndInvokeAction() with each GridLockKey once to create the lock objects
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey1, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey2, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey3, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey4, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey5, () => { });
            List<String> writeLog = new();
            using (AutoResetEvent completeSignal = new(false))
            {
                Thread thread1 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey1, () =>
                    {
                        Thread.Sleep(300);
                        writeLog.Add(nameof(stockPriceGridLockKey1));
                    });
                });
                Thread thread2 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey2, () =>
                    {
                        writeLog.Add(nameof(stockPriceGridLockKey2));
                        completeSignal.Set();
                    });
                });
                Thread thread3 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey3, () => { writeLog.Add(nameof(stockPriceGridLockKey3)); });
                });
                Thread thread4 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey4, () => { writeLog.Add(nameof(stockPriceGridLockKey4)); });
                });
                Thread thread5 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(stockPriceGridLockKey5, () => { writeLog.Add(nameof(stockPriceGridLockKey5)); });
                });

                thread1.Start();
                Thread.Sleep(50);
                thread2.Start();
                Thread.Sleep(50);
                thread3.Start();
                Thread.Sleep(50);
                thread4.Start();
                Thread.Sleep(50);
                thread5.Start();

                completeSignal.WaitOne();
                Assert.That(writeLog.Count == 5);
                Assert.That(writeLog[0] == nameof(stockPriceGridLockKey3));
                Assert.That(writeLog[1] == nameof(stockPriceGridLockKey4));
                Assert.That(writeLog[2] == nameof(stockPriceGridLockKey5));
                Assert.That(writeLog[3] == nameof(stockPriceGridLockKey1));
                Assert.That(writeLog[4] == nameof(stockPriceGridLockKey2));
            }
        }
    }
}
