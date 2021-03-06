﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using MBW.Tools.RabbitDump.Options;
using MBW.Tools.RabbitDump.Utilities;
using Microsoft.Extensions.Logging;

namespace MBW.Tools.RabbitDump.Movers.Zip
{
    class ZipSource : ZipBase, ISource
    {
        private readonly ArgumentsModel _model;
        private readonly ILogger<ZipSource> _logger;

        public ZipSource(ArgumentsModel model, ILogger<ZipSource> logger)
        {
            _model = model;
            _logger = logger;
        }

        public void SendData(ITargetBlock<MessageItem> target, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Reading zip file at {File}", _model.Input);

            using (FileStream zipFs = File.OpenRead(_model.Input))
            using (ZipArchive zip = new ZipArchive(zipFs, ZipArchiveMode.Read))
            using (MemoryStream msBuffer = new MemoryStream())
            {
                _logger.LogDebug("Reading {FilesCount} files ({Count} messages) from zip file", zip.Entries.Count,
                    zip.Entries.Count / 2);

                var entries = zip.Entries
                    .Where(s => s.Name.EndsWith(DataExtension, StringComparison.Ordinal))
                    .Select(s => s.FullName)
                    .OrderBy(s => s);

                foreach (string name in entries)
                {
                    ZipArchiveEntry entry = zip.GetEntry(name);

                    byte[] data;
                    using (Stream fs = entry.Open())
                    {
                        fs.CopyTo(msBuffer);
                        data = msBuffer.ToArray();
                        msBuffer.SetLength(0);
                    }

                    ZipArchiveEntry metaEntry = zip.GetEntry(Path.ChangeExtension(entry.FullName, MetaExtension));

                    MessageItem mi;
                    using (Stream fs = metaEntry.Open())
                        mi = Serialization.Deserialize<MessageItem>(fs);

                    mi.Data = data;

                    target.Post(mi);
                }
            }
        }

        public void Acknowledge(ICollection<MessageItem> items)
        {
        }
    }
}