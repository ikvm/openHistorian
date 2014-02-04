﻿//******************************************************************************************************
//  EngineStreamFilter'2.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
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
//  2/2/2014 - Steven E. Chisholm
//       Generated original version of source code. 
//     
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GSF.Collections;
using GSF.SortedTreeStore.Engine;
using GSF.SortedTreeStore.Tree;

namespace GSF.SortedTreeStore.Filters
{
    public class EngineStreamFilterBitArray<TKey, TValue>
        : StreamFilterBase<TKey, TValue>
        where TKey : EngineKeyBase<TKey>, new()
        where TValue : class, ISortedTreeValue<TValue>, new()
    {
        long[] m_array;
        ulong m_maxValue;
        int m_count;

        public EngineStreamFilterBitArray(PointIDFilter.BitArrayFilter<TKey> keyMatchFilter)
        {
            m_array = keyMatchFilter.m_points.m_array;
            m_maxValue = keyMatchFilter.m_maxValue;
        }

        public override bool StopReading(TKey key, TValue value)
        {
            int point = (int)key.PointID;
            m_count++;
            return (m_count & 1023) == 0 || (key.PointID <= m_maxValue && (m_array[point >> BitArray.BitsPerElementShift] & (1L << (point & BitArray.BitsPerElementMask))) != 0);
        }
    }
}
