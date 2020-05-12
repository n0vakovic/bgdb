﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace PageManager
{
    public interface IAllocateStringPage
    {
        StringOnlyPage AllocatePageStr(ulong prevPage, ulong nextPage);
        StringOnlyPage GetPageStr(ulong pageId);
    }

    public interface IPageWithOffsets<T>
    {
        public uint MergeWithOffsetFetch(T item);
        public T FetchWithOffset(uint offset);
        public bool CanFit(T item);
    }

    public class StringOnlyPage : PageSerializerBase<char[][]>, IPageWithOffsets<char[]>
    {
        public StringOnlyPage(uint pageSize, ulong pageId, ulong prevPageId, ulong nextPageId)
        {
            if (pageSize < IPage.FirstElementPosition + sizeof(char) * 2)
            {
                throw new ArgumentException("Size can't be less than size of char and null termination");
            }

            this.pageSize = pageSize;
            this.pageId = pageId;
            this.prevPageId = prevPageId;
            this.items = new char[0][];
        }

        public StringOnlyPage(BinaryReader stream)
        {
            this.pageId = stream.ReadUInt64();
            this.pageSize = stream.ReadUInt32();

            PageType pageTypePersisted = (PageType)stream.ReadUInt32();

            if (PageManager.PageType.StringPage != pageTypePersisted)
            {
                throw new InvalidCastException();
            }

            this.rowCount = stream.ReadUInt32();

            this.prevPageId = stream.ReadUInt64();
            this.nextPageId = stream.ReadUInt64();

            if (stream.BaseStream.Position % this.pageSize != IPage.FirstElementPosition)
            {
                throw new SerializationException();
            }

            this.items = new char[this.rowCount][];

            for (int elemCount = 0; elemCount < this.rowCount; elemCount++)
            {
                int charLength = stream.ReadInt16();
                this.items[elemCount] = stream.ReadChars(charLength);
            }
        }

        public override PageType PageType() => PageManager.PageType.StringPage;

        public override uint GetSizeNeeded(char[][] items)
        {
            uint byteCount = 0;
            foreach (char[] item in items)
            {
                byteCount += (uint)item.Length + sizeof(short);
            }

            return byteCount;
        }

        public override uint MaxRowCount()
        {
            return this.pageSize - IPage.FirstElementPosition;
        }

        public override bool CanFit(char[][] items)
        {
            uint size = this.GetSizeNeeded(this.items) + this.GetSizeNeeded(items);
            return this.pageSize - IPage.FirstElementPosition >= size;
        }

        public override void Merge(char[][] items)
        {
            uint size = this.GetSizeNeeded(this.items) + this.GetSizeNeeded(items);
            if (this.pageSize - IPage.FirstElementPosition < size)
            {
                throw new NotEnoughSpaceException();
            }

            this.items = this.items.Concat(items).ToArray();
            this.rowCount = (uint)this.items.Length;
        }

        public uint MergeWithOffsetFetch(char[] item)
        {
            uint size = this.GetSizeNeeded(this.items) + (uint)item.Length + sizeof(short);

            if (this.pageSize - IPage.FirstElementPosition < size)
            {
                throw new NotEnoughSpaceException();
            }

            uint positionInBuffer = IPage.FirstElementPosition;
            foreach (var arr in this.items)
            {
                positionInBuffer += (uint)arr.Length + sizeof(short);
            }

            this.rowCount++;
            this.items = this.items.Append(item).ToArray();

            return positionInBuffer;
        }

        public char[] FetchWithOffset(uint offset)
        {
            if (offset < IPage.FirstElementPosition || offset >= this.pageSize)
            {
                throw new ArgumentException();
            }

            uint currOfset = IPage.FirstElementPosition;
            foreach (char[] item in this.items)
            {
                if (currOfset == offset)
                {
                    return item;
                }
                else
                {
                    currOfset += (uint)item.Length + sizeof(short);
                }
            }

            throw new PageCorruptedException();
        }

        public override void Store(char[][] items)
        {
            if (!CanFit(items))
            {
                throw new NotEnoughSpaceException();
            }

            this.items = items;
            this.rowCount = (uint)items.Length;
        }

        public override void Persist(Stream destination)
        {
            using (BinaryWriter bw = new BinaryWriter(destination))
            {
                bw.Write(this.pageId);
                bw.Write(this.pageSize);
                bw.Write((int)this.PageType());
                bw.Write(this.rowCount);
                bw.Write(this.prevPageId);
                bw.Write(this.nextPageId);

                foreach (char[] item in this.items)
                {
                    bw.Write((short)item.Length);
                    bw.Write(item);
                }
            }
        }

        public override char[][] Fetch() => this.items;

        public bool CanFit(char[] item)
        {
            uint size = this.GetSizeNeeded(this.items) + (uint)item.Length + sizeof(short);
            return this.pageSize - IPage.FirstElementPosition >= size;
        }
    }
}
