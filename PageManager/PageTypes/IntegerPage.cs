﻿using System;
using System.IO;

namespace PageManager
{
    public interface IAllocateIntegerPage
    {
        IntegerOnlyPage AllocatePageInt(ulong prevPage, ulong nextPage);
        IntegerOnlyPage GetPageInt(ulong pageId);
    }

    public class IntegerOnlyPage : SimpleTypeOnlyPage<int>
    {
        public IntegerOnlyPage(uint pageSize, ulong pageId, ulong prevPageId, ulong nextPageId) : base(pageSize, pageId, PageManager.PageType.IntPage, prevPageId, nextPageId) { }
        public IntegerOnlyPage(BinaryReader stream) : base(stream, PageManager.PageType.IntPage) { }

        protected override void SerializeInternal(BinaryReader stream)
        {
            this.items = new int[this.rowCount];

            for (int i = 0; i < this.items.Length; i++)
            {
                this.items[i] = stream.ReadInt32();
            }
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

                for (int i = 0; i < this.rowCount; i++)
                {
                    bw.Write(this.items[i]);
                }
            }
        }
    }
}
