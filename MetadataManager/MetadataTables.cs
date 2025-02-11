﻿using DataStructures;
using PageManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MetadataManager
{
    public struct MetadataTable
    {
        public const int TableIdColumnPos = 0;
        public int TableId;
        public const int TableNameColumnPos = 1;
        public string TableName;
        public const int RootPageColumnPos = 2;
        public ulong RootPage;
        public MetadataColumn[] Columns;

        // Virtual fields.
        public IPageCollection<RowHolder> Collection;
    }

    public struct TableCreateDefinition
    {
        public string TableName;
        public string[] ColumnNames;
        public ColumnInfo[] ColumnTypes;
    }

    public class MetadataTablesManager : IMetadataObjectManager<MetadataTable, TableCreateDefinition, int>
    {
        public const string MetadataTableName = "sys.tables";

        private IPageCollection<RowHolder> pageListCollection;
        private HeapWithOffsets<char[]> stringHeap;
        private IMetadataObjectManager<MetadataColumn, ColumnCreateDefinition, Tuple<int, int>> columnManager;
        private IAllocateMixedPage pageAllocator;

        private static ColumnInfo[] columnDefinitions = new ColumnInfo[]
        {
            new ColumnInfo(ColumnType.Int),
            new ColumnInfo(ColumnType.String, 20),
            new ColumnInfo(ColumnType.PagePointer),
        };

        private object cacheLock = new object();
        private Dictionary<string, MetadataTable> nameTableCache = new Dictionary<string, MetadataTable>();

        public static ColumnInfo[] GetSchemaDefinition() => columnDefinitions;

        public MetadataTablesManager(IAllocateMixedPage pageAllocator, MixedPage firstPage, HeapWithOffsets<char[]> stringHeap, IMetadataObjectManager<MetadataColumn, ColumnCreateDefinition, Tuple<int, int>> columnManager)
        {
            if (pageAllocator == null || firstPage == null || columnManager == null)
            {
                throw new ArgumentNullException();
            }

            this.pageListCollection = new PageListCollection(pageAllocator, columnDefinitions, firstPage.PageId());
            this.stringHeap = stringHeap;
            this.columnManager = columnManager;
            this.pageAllocator = pageAllocator;
        }

        public async Task<bool> Exists(TableCreateDefinition def, ITransaction tran)
        {
            await foreach (RowHolder rh in pageListCollection.Iterate(tran))
            {
                PagePointerOffsetPair stringPointer = rh.GetField<PagePointerOffsetPair>(1);

                if (def.TableName == new string(await stringHeap.Fetch(stringPointer, tran)))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<int> CreateObject(TableCreateDefinition def, ITransaction tran)
        {
            if (await this.Exists(def, tran))
            {
                throw new ElementWithSameNameExistsException();
            }

            if (def.ColumnNames.Length != def.ColumnTypes.Length)
            {
                throw new ArgumentException();
            }

            int id = 1;
            if (!(await pageListCollection.IsEmpty(tran)))
            {
                int maxId = await pageListCollection.Max<int>(rh => rh.GetField<int>(0), startMin: 0, tran);
                id = maxId + 1;
            }

            MixedPage rootPage = await this.pageAllocator.AllocateMixedPage(def.ColumnTypes, PageManagerConstants.NullPageId, PageManagerConstants.NullPageId, tran);

            RowHolder rh = new RowHolder(columnDefinitions);
            PagePointerOffsetPair namePointer =  await this.stringHeap.Add(def.TableName.ToCharArray(), tran);

            rh.SetField<int>(0, id);
            rh.SetField<PagePointerOffsetPair>(1, namePointer);
            rh.SetField<long>(2, (long)rootPage.PageId());
            await pageListCollection.Add(rh, tran);

            for (int i = 0; i < def.ColumnNames.Length; i++)
            {
                ColumnCreateDefinition ccd = new ColumnCreateDefinition(id, def.ColumnNames[i], def.ColumnTypes[i], i);
                await columnManager.CreateObject(ccd, tran);
            }

            return id;
        }

        public async IAsyncEnumerable<MetadataTable> Iterate(ITransaction tran)
        {
            await foreach (RowHolder rh in pageListCollection.Iterate(tran))
            {
                var mdObj = 
                    new MetadataTable()
                    {
                        TableId = rh.GetField<int>(MetadataTable.TableIdColumnPos),
                        RootPage = (ulong)rh.GetField<long>(MetadataTable.RootPageColumnPos),
                    };

                PagePointerOffsetPair stringPointer = rh.GetField<PagePointerOffsetPair>(MetadataTable.TableNameColumnPos);
                char[] tableName= await this.stringHeap.Fetch(stringPointer, tran);

                mdObj.TableName = new string(tableName);

                List<MetadataColumn> columns = new List<MetadataColumn>();
                await foreach (var column in this.columnManager.Iterate(tran))
                {
                    if (column.TableId == mdObj.TableId)
                    {
                        columns.Add(column);
                    }
                }

                mdObj.Columns = columns.ToArray();

                mdObj.Collection = new PageListCollection(this.pageAllocator, mdObj.Columns.Select(ci => ci.ColumnType).ToArray(), mdObj.RootPage);

                yield return mdObj;
            }
        }

        public async Task<MetadataTable> GetById(int id, ITransaction tran)
        {
            await foreach (var table in this.Iterate(tran))
            {
                if (table.TableId == id)
                {
                    return table;
                }
            }

            throw new KeyNotFoundException();
        }

        public async Task<MetadataTable> GetByName(string name, ITransaction tran)
        {
            lock (this.cacheLock)
            {
                MetadataTable md;
                if (this.nameTableCache.TryGetValue(name, out md))
                {
                    return md;
                }
            }

            await foreach (var table in this.Iterate(tran))
            {
                lock (this.cacheLock)
                {
                    this.nameTableCache[table.TableName] = table;
                }

                if (table.TableName == name)
                {
                    return table;
                }
            }

            throw new KeyNotFoundException();
        }
    }
}
