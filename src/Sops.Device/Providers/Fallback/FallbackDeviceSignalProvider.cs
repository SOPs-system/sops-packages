using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Abstractions;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Providers.Fallback
{
    /// <summary>
    /// Safe fallback provider gathering non-invasive environmental signals
    /// available across all .NET runtime profiles.
    /// </summary>
    public sealed class FallbackDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly string? _salt;

        public FallbackDeviceSignalProvider(string? salt = null)
        {
            _salt = salt;
        }

        public Task<IReadOnlyList<DeviceSignal>> GetSignalsAsync(CancellationToken cancellationToken = default)
        {
            var signals = new List<DeviceSignal>();

            // 1. Processor core count
            string procCount = Environment.ProcessorCount.ToString();
            string normProc = DeviceSignalNormalizer.Normalize("ProcessorCount", procCount);
            signals.Add(DeviceSignal.Available("ProcessorCount", "Environment", normProc, DeviceSignalNormalizer.ComputeSha256(normProc, _salt), SignalStability.Medium, SignalReliability.Medium));

            // 2. Machine name hash
            try
            {
                string machine = Environment.MachineName;
                if (DeviceSignalNormalizer.IsValidSignal("MachineName", machine, out _))
                {
                    string norm = DeviceSignalNormalizer.Normalize("MachineName", machine);
                    signals.Add(DeviceSignal.Available("MachineName", "Environment", norm, DeviceSignalNormalizer.ComputeSha256(norm, _salt), SignalStability.Low, SignalReliability.Low));
                }
            }
            catch { }

            // 3. 64-bit architecture indicator
            string arch = IntPtr.Size == 8 ? "64bit" : "32bit";
            signals.Add(DeviceSignal.Available("ProcessArchitecture", "Environment", arch, DeviceSignalNormalizer.ComputeSha256(arch, _salt), SignalStability.High, SignalReliability.Low));

            return Task.FromResult<IReadOnlyList<DeviceSignal>>(signals);
        }
    }
}
