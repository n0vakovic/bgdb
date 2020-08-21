﻿using LockManager;
using LogManager;
using NUnit.Framework;
using PageManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Test.Common;

namespace PageManagerTests
{
    public class ConcurrentPageAccessTests
    {
        private const int DefaultSize = 4096;
        private const ulong DefaultPrevPage = PageManagerConstants.NullPageId;
        private const ulong DefaultNextPage = PageManagerConstants.NullPageId;

        [Test]
        [Repeat(10)]
        public async Task ConcurrentWriteTests()
        {
            PersistedStream persistedStream = new PersistedStream(1024 * 1024, "concurrent.data", createNew: true);
            IBufferPool bp = new BufferPool();
            ILockManager lm = new LockManager.LockManager();
            var lgm = new LogManager.LogManager(new BinaryWriter(new MemoryStream()));
            using var pm =  new PageManager.PageManager(DefaultSize, new FifoEvictionPolicy(1, 1), persistedStream, bp, lm, TestGlobals.TestFileLogger);

            const int workerCount = 100;

            GenerateDataUtils.GenerateSampleData(out ColumnType[] types, out int[][] intColumns, out double[][] doubleColumns, out long[][] pagePointerColumns, out PagePointerOffsetPair[][] pagePointerOffsetColumns);

            async Task generatePagesAction()
            {
                for (int i = 0; i < workerCount; i++)
                {
                    using (ITransaction tran = lgm.CreateTransaction(pm))
                    {
                        var mp = await pm.AllocateMixedPage(types, DefaultPrevPage, DefaultNextPage, tran);
                        await tran.AcquireLock(mp.PageId(), LockTypeEnum.Exclusive);
                        RowsetHolder holder = new RowsetHolder(types);
                        holder.SetColumns(intColumns, doubleColumns, pagePointerOffsetColumns, pagePointerColumns);
                        mp.Merge(holder, tran);
                        await tran.Commit();
                    }
                }
            }

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < workerCount; i++)
            {
                tasks.Add(Task.Run(generatePagesAction));
            }

            await Task.WhenAll(tasks);
        }

        [Test]
        [Repeat(1)]
        public async Task ConcurrentReadAndWriteTests()
        {
            PersistedStream persistedStream = new PersistedStream(1024 * 1024, "concurrent.data", createNew: true);
            IBufferPool bp = new BufferPool();
            ILockManager lm = new LockManager.LockManager();
            var lgm = new LogManager.LogManager(new BinaryWriter(new MemoryStream()));
            using var pm =  new PageManager.PageManager(DefaultSize, new FifoEvictionPolicy(1, 1), persistedStream, bp, lm, TestGlobals.TestFileLogger);

            long maxPageId = 0;

            const int workerCount = 100;

            GenerateDataUtils.GenerateSampleData(out ColumnType[] types, out int[][] intColumns, out double[][] doubleColumns, out long[][] pagePointerColumns, out PagePointerOffsetPair[][] pagePointerOffsetColumns);

            async Task generatePagesAction()
            {
                for (int i = 0; i < workerCount; i++)
                {
                    using (ITransaction tran = lgm.CreateTransaction(pm))
                    {
                        var mp = await pm.AllocateMixedPage(types, DefaultPrevPage, DefaultNextPage, tran);
                        await tran.AcquireLock(mp.PageId(), LockTypeEnum.Exclusive);
                        RowsetHolder holder = new RowsetHolder(types);
                        holder.SetColumns(intColumns, doubleColumns, pagePointerOffsetColumns, pagePointerColumns);
                        mp.Merge(holder, tran);
                        await tran.Commit();
                        Interlocked.Exchange(ref maxPageId, (long)mp.PageId());
                    }
                }
            }

            async Task readRandomPages()
            {
                for (int i = 0; i < workerCount; i++)
                {
                    using (ITransaction tran = lgm.CreateTransaction(pm))
                    {
                        long currMaxPageId = Interlocked.Read(ref maxPageId);

                        if (maxPageId < 2)
                        {
                            continue;
                        }

                        Random rnd = new Random();
                        ulong pageToRead = (ulong)rnd.Next(2, (int)currMaxPageId);

                        using (var _ = await tran.AcquireLock(pageToRead, LockTypeEnum.Shared))
                        {
                            await pm.GetMixedPage(pageToRead, tran, types);
                        }

                        await tran.Commit();
                    }
                }
            }

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < workerCount; i++)
            {
                tasks.Add(Task.Run(generatePagesAction));
                tasks.Add(Task.Run(readRandomPages));
            }

            await Task.WhenAll(tasks);
        }
    }
}