// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ArchiveLib.Constants;

namespace ArchiveLib.Structures
{
    public class FileEntry
    {
        public uint Index            { get; set; }
        public string Name           { get; set; }
        public FileOptions Option    { get; set; }
        public uint UncompressedSize { get; set; }
        public uint CompressedSize   { get; set; }
        public byte[] Sha1           { get; set; }
        public ulong ArchiveOffset   { get; set; }

        public override string ToString()
        {
            return $"Name: '{Name}', CS: '{CompressedSize}', US: '{UncompressedSize}', Offset: '0x{ArchiveOffset:X}'";
        }
    }
}
