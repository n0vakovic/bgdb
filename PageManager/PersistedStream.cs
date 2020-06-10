﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace PageManager
{
    public interface IPersistedStream : IDisposable
    {
        public string GetFileName();
        public ulong CurrentFileSize();
        public void Grow(ulong newSize);
        public void Shrink(ulong newSize);
        public Task SeekAndWrite(ulong position, IPage page);
        public Task<IPage> SeekAndRead(ulong position, PageType pageType, ColumnType[] columnTypes);
        public bool IsInitialized();
        public void MarkInitialized();
    }

    public class PersistedStream : IPersistedStream
    {
        private string fileName;

        private FileStream fileStream;
        private BinaryWriter binaryWriter;
        private BinaryReader binaryReader;
        private bool isInitialized;

        public PersistedStream(ulong startFileSize, string fileName, bool createNew)
        {
            if (File.Exists(fileName) && !createNew)
            {
                this.fileStream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
                isInitialized = true;
            }
            else
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                this.fileStream = new FileStream(fileName, FileMode.CreateNew, FileAccess.ReadWrite);
                this.fileStream.SetLength((long)startFileSize);
                isInitialized = false;
            }

            this.binaryWriter = new BinaryWriter(this.fileStream);
            this.binaryReader = new BinaryReader(this.fileStream);

            this.fileName = fileName;
        }

        public ulong CurrentFileSize() => (ulong)this.fileStream.Length;

        public string GetFileName() => this.fileName;

        public void Grow(ulong newSize)
        {
            if ((ulong)this.fileStream.Length > newSize)
            {
                throw new ArgumentException();
            }

            this.fileStream.SetLength((long)newSize);
        }

        public void Shrink(ulong newSize)
        {
            if ((ulong)this.fileStream.Length < newSize)
            {
                throw new ArgumentException();
            }

            this.fileStream.SetLength((long)newSize);
        }

        public async Task SeekAndWrite(ulong position, IPage page)
        {
            this.fileStream.Seek((long)position, SeekOrigin.Begin);
            page.Persist(this.binaryWriter);
            await this.fileStream.FlushAsync();
        }

        public async Task<IPage> SeekAndRead(ulong position, PageType pageType, ColumnType[] columnTypes)
        {
            this.fileStream.Seek((long)position, SeekOrigin.Begin);

            if (pageType == PageType.DoublePage)
            {
                return new DoubleOnlyPage(this.binaryReader);
            }
            else if (pageType == PageType.IntPage)
            {
                return new IntegerOnlyPage(this.binaryReader);
            }
            else if (pageType == PageType.LongPage)
            {
                return new LongOnlyPage(this.binaryReader);
            }
            else if (pageType == PageType.MixedPage)
            {
                return new MixedPage(this.binaryReader, columnTypes);
            }
            else if (pageType == PageType.StringPage)
            {
                return new StringOnlyPage(this.binaryReader);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public void Dispose()
        {
            this.fileStream.Dispose();
            this.binaryReader.Dispose();
        }

        public bool IsInitialized() => this.isInitialized;

        public void MarkInitialized()
        {
            this.isInitialized = true;
        }
    }
}
