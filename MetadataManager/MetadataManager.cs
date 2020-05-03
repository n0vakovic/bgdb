﻿using PageManager;
using System;

namespace MetadataManager
{
    public interface ITableManager
    {
        bool CreateTable(string tableName, string[] columnNames, ColumnType[] columnTypes);
    }

    public interface IMetadataObject
    {
    }


    public class MdTable
    {
        private uint tableId;
        private string tableName;
        private string[] columnNames;
        private ColumnType[] columnTypes;
        private ulong allocationMapFirstPage;
    }

    public class TableManager
    {
        private IAllocateStringPage stringPageAllocator;
        private IAllocateIntegerPage intPageAllocator;

        private const int MaxNameLength = 32;

        private const MetadataObjectEnum MetadataObjectId = MetadataObjectEnum.MdTableId;

        public TableManager(IAllocateIntegerPage intPageAllocator, IAllocateStringPage strPageAllocator)
        {
            this.stringPageAllocator = strPageAllocator;
            this.intPageAllocator = intPageAllocator;
        }

        bool CreateTable(string tableName, string[] columnNames, ColumnType[] columnTypes)
        {
            if (tableName.Length > MaxNameLength)
            {
                throw new NameTooLongException();
            }

            if (tableName == null || columnNames.Length < 1 || columnNames.Length != columnTypes.Length)
            {
                throw new ArgumentException();
            }

            // Calc size for rows.

            return false;
        }
    }

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

        public MetadataManager(IAllocateMixedPage pageAllocator, bool useExistingMasterPage)
        {
            this.pageAllocator = pageAllocator;

            if (!useExistingMasterPage)
            {
                var page = pageAllocator.AllocateMixedPage(masterPageColumnDefinition, prevPage: 0, nextPage: 0);
                if (page.PageId() != 0)
                {
                    throw new InvalidOperationException("mixed page allocator should be empty");
                }
            }
        }
    }
}