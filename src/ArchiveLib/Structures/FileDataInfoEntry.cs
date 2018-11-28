// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ArchiveLib.Structures
{
    class FileDataInfoEntry
    {
        public uint Index       { get; set; }
        public byte[] Hash      { get; set; }
        public ulong Size       { get; set; }
        public ulong DataOffset { get; set; }
        public ulong DataLength { get; set; }
    }
}
