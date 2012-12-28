﻿//******************************************************************************************************
//  SignalGroup.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
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
//  12/12/2012 - Steven E. Chisholm
//       Generated original version of source code. 
//
//******************************************************************************************************
using System.Linq;
using System;
using System.Collections.Generic;
using openVisN.Calculations;
using openVisN.Library;

namespace openVisN
{

    public abstract class SignalGroup
    {
        SortedList<string, MetadataBase> m_signals;
        public string SignalGroupName { get; set; }

        SortedList<string, MetadataBase> Signals
        {
            get
            {
                if (m_signals == null)
                {
                    var allSignals = GetAllSignalsNew();
                    var signals = new SortedList<string, MetadataBase>(allSignals.Count);
                    allSignals.ForEach((x) => signals.Add(x.Key, x.Value));
                    m_signals = signals;
                }
                return m_signals;
            }
        }

        public IList<MetadataBase> GetAllSignals()
        {
            return Signals.Values;
        }

        protected abstract List<KeyValuePair<string, MetadataBase>> GetAllSignalsNew();

        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }
        public override int GetHashCode()
        {
            return SignalGroupName.GetHashCode();
        }

        public MetadataBase TryGetSignal(string signalType)
        {
            MetadataBase rv;
            if (Signals.TryGetValue(signalType, out rv))
                return rv;
            return null;
        }

    }

    public class SinglePhasorTerminal : SignalGroup
    {
        public SignalGroupBook ExtraData;
        public MetadataBase VoltageMagnitude;
        public MetadataBase VoltageAngle;
        public MetadataBase CurrentMagnitude;
        public MetadataBase CurrentAngle;
        public MetadataBase Dfdt;
        public MetadataBase Frequency;
        public MetadataBase Status;

        public MetadataBase Watt;
        public MetadataBase PowerFactor;
        public MetadataBase VoltAmpre;
        public MetadataBase VoltAmpreReactive;

        public MetadataBase VoltageMagnitudePu;

        public MetadataBase VoltageAngleReference;

        public void CreateCalculatedSignals(MetadataBase angleReference)
        {
            var calcPu = new SignalScaling(Math.Sqrt(3) / ExtraData.NominalVoltage, VoltageMagnitude);
            calcPu.GetPoints(out VoltageMagnitudePu);

            var calc = new SinglePhasorPowerSignals(VoltageMagnitude, VoltageAngle, CurrentMagnitude, CurrentAngle);
            calc.GetPoints(out Watt, out PowerFactor, out VoltAmpre, out VoltAmpreReactive);

            var calcRef = new SignalAngleDifference(VoltageAngle, angleReference);
            calcRef.GetPoints(out VoltageAngleReference);
        }

        protected override List<KeyValuePair<string, MetadataBase>> GetAllSignalsNew()
        {
            var list = new List<KeyValuePair<string, MetadataBase>>();
            list.Add(new KeyValuePair<string, MetadataBase>("Voltage Magnitude", VoltageMagnitude));
            list.Add(new KeyValuePair<string, MetadataBase>("Voltage Angle", VoltageAngle));
            list.Add(new KeyValuePair<string, MetadataBase>("Current Magnitude", CurrentMagnitude));
            list.Add(new KeyValuePair<string, MetadataBase>("Current Angle", CurrentAngle));
            list.Add(new KeyValuePair<string, MetadataBase>("DFDT", Dfdt));
            list.Add(new KeyValuePair<string, MetadataBase>("Frequency", Frequency));
            list.Add(new KeyValuePair<string, MetadataBase>("Status", Status));
            list.Add(new KeyValuePair<string, MetadataBase>("Watt", Watt));
            list.Add(new KeyValuePair<string, MetadataBase>("Power Factor", PowerFactor));
            list.Add(new KeyValuePair<string, MetadataBase>("Volt Ampre", VoltAmpre));
            list.Add(new KeyValuePair<string, MetadataBase>("Volt Ampre Reactive", VoltAmpreReactive));
            list.Add(new KeyValuePair<string, MetadataBase>("Voltage Magnitude Per Unit", VoltageMagnitudePu));
            list.Add(new KeyValuePair<string, MetadataBase>("Voltage Angle Reference", VoltageAngleReference));
            return list;

        }
    }
}
