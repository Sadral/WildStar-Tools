// Copyright (c) Arctium.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ArchiveLib.IO;
using System;
using System.Linq;

namespace Arctium.WildStar.DataExtractor
{
    class Program
    {
        static ArchiveManager archiveManager;

        static void Main(string[] args)
        {
            Console.WriteLine("WildStar Client Database Extractor");

            Console.WriteLine();

            Console.WriteLine("Loading archive info...");

            archiveManager = new ArchiveManager(Environment.CurrentDirectory + "/", "Patch/ClientData.index");

            Console.WriteLine("Done.");
            Console.WriteLine();

            // Extract .tbl files
            ExtractClientTables();
            
            Console.ReadKey();
        }

        static void ExtractClientTables()
        {
            Console.WriteLine("Loading '.tbl' files...");

            // Get all .tbl files.
            var tbl = archiveManager.GetFiles(".tbl", 0x4454424C);

            Console.WriteLine("Done.");

            Console.WriteLine();
            Console.WriteLine("Writing '.tbl' files to 'ClientDB' folder...");

            System.IO.Directory.CreateDirectory("./ClientDB/");

            tbl.ToList().ForEach(f => System.IO.File.WriteAllBytes($"./ClientDB/{f.Key}", f.Value));

            Console.WriteLine($"Extracted {tbl.Count} files.");
        }
    }
}
