/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Server
{
    /// <summary>
    /// Sampled value of a meter value message (independent of the OCPP version).
    /// Measurand/Phase/Location/Unit as in the OCPP specification (e.g. "Energy.Active.Import.Register", "L1-N", "Outlet", "kWh").
    /// </summary>
    public sealed record MeterSample(DateTimeOffset Timestamp, string Measurand, string Phase, string Location, double Value, string Unit, int Multiplier = 0);

    /// <summary>
    /// Values selected from a meter value message (-1 = not contained)
    /// </summary>
    public sealed class MeterReading
    {
        public double MeterKWH { get; set; } = -1;
        public double CurrentChargeKW { get; set; } = -1;
        public double StateOfCharge { get; set; } = -1;

        /// <summary>
        /// Timestamp of the meter value (MeterKWH)
        /// </summary>
        public DateTimeOffset? MeterTime { get; set; }
    }

    /// <summary>
    /// Selects meter value, charging power and SoC from the sampled values of a meter value message (all OCPP versions):
    /// - newest timestamp
    /// - location "Outlet" (or without location = default), "Inlet" only if there is no outlet value, "EV"/"Body"/"Cable" are ignored
    /// - value without phase preferred. Otherwise: power = sum of the phases, energy = sum of L1+L2+L3 (only if all three exist)
    /// - units case-insensitive, OCPP 2.x multiplier (10^x)
    /// </summary>
    public static class MeterValueEvaluator
    {
        public const string EnergyActiveImportRegister = "Energy.Active.Import.Register";
        public const string PowerActiveImport = "Power.Active.Import";
        public const string StateOfCharge = "SoC";

        private const string LocationOutlet = "Outlet";
        private const string LocationInlet = "Inlet";

        // phases L1/L2/L3 measured against N or ground ("N" and line-to-line values like "L1-L2" aren't used)
        private static readonly string[][] Phases =
        {
            new[] { "L1", "L1-N" },
            new[] { "L2", "L2-N" },
            new[] { "L3", "L3-N" }
        };

        private static readonly ConcurrentDictionary<Enum, string> EnumValues = new ConcurrentDictionary<Enum, string>();

        public static MeterReading Evaluate(IEnumerable<MeterSample> samples, ILogger logger)
        {
            MeterReading reading = new MeterReading();
            List<MeterSample> all = samples.ToList();
            foreach (MeterSample s in all)
            {
                logger.LogTrace("MeterValues => Timestamp={0} / Measurand={1} / Phase={2} / Location={3} / Value={4} / Unit={5} / Multiplier={6}",
                    s.Timestamp, s.Measurand, s.Phase, s.Location, s.Value, s.Unit, s.Multiplier);
            }

            // charged amount of energy (Spec: default measurand)
            List<MeterSample> energy = Converted(all.Where(s => IsMeasurand(s, EnergyActiveImportRegister)), ToKWh, "Value", logger);
            if (TrySelect(energy, requireAllPhases: true, logger, out double meterKWH, out DateTimeOffset meterTime))
            {
                reading.MeterKWH = meterKWH;
                reading.MeterTime = meterTime;
                logger.LogTrace("MeterValues => Value: '{0:0.000}' kWh", meterKWH);
            }

            // current charging power
            List<MeterSample> power = Converted(all.Where(s => IsMeasurand(s, PowerActiveImport)), ToKW, "Charging", logger);
            if (TrySelect(power, requireAllPhases: false, logger, out double currentChargeKW, out _))
            {
                reading.CurrentChargeKW = currentChargeKW;
                logger.LogTrace("MeterValues => Charging '{0:0.000}' kW", currentChargeKW);
            }

            // state of charge (battery status): any location (usually "EV")
            MeterSample soc = all.Where(s => IsMeasurand(s, StateOfCharge)).OrderBy(s => s.Timestamp).LastOrDefault();
            if (soc != null)
            {
                reading.StateOfCharge = soc.Value * Math.Pow(10, soc.Multiplier);
                logger.LogTrace("MeterValues => SoC: '{0:0.0}'%", reading.StateOfCharge);
            }

            return reading;
        }

        /// <summary>
        /// Spec value of an enum from the message classes ([EnumMember(Value = "...")]), null for null
        /// </summary>
        public static string EnumValue(Enum value)
        {
            if (value == null) return null;
            return EnumValues.GetOrAdd(value, v =>
            {
                FieldInfo field = v.GetType().GetField(v.ToString());
                return field?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? v.ToString();
            });
        }

        private static bool IsMeasurand(MeterSample sample, string measurand)
        {
            // no measurand = "Energy.Active.Import.Register" ("Missing": placeholder of the generated 2.x classes)
            string m = (string.IsNullOrEmpty(sample.Measurand) || sample.Measurand == "Missing") ? EnergyActiveImportRegister : sample.Measurand;
            return m == measurand;
        }

        /// <summary>
        /// Converts the values into kWh/kW (unit + multiplier). Values with unknown units are dropped.
        /// </summary>
        private static List<MeterSample> Converted(IEnumerable<MeterSample> samples, Func<string, double?> factor, string name, ILogger logger)
        {
            List<MeterSample> result = new List<MeterSample>();
            foreach (MeterSample s in samples)
            {
                double? f = factor(s.Unit?.ToLowerInvariant());
                if (f == null)
                {
                    logger.LogWarning("MeterValues => {0}: unexpected unit: '{1}' (Value={2}) => ignored", name, s.Unit, s.Value);
                    continue;
                }
                result.Add(s with { Value = s.Value * Math.Pow(10, s.Multiplier) * f.Value, Multiplier = 0 });
            }
            return result;
        }

        private static double? ToKWh(string unit) => unit switch
        {
            null or "wh" or "vah" or "varh" => 0.001,   // Spec: default "Wh"
            "kwh" or "kvah" or "kvarh" => 1,
            _ => null
        };

        private static double? ToKW(string unit) => unit switch
        {
            null or "w" or "va" or "var" => 0.001,
            "kw" or "kva" or "kvar" => 1,
            _ => null
        };

        /// <summary>
        /// Selects the value of the newest timestamp: outlet before inlet, value without phase before the phase values
        /// </summary>
        private static bool TrySelect(List<MeterSample> samples, bool requireAllPhases, ILogger logger, out double value, out DateTimeOffset timestamp)
        {
            value = -1;
            timestamp = default;
            foreach (IGrouping<DateTimeOffset, MeterSample> group in samples.GroupBy(s => s.Timestamp).OrderByDescending(g => g.Key))
            {
                // Spec: default location "Outlet"
                List<MeterSample> outlet = group.Where(s => string.IsNullOrEmpty(s.Location) || s.Location == LocationOutlet).ToList();
                List<MeterSample> inlet = group.Where(s => s.Location == LocationInlet).ToList();
                foreach (List<MeterSample> location in new[] { outlet, inlet })
                {
                    if (location.Count > 0 && TryTotal(location, requireAllPhases, logger, out value))
                    {
                        timestamp = group.Key;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryTotal(List<MeterSample> samples, bool requireAllPhases, ILogger logger, out double value)
        {
            MeterSample total = samples.LastOrDefault(s => string.IsNullOrEmpty(s.Phase));
            if (total != null)
            {
                value = total.Value;
                return true;
            }

            // only values per phase
            List<MeterSample> phaseValues = Phases
                .Select(names => samples.LastOrDefault(s => names.Contains(s.Phase)))
                .Where(s => s != null)
                .ToList();
            if (phaseValues.Count == 0 || (requireAllPhases && phaseValues.Count < Phases.Length))
            {
                if (phaseValues.Count > 0)
                {
                    logger.LogWarning("MeterValues => Meter value only for phase(s) {0} => ignored (total needs L1, L2 and L3)", string.Join(", ", phaseValues.Select(s => s.Phase)));
                }
                value = -1;
                return false;
            }
            value = phaseValues.Sum(s => s.Value);
            return true;
        }
    }
}
