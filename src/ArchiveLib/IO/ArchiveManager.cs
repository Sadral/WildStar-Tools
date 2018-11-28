// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ArchiveLib.Misc;
using ArchiveLib.Structures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

namespace ArchiveLib.IO
{
    public class ArchiveManager
    {
        public byte[] this[string fileName]
        {
            get
            {
                var entry = files.SingleOrDefault(b => b.Value.Name == fileName).Value;

                if (entry == null)
                    return new byte[0];

                archiveFileReader.BaseStream.Position = (long)entry.ArchiveOffset;

                return archiveFileReader.ReadBytes((int)entry.CompressedSize);
            }
        }

        ConcurrentDictionary<byte[], FileEntry> files { get; set; }
        Dictionary<uint, Tuple<SortedDictionary<uint, FolderEntry>, SortedDictionary<uint, FileEntry>>> folders;

        static string baseUrl = "http://wildstar.patcher.ncsoft.com/";
        static string versionUrl = string.Format("{0}version.txt", baseUrl);
        static string version = "";

        IndexFile indexFile;
        ArchiveFile archiveFile;
        BinaryReader indexFileReader, archiveFileReader;
        WebClient webclient;

        public ArchiveManager(string basePath, string indexFilePath, bool hasArchiveFile = true)
        {
            webclient = new WebClient();

            try
            {
                version = webclient.DownloadString(versionUrl);
            }
            catch
            {
                webclient = null;
            }

            var fileInfo = new FileInfo(basePath + indexFilePath);

            indexFileReader = new BinaryReader(new MemoryStream(File.ReadAllBytes(basePath + indexFilePath)));

            ReadIndexFile();

            if (hasArchiveFile)
            {
                archiveFileReader = new BinaryReader(new FileStream($"{fileInfo.Directory.FullName}\\{fileInfo.Name.Replace(".index", "")}.archive", FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true));

                ReadArchiveFile();
            }
        }

        public ArchiveManager(byte[] indexData)
        {
            indexFileReader = new BinaryReader(new MemoryStream(indexData));

            ReadIndexFile();
        }

        public void ReadIndexFile()
        {
            files = new ConcurrentDictionary<byte[], FileEntry>(new ByteArrayComparer());

            indexFile = new IndexFile
            {
                Signature = indexFileReader.ReadFourCC(),
                Version = indexFileReader.ReadUInt32(),
            };

            indexFileReader.Skip(512);

            indexFile.FileSize = indexFileReader.ReadUInt32();

            indexFileReader.Skip(12);

            indexFile.FileSystemInfo = indexFileReader.ReadUInt32();

            // Set to XDIA start.
            indexFileReader.BaseStream.Position = 0x240;

            indexFile.AIDX = new ArchiveIndex
            {
                Signature = indexFileReader.ReadFourCC(),
                Version = indexFileReader.ReadUInt32(),
                GameBuild = indexFileReader.ReadUInt32()
            };

            indexFileReader.BaseStream.Position = indexFile.FileSystemInfo - 8;

            var aidxEntryCount = indexFileReader.ReadInt32() / 16;

            indexFileReader.Skip(4 + 16);

            indexFile.AIDX.Entries = new List<ArchiveIndexEntry>(aidxEntryCount - 1);

            folders = new Dictionary<uint, Tuple<SortedDictionary<uint, FolderEntry>, SortedDictionary<uint, FileEntry>>>();

            for (var i = 0; i < aidxEntryCount - 1; i++)
            {
                var entryOffset = indexFileReader.ReadInt64();
                var entrySize = indexFileReader.ReadInt64();
                var nextEntryOffset = indexFileReader.BaseStream.Position;

                // Skip AIDX start.
                if (entryOffset <= 0x240 || entrySize == 0 || entryOffset == (aidxEntryCount * 16))
                    continue;

                indexFileReader.BaseStream.Position = entryOffset;

                var aidxEntry = new ArchiveIndexEntry
                {
                    FolderEntryCount = indexFileReader.ReadUInt32(),
                    FileEntryCount = indexFileReader.ReadUInt32()
                };

                aidxEntry.Folders = new Dictionary<uint, FolderEntry>((int)aidxEntry.FolderEntryCount);
                aidxEntry.Files = new List<FileEntry>((int)aidxEntry.FileEntryCount);

                var lastStringLength = 0;
                var folderStringEnd = 0L;

                var fList = new SortedDictionary<uint, FolderEntry>();

                for (var j = 0; j < aidxEntry.FolderEntryCount; j++)
                {
                    var folderEntry = new FolderEntry();

                    folderEntry.LowestLevel = indexFileReader.ReadUInt32();
                    folderEntry.Level = indexFileReader.ReadUInt32();

                    var position = indexFileReader.BaseStream.Position;

                    indexFileReader.BaseStream.Position = entryOffset + 8 + ((aidxEntry.FolderEntryCount * 8) + (aidxEntry.FileEntryCount * 56) + lastStringLength);

                    folderEntry.Name = indexFileReader.ReadCString();

                    lastStringLength += folderEntry.Name.Length + 1;
                    folderStringEnd = indexFileReader.BaseStream.Position;

                    indexFileReader.BaseStream.Position = position;

                    fList.Add(folderEntry.Level, folderEntry);

                    aidxEntry.Folders.Add(folderEntry.Level, folderEntry);
                }

                var fList2 = new SortedDictionary<uint, FileEntry>();

                for (var j = 0; j < aidxEntry.FileEntryCount; j++)
                {
                    var fileEntry = new FileEntry();

                    fileEntry.Index = indexFileReader.ReadUInt32();
                    fileEntry.Option = (Constants.FileOptions)indexFileReader.ReadUInt32();

                    indexFileReader.ReadUInt32();
                    indexFileReader.ReadUInt32();

                    fileEntry.UncompressedSize = indexFileReader.ReadUInt32();

                    indexFileReader.ReadUInt32();

                    fileEntry.CompressedSize = indexFileReader.ReadUInt32();

                    indexFileReader.ReadUInt32();

                    fileEntry.Sha1 = indexFileReader.ReadBytes(20);
                    indexFileReader.ReadUInt32();

                    var position = indexFileReader.BaseStream.Position;

                    indexFileReader.BaseStream.Position = entryOffset + 8 + ((aidxEntry.FolderEntryCount * 8) + (aidxEntry.FileEntryCount * 56) + lastStringLength);

                    fileEntry.Name = indexFileReader.ReadCString();

                    lastStringLength += fileEntry.Name.Length + 1;

                    indexFileReader.BaseStream.Position = position;

                    files.TryAdd(fileEntry.Sha1, fileEntry);

                    fList2.Add(fileEntry.Index, fileEntry);
                }

                if (fList.Count > 0 || fList2.Count > 0)
                    folders.Add((uint)i, Tuple.Create(fList, fList2));

                indexFileReader.BaseStream.Position = nextEntryOffset;
            }
        }

        public void ReadArchiveFile()
        {
            archiveFile = new ArchiveFile
            {
                Signature = archiveFileReader.ReadFourCC(),
                Version = archiveFileReader.ReadUInt32(),
            };

            archiveFileReader.Skip(512);

            archiveFile.FileSize = archiveFileReader.ReadUInt32();

            archiveFileReader.Skip(12);

            archiveFile.FileDataInfoOffset = archiveFileReader.ReadUInt64();
            archiveFile.FileDataInfoCount = archiveFileReader.ReadUInt64() - 3;

            // Set to AARC start.
            archiveFileReader.BaseStream.Position = 0x270 + 8;

            var fileCount = archiveFileReader.ReadUInt32();

            archiveFileReader.BaseStream.Position = (long)archiveFile.FileDataInfoOffset + 32;

            var position = archiveFileReader.BaseStream.Position;

            archiveFileReader.BaseStream.Position = archiveFileReader.ReadInt64();

            archiveFile.FileDataInfo = new List<FileDataInfoEntry>((int)archiveFile.FileDataInfoCount);

            for (var i = 0u; i < archiveFile.FileDataInfoCount; i++)
            {
                var fdie = new FileDataInfoEntry
                {
                    Index = archiveFileReader.ReadUInt32(),
                    Hash = archiveFileReader.ReadBytes(20),
                    Size = archiveFileReader.ReadUInt64()
                };

                archiveFile.FileDataInfo.Add(fdie);
            }

            archiveFileReader.BaseStream.Position = position + 16;

            var offsetMapStart = archiveFileReader.BaseStream.Position;

            for (var i = 0; i < (long)archiveFile.FileDataInfoCount; i++)
            {
                FileEntry entry;

                if (files.TryGetValue(archiveFile.FileDataInfo[i].Hash, out entry))
                {
                    archiveFileReader.BaseStream.Position = offsetMapStart + archiveFile.FileDataInfo[i].Index * 16 - 0x30;

                    files[archiveFile.FileDataInfo[i].Hash].ArchiveOffset = archiveFileReader.ReadUInt64();

                    archiveFileReader.Skip(8);
                }
            }
        }

        public Dictionary<string, byte[]> GetFiles(string fileExtension, uint magic = 0)
        {
            var entries = files.Where(b => b.Value.Name.EndsWith(fileExtension)).ToList();
            var dataList = new Dictionary<string, byte[]>(entries.Count);

            foreach (var entry in entries)
            {
                archiveFileReader.BaseStream.Position = (long)entry.Value.ArchiveOffset;

                var data = archiveFileReader.ReadBytes((int)entry.Value.CompressedSize);

                if (entry.Value.CompressedSize != entry.Value.UncompressedSize)
                {
                    if (entry.Value.Option == Constants.FileOptions.ZLibCompressed)
                    {
                        var dData = DecompressZlib(data, (int)entry.Value.UncompressedSize);

                        if (dData == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: '{0}'.", entry.Value.Name);

                            dData = DownloadFile(entry, magic);
                        }
                        else
                        {
                            var rMagic = BitConverter.ToUInt32(dData, 0);

                            if (rMagic != magic)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Something is not right here!!! Magic = '{0}'.", rMagic);

                                dData = DownloadFile(entry, magic);
                            }
                        }

                        if (dData == null)
                            continue;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Successfully extracted '{0}'", entry.Value.Name);
                        Console.ForegroundColor = ConsoleColor.Gray;

                        dataList.Add(entry.Value.Name, dData);
                    }

                    if (entry.Value.Option == Constants.FileOptions.LzmaCompressed)
                    {
                        var dData = DecompressLzma(data, (int)entry.Value.UncompressedSize);

                        if (dData == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: '{0}'.", entry.Value.Name);

                            dData = DownloadFile(entry, magic);
                        }
                        else
                        {
                            var rMagic = BitConverter.ToUInt32(dData, 0);

                            if (rMagic != magic)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Something is not right here!!! Magic = '{0}'.", rMagic);

                                dData = DownloadFile(entry, magic);
                            }
                        }

                        if (dData == null)
                            continue;

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Successfully extracted '{0}'", entry.Value.Name);
                        Console.ForegroundColor = ConsoleColor.Gray;

                        dataList.Add(entry.Value.Name, dData);
                    }
                }

                else
                    dataList.Add(entry.Value.Name, data);
            }

            return dataList;
        }

        byte[] DownloadFile(KeyValuePair<byte[], FileEntry> entry, uint magic)
        {
            if (webclient != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine("Trying to downloading it...");

                var dData = DecompressLzma(webclient.DownloadData(string.Format("{0}{1}/{2}.bin", baseUrl, version, entry.Key.ToHexString().ToLower())), (int)entry.Value.UncompressedSize);

                if (dData == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error while downloading.\nPlease re-download the game or turn on your internet connection!");

                    return null;
                }
                else
                {
                    var rMagic = BitConverter.ToUInt32(dData, 0);

                    if (rMagic != magic)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Something is not right here!!! Magic = '{0}'.", rMagic);

                        return null;
                    }
                }

                return dData;
            }

            return null;
        }

        public byte[] DecompressLzma(byte[] data, int decompressedSize)
        {
            try
            {
                var unpackedData = new byte[decompressedSize];

                var hash = SHA1.Create().ComputeHash(data);

                SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();

                var decompressed = new MemoryStream();

                using (var compressed = new MemoryStream(data))
                {


                    var props = new byte[5];

                    compressed.Read(props, 0, 5);
                    decoder.SetDecoderProperties(props);
                    decoder.Code(compressed, decompressed, data.Length, decompressedSize, null);
                }

                return decompressed.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public byte[] DecompressZlib(byte[] data, int decompressedSize)
        {
            try
            {
                var unpackedData = new byte[decompressedSize];

                using (var inflate = new DeflateStream(new MemoryStream(data, 2, data.Length - 2), CompressionMode.Decompress))
                {
                    var decompressed = new MemoryStream();
                    inflate.CopyTo(decompressed);

                    decompressed.Seek(0, SeekOrigin.Begin);

                    for (int i = 0; i < decompressedSize; i++)
                        unpackedData[i] = (byte)decompressed.ReadByte();
                }

                return unpackedData;
            }
            catch
            {
                return null;
            }
        }
    }
}
