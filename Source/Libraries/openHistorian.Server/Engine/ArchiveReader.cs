﻿//******************************************************************************************************
//  ArchiveReader.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  10/25/2012 - Steven E. Chisholm
//       Generated original version of source code. 
//       
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GSF.Threading;
using openHistorian.Collections;
using openHistorian.Collections.KeyValue;
using openHistorian.Archive;

namespace openHistorian.Engine
{
    internal class ArchiveReader : IHistorianDataReader
    {
        ArchiveList m_list;
        ArchiveListSnapshot m_snapshot;
        long m_timeout;

        public ArchiveReader(ArchiveList list, long timeout)
        {
            m_timeout = timeout;
            m_list = list;
            m_snapshot = m_list.CreateNewClientResources();
        }

        public IPointStream Read(ulong key1)
        {
            return new ReadStream(key1, key1, m_snapshot, m_timeout);
        }

        public IPointStream Read(ulong startKey1, ulong endKey1)
        {
            return new ReadStream(startKey1, endKey1, m_snapshot, m_timeout);
        }

        public IPointStream Read(ulong startKey1, ulong endKey1, IEnumerable<ulong> listOfKey2)
        {
            ulong maxValue = listOfKey2.Union(new ulong[] { 0 }).Max();
            if (maxValue < 8 * 1024 * 64) //524288
            {
                return new ReadStreamFilteredBitArray(startKey1, endKey1, m_snapshot, listOfKey2, (int)maxValue, m_timeout);
            }
            else if (maxValue <= uint.MaxValue)
            {
                return new ReadStreamFilteredIntDictionary(startKey1, endKey1, m_snapshot, listOfKey2, m_timeout);
            }
            else
            {
                return new ReadStreamFilteredLongDictionary(startKey1, endKey1, m_snapshot, listOfKey2, m_timeout);
            }
        }

        /// <summary>
        /// Closes the current reader.
        /// </summary>
        public void Close()
        {
            Dispose();
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            m_snapshot.Dispose();
        }

        private class ReadStream : IPointStream
        {
            ArchiveListSnapshot m_snapshot;
            ulong m_startKey;
            ulong m_stopKey;
            bool m_timedOut;
            long m_pointCount;

            TimeoutOperation m_timeout;
            Queue<KeyValuePair<int, ArchiveFileSummary>> m_tables;

            int m_currentIndex;
            ArchiveFileSummary m_currentSummary;
            ArchiveFileReadSnapshot m_currentInstance;
            ITreeScanner256 m_currentScanner;

            public ReadStream(ulong startKey, ulong stopKey, ArchiveListSnapshot snapshot, long timeout)
            {
                if (timeout > 0)
                {
                    m_timeout = new TimeoutOperation();
                    m_timeout.RegisterTimeout(new TimeSpan(timeout * TimeSpan.TicksPerMillisecond), () => m_timedOut = true);
                }

                m_startKey = startKey;
                m_stopKey = stopKey;
                m_snapshot = snapshot;
                m_snapshot.UpdateSnapshot();

                m_tables = new Queue<KeyValuePair<int, ArchiveFileSummary>>();

                for (int x = 0; x < m_snapshot.Tables.Count(); x++)
                {
                    var table = m_snapshot.Tables[x];
                    if (table != null)
                    {
                        if (table.Contains(startKey, stopKey))
                        {
                            m_tables.Enqueue(new KeyValuePair<int, ArchiveFileSummary>(x, table));
                        }
                        else
                        {
                            m_snapshot.Tables[x] = null;
                        }
                    }
                }
                prepareNextFile();
            }

            public bool Read(out ulong key1, out ulong key2, out ulong value1, out ulong value2)
            {
                if (m_timedOut)
                    Cancel();
                if (m_currentScanner.GetNextKey(out key1, out key2, out value1, out value2))
                {
                    if (key1 <= m_stopKey)
                        return true;
                }
                if (!prepareNextFile())
                {
                    if (m_timeout != null)
                    {
                        m_timeout.Cancel();
                        m_timeout = null;
                    }
                    return false;
                }
                return Read(out key1, out key2, out value1, out value2);
            }

            bool prepareNextFile()
            {
                if (m_currentInstance != null)
                {
                    m_currentInstance.Dispose();
                    m_snapshot.Tables[m_currentIndex] = null;
                    m_currentInstance = null;
                }
                if (m_tables.Count > 0)
                {
                    var kvp = m_tables.Dequeue();
                    m_currentIndex = kvp.Key;
                    m_currentInstance = kvp.Value.ActiveSnapshotInfo.CreateReadSnapshot();
                    m_currentScanner = m_currentInstance.GetTreeScanner();
                    m_currentScanner.SeekToKey(m_startKey, 0);
                }
                else
                {
                    m_currentScanner = NullTreeScanner256.Instance;
                    return false;
                }
                return true;
            }
            public void Cancel()
            {
                if (m_timeout != null)
                {
                    m_timeout.Cancel();
                    m_timeout = null;
                }

                if (m_currentInstance != null)
                {
                    m_currentInstance.Dispose();
                    m_snapshot.Tables[m_currentIndex] = null;
                    m_currentInstance = null;
                }
                m_currentScanner = NullTreeScanner256.Instance;
                while (m_tables.Count > 0)
                {
                    var kvp = m_tables.Dequeue();
                    m_snapshot.Tables[kvp.Key] = null;
                }
            }
        }

        private class ReadStreamFilteredBitArray : IPointStream
        {
            ReadStream m_stream;
            BitArray m_points;
            ulong m_maxValue;

            public ReadStreamFilteredBitArray(ulong startKey, ulong stopKey, ArchiveListSnapshot snapshot, IEnumerable<ulong> points, int maxValue, long timeout)
            {
                m_maxValue = (ulong)maxValue;
                m_points = new BitArray(maxValue + 1, false);
                foreach (ulong pt in points)
                {
                    m_points.SetBit((int)pt);
                }
                m_stream = new ReadStream(startKey, stopKey, snapshot, timeout);
            }

            public bool Read(out ulong key1, out ulong key2, out ulong value1, out ulong value2)
            {
                while (m_stream.Read(out key1, out key2, out value1, out value2))
                {
                    if (key2 <= m_maxValue && m_points[(int)key2])
                        return true;
                }
                return false;
            }

            public void Cancel()
            {
                m_stream.Cancel();
            }
        }

        private class ReadStreamFilteredLongDictionary : IPointStream
        {
            ReadStream m_stream;
            Dictionary<ulong, byte> m_points;

            public ReadStreamFilteredLongDictionary(ulong startKey, ulong stopKey, ArchiveListSnapshot snapshot, IEnumerable<ulong> points, long timeout)
            {
                m_points = new Dictionary<ulong, byte>(points.Count() * 5);
                foreach (ulong pt in points)
                {
                    m_points.Add(pt, 0);
                }
                m_stream = new ReadStream(startKey, stopKey, snapshot, timeout);
            }

            public bool Read(out ulong key1, out ulong key2, out ulong value1, out ulong value2)
            {
                while (m_stream.Read(out key1, out key2, out value1, out value2))
                {
                    if (m_points.ContainsKey(key2))
                        return true;
                }
                return false;
            }

            public void Cancel()
            {
                m_stream.Cancel();
            }
        }

        private class ReadStreamFilteredIntDictionary : IPointStream
        {
            ReadStream m_stream;
            Dictionary<uint, byte> m_points;

            public ReadStreamFilteredIntDictionary(ulong startKey, ulong stopKey, ArchiveListSnapshot snapshot, IEnumerable<ulong> points, long timeout)
            {
                m_points = new Dictionary<uint, byte>(points.Count() * 5);
                foreach (ulong pt in points)
                {
                    m_points.Add((uint)pt, 0);
                }
                m_stream = new ReadStream(startKey, stopKey, snapshot, timeout);
            }

            public bool Read(out ulong key1, out ulong key2, out ulong value1, out ulong value2)
            {
                while (m_stream.Read(out key1, out key2, out value1, out value2))
                {
                    if (key2 <= uint.MaxValue && m_points.ContainsKey((uint)key2))
                        return true;
                }
                return false;
            }

            public void Cancel()
            {
                m_stream.Cancel();
            }
        }

    }
}
