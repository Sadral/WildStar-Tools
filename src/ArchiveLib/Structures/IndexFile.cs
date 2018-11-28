// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ArchiveLib.Structures
{
    class IndexFile
    {
        public string Signature    { get; set; }
        public uint Version        { get; set; }
        public uint FileSize       { get; set; }
        public uint FileSystemInfo { get; set; } 

        public ArchiveIndex AIDX { get; set; }
    }
}
