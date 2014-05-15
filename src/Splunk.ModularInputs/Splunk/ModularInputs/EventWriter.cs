﻿/*
 * Copyright 2014 Splunk, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"): you may
 * not use this file except in compliance with the License. You may obtain
 * a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */

namespace Splunk.ModularInputs
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Serialization;

    /// <summary>
    /// 
    /// </summary>
    public class EventWriter : IDisposable
    {
        private static XmlSerializer serializer = new XmlSerializer(typeof(Event));

        public event Action EventWritten;

        private BlockingCollection<Event> eventQueue;
        private XmlWriter writer;
        private Task eventQueueMonitor = null;
        private TextWriter stderr;
        private TextWriter stdout;

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public EventWriter() : this(Console.Out, Console.Error) {}

        public EventWriter(TextWriter stdout, TextWriter stderr)
        {
            this.stderr = stderr;
            eventQueue = new BlockingCollection<Event>();
            var settings = new XmlWriterSettings
            {
                Async = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };

            writer = XmlWriter.Create(stdout, settings);
            this.stdout = stdout;
        }

        #endregion

        /// <param name="eventElement">
        public void WriteEvent(Event e)
        {
            if (eventQueueMonitor == null)
                eventQueueMonitor = Task.Factory.StartNew<Task>(WriteEventElementsAsync);
            eventQueue.Add(e);
        }
    
        public void Dispose()
        {
            eventQueue.CompleteAdding();
            if (eventQueueMonitor != null)
                eventQueueMonitor.Wait();
            writer.Close();
        }

        public async Task LogAsync(string severity, string message)
        {
            await this.stderr.WriteAsync(severity + " " + message + this.stderr.NewLine);
        }

        private async Task WriteEventElementsAsync()
        {
            await this.writer.WriteStartElementAsync(prefix: null, localName: "stream", ns: null);

            Event e;
            while (!eventQueue.IsCompleted)
            {
                if (eventQueue.TryTake(out e))
                {
                    serializer.Serialize(writer, e);
                    await this.writer.FlushAsync();
                    EventWritten();
                }
                else
                {
                    await Task.Delay(50);
                }
            }
                
            await writer.WriteEndElementAsync();
            await writer.FlushAsync();
            EventWritten();
        }
    }
}
