﻿using LogManager;
using PageManager;
using System;
using System.Collections.Generic;

namespace MetadataManager
{
    public class MetadataManager
    {
        private IAllocateMixedPage pageAllocator;

        private readonly ColumnType[] masterPageColumnDefinition = new ColumnType[]
        {
            // MD object id.
            ColumnType.Int,
            // Pointer to first page.
            ColumnType.PagePointer,
        };

        private PageListCollection masterMetadataCollection;
        private HeapWithOffsets<char[]> stringHeap;

        private MetadataColumnsManager columnsManager;
        private MetadataTablesManager tableManager;

        private LogManager.ILogManager logManager;

        public MetadataManager(IAllocateMixedPage pageAllocator, HeapWithOffsets<char[]> stringHeap, IBootPageAllocator bootPageAllocator, ILogManager logManager)
        {
            this.pageAllocator = pageAllocator;
            this.stringHeap = stringHeap;
            this.logManager = logManager;

            if (!bootPageAllocator.BootPageInitialized())
            {
                ITransaction tran = new Transaction(logManager, "SETUP_BOOT_PAGE");
                bootPageAllocator.AllocatePageBootPage(PageType.MixedPage, this.masterPageColumnDefinition, tran);

                this.masterMetadataCollection = new PageListCollection(this.pageAllocator, this.masterPageColumnDefinition, this.pageAllocator.GetMixedPage(IBootPageAllocator.BootPageId, tran));
                tran.Commit();

                MetadataInitialSetup();
            }
            else
            {
                ITransaction tran = new Transaction(logManager, "GET_BOOT_PAGE");
                this.masterMetadataCollection = new PageListCollection(this.pageAllocator, this.masterPageColumnDefinition, this.pageAllocator.GetMixedPage(IBootPageAllocator.BootPageId, tran));
                tran.Commit();
            }
        }

        private void MetadataInitialSetup()
        {
            ITransaction tran = new Transaction(logManager, "MetadataSetup");
            RowsetHolder rh = new RowsetHolder(this.masterPageColumnDefinition);

            var mdColumnsFirstPage = this.pageAllocator.AllocateMixedPage(MetadataColumnsManager.GetSchemaDefinition(), 0, 0, tran);
            this.columnsManager = new MetadataColumnsManager(this.pageAllocator, mdColumnsFirstPage, this.stringHeap);

            var mdTableFirstPage = this.pageAllocator.AllocateMixedPage(MetadataTablesManager.GetSchemaDefinition(), 0, 0, tran);
            this.tableManager = new MetadataTablesManager(this.pageAllocator, mdTableFirstPage, this.stringHeap, this.columnsManager);

            rh.SetColumns(
                new int[1][] { new int[] 
                { 
                    (int)MetadataObjectEnum.MdTableId,
                    (int)MetadataObjectEnum.MdColumnId,
                }},
                new double[0][], new PagePointerOffsetPair[0][],
                new long[1][] { new long[] 
                { 
                    (long)mdTableFirstPage.PageId(),
                    (long)mdColumnsFirstPage.PageId() 
                }});
            masterMetadataCollection.Add(rh, tran);

            tran.Commit();
        }

        public MetadataTablesManager GetTableManager() => this.tableManager;

        public MetadataColumnsManager GetColumnManager() => this.columnsManager;
    }
}
