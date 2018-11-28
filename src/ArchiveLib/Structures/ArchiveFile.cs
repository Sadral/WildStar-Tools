// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ArchiveLib.Structures
{
    class ArchiveFile
    {
        public string Signature         { get; set; }
        public uint Version             { get; set; }
        public ulong FileSize           { get; set; }
        public ulong FileDataInfoOffset { get; set; }
        public ulong FileDataInfoCount  { get; set; }

        public List<FileDataInfoEntry> FileDataInfo { get; set; }
    }
}
