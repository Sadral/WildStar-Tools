// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Text;

namespace ArchiveLib.Misc
{
    public static class Extensions
    {
        public static string ReadFourCC(this BinaryReader br)
        {
            return Encoding.ASCII.GetString(br.ReadBytes(4).Reverse().ToArray());
        }

        public static string ToHexString(this byte[] data)
        {
            var hex = "";

            foreach (var b in data)
                hex += $"{b:X2}";

            return hex.ToUpper();
        }

        public static string ReadCString(this BinaryReader br)
        {
            var ret = "";

            while (true)
            {
                var nextChar = br.ReadChar();

                if (nextChar == 0)
                    break;

                ret += nextChar;
            }

            return ret;
        }

        public static bool Compare(this byte[] b, byte[] b2)
        {
            for (int i = 0; i < b2.Length; i++)
                if (b[i] != b2[i])
                    return false;

            return true;
        }

        public static void Skip(this BinaryReader br, int count) => br.BaseStream.Position += count;
    }
}
