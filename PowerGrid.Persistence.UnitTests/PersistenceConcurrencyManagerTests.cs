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

        [TearDown]
        protected void TearDown()
        {
            testPersistenceConcurrencyManager.Dispose();
        }

        [Test]
        public void AcquireLockAndInvokeActionCommonKeyPropertiesLockOverload()
        {
            GridCommonKeyProperties commonKeyProperties1 = new(marketTag);
            GridCommonKeyProperties commonKeyProperties2 = new(calibratedTag);
            GridCommonKeyProperties commonKeyProperties3 = new(marketTag);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey1 = new(commonKeyProperties1);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey2 = new(commonKeyProperties2);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey3 = new(commonKeyProperties3);
            StockPriceGridOuterKeyProperties outerKeyProperties1 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties2 = new(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey1 = new(outerKeyProperties1);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey2 = new(outerKeyProperties2);
            List<String> writeLog = new();
            using (AutoResetEvent completeSignal = new(false))
            {
                Thread thread1 = new(() =>
                {
                    // This thread starts first and locks 'Market'
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, () =>
                    {
                        Thread.Sleep(500);
                        writeLog.Add(nameof(commonKeyPropertiesLockKey1));
                    });
                });
                Thread thread2 = new(() =>
                {
                    // This thread should be blocked by thread1 above as it locks tag 'Market'
                    // Note that even though this thread is started before thread3, this will finish last, as thread3 uses a write lock on the common key properties
                    //   whereas this thread uses a read lock.  The ReaderWriterLockSlim instances used inside PersistenceConcurrencyManager prioritze write lock
                    //   requests over read lock requests.  See https://learn.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim.enterwritelock?view=net-10.0&redirectedfrom=MSDN#remarks
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey1, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey1));
                        completeSignal.Set();
                    });
                });
                Thread thread3 = new(() =>
                {
                    // This thread should be blocked by thread1 above as it locks tag 'Market'
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey3, () =>
                    {
                        writeLog.Add(nameof(commonKeyPropertiesLockKey3));
                    });
                });
                // Below threads have no key clash with the 3 above, so all should complete before threads 1, 2, and 3
                Thread thread4 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, () =>
                    {
                        writeLog.Add(nameof(commonKeyPropertiesLockKey2));
                    });
                });
                Thread thread5 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, outerKeyPropertiesLockKey2, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey2));
                    });
                });

                thread1.Start();
                Thread.Sleep(100);
                thread2.Start();
                Thread.Sleep(100);
                thread3.Start();
                Thread.Sleep(100);
                thread4.Start();
                Thread.Sleep(100);
                thread5.Start();

                completeSignal.WaitOne();
                Assert.That(writeLog.Count == 5);
                Assert.That(writeLog[0] == nameof(commonKeyPropertiesLockKey2));
                Assert.That(writeLog[1] == nameof(outerKeyPropertiesLockKey2));
                Assert.That(writeLog[2] == nameof(commonKeyPropertiesLockKey1));
                Assert.That(writeLog[3] == nameof(commonKeyPropertiesLockKey3));
                Assert.That(writeLog[4] == nameof(outerKeyPropertiesLockKey1));
            }
        }

        [Test]
        public void AcquireLockAndInvokeActionOuterKeyPropertiesLockOverload()
        {
            GridCommonKeyProperties commonKeyProperties1 = new(marketTag);
            GridCommonKeyProperties commonKeyProperties2 = new(calibratedTag);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey1 = new(commonKeyProperties1);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey2 = new(commonKeyProperties2);
            StockPriceGridOuterKeyProperties outerKeyProperties1 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties2 = new(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties3 = new(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties4 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-22"));
            StockPriceGridOuterKeyProperties outerKeyProperties5 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey1 = new(outerKeyProperties1);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey2 = new(outerKeyProperties2);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey3 = new(outerKeyProperties3);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey4 = new(outerKeyProperties4);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey5 = new(outerKeyProperties5);
            List<String> writeLog = new();
            using (AutoResetEvent completeSignal = new(false))
            {
                Thread thread1 = new(() =>
                {
                    // This thread starts first and locks 'Market'
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey1, () =>
                    {
                        Thread.Sleep(600);
                        writeLog.Add(nameof(outerKeyPropertiesLockKey1));
                    });
                });
                Thread thread2 = new(() =>
                {
                    // This thread should be blocked by thread1 above as it locks tag 'Market'
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey1, () =>
                    {
                        writeLog.Add($"{nameof(outerKeyPropertiesLockKey1)}-2");
                        completeSignal.Set();
                    });
                });
                // Below threads have no key clash with the 2 above, so all should complete before threads 1, and 2
                Thread thread3 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, outerKeyPropertiesLockKey2 , () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey2));
                    });
                });
                Thread thread4 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey3, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey3));
                    });
                });
                Thread thread5 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey4, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey4));
                    });
                });
                Thread thread6 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, () =>
                    {
                        writeLog.Add(nameof(commonKeyPropertiesLockKey2));
                    });
                });

                thread1.Start();
                Thread.Sleep(100);
                thread2.Start();
                Thread.Sleep(100);
                thread3.Start();
                Thread.Sleep(100);
                thread4.Start();
                Thread.Sleep(100);
                thread5.Start();
                Thread.Sleep(100);
                thread6.Start();

                completeSignal.WaitOne();
                Assert.That(writeLog.Count == 6);
                Assert.That(writeLog[0] == nameof(outerKeyPropertiesLockKey2));
                Assert.That(writeLog[1] == nameof(outerKeyPropertiesLockKey3));
                Assert.That(writeLog[2] == nameof(outerKeyPropertiesLockKey4));
                Assert.That(writeLog[3] == nameof(commonKeyPropertiesLockKey2));
                Assert.That(writeLog[4] == nameof(outerKeyPropertiesLockKey1));
                Assert.That(writeLog[5] == nameof(outerKeyPropertiesLockKey1) + "-2");
            }
        }

        [Test]
        public void AcquireLockAndInvokeAction_LockObjectsAlreadyExist()
        {
            GridCommonKeyProperties commonKeyProperties1 = new(marketTag);
            GridCommonKeyProperties commonKeyProperties2 = new(calibratedTag);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey1 = new(commonKeyProperties1);
            GridCommonKeyPropertiesLockKey commonKeyPropertiesLockKey2 = new(commonKeyProperties2);
            StockPriceGridOuterKeyProperties outerKeyProperties1 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties2 = new(calibratedTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties3 = new(marketTag, refinitivDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyProperties outerKeyProperties4 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-22"));
            StockPriceGridOuterKeyProperties outerKeyProperties5 = new(marketTag, bloombergDataSource, utils.CreateDateOnlyFromString("2026-03-23"));
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey1 = new(outerKeyProperties1);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey2 = new(outerKeyProperties2);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey3 = new(outerKeyProperties3);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey4 = new(outerKeyProperties4);
            StockPriceGridOuterKeyPropertiesLockKey outerKeyPropertiesLockKey5 = new(outerKeyProperties5);
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey1, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, outerKeyPropertiesLockKey2, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey3, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey4, () => { });
            testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, () => { });
            List<String> writeLog = new();
            using (AutoResetEvent completeSignal = new(false))
            {
                Thread thread1 = new(() =>
                {
                    // This thread starts first and locks 'Market'
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey1, () =>
                    {
                        Thread.Sleep(600);
                        writeLog.Add(nameof(outerKeyPropertiesLockKey1));
                    });
                });
                Thread thread2 = new(() =>
                {
                    // This thread should be blocked by thread1 above as it locks tag 'Market'
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey1, () =>
                    {
                        writeLog.Add($"{nameof(outerKeyPropertiesLockKey1)}-2");
                        completeSignal.Set();
                    });
                });
                // Below threads have no key clash with the 2 above, so all should complete before threads 1, and 2
                Thread thread3 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, outerKeyPropertiesLockKey2, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey2));
                    });
                });
                Thread thread4 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey3, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey3));
                    });
                });
                Thread thread5 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey1, outerKeyPropertiesLockKey4, () =>
                    {
                        writeLog.Add(nameof(outerKeyPropertiesLockKey4));
                    });
                });
                Thread thread6 = new(() =>
                {
                    testPersistenceConcurrencyManager.AcquireLockAndInvokeAction(commonKeyPropertiesLockKey2, () =>
                    {
                        writeLog.Add(nameof(commonKeyPropertiesLockKey2));
                    });
                });

                thread1.Start();
                Thread.Sleep(100);
                thread2.Start();
                Thread.Sleep(100);
                thread3.Start();
                Thread.Sleep(100);
                thread4.Start();
                Thread.Sleep(100);
                thread5.Start();
                Thread.Sleep(100);
                thread6.Start();

                completeSignal.WaitOne();
                Assert.That(writeLog.Count == 6);
                Assert.That(writeLog[0] == nameof(outerKeyPropertiesLockKey2));
                Assert.That(writeLog[1] == nameof(outerKeyPropertiesLockKey3));
                Assert.That(writeLog[2] == nameof(outerKeyPropertiesLockKey4));
                Assert.That(writeLog[3] == nameof(commonKeyPropertiesLockKey2));
                Assert.That(writeLog[4] == nameof(outerKeyPropertiesLockKey1));
                Assert.That(writeLog[5] == nameof(outerKeyPropertiesLockKey1) + "-2");
            }
        }
    }
}
