// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ArchiveLib.Structures
{
    class ArchiveIndex
    {
        public string Signature { get; set; }
        public uint Version     { get; set; }
        public uint GameBuild   { get; set; }

        public List<ArchiveIndexEntry> Entries;
    }
}
