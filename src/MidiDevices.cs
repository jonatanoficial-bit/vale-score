using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ValeScore
{
    internal static class MidiDevices
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OutputCaps
        {
            public ushort Manufacturer, Product;
            public uint Driver;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public ushort Technology, Voices, Notes, ChannelMask;
            public uint Support;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct InputCaps
        {
            public ushort Manufacturer, Product;
            public uint Driver;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public uint Support;
        }

        internal sealed class Device
        {
            public uint Id;
            public string Name;
            public override string ToString() { return Name; }
        }

        [DllImport("winmm.dll")] private static extern uint midiOutGetNumDevs();
        [DllImport("winmm.dll")] private static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern uint midiOutGetDevCaps(UIntPtr id, out OutputCaps caps, uint size);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern uint midiInGetDevCaps(UIntPtr id, out InputCaps caps, uint size);
        [DllImport("winmm.dll")] private static extern uint midiOutOpen(out IntPtr handle, uint id, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint midiOutShortMsg(IntPtr handle, uint message);
        [DllImport("winmm.dll")] private static extern uint midiOutReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint midiOutClose(IntPtr handle);

        public static List<Device> Outputs()
        {
            var devices = new List<Device>();
            for (uint id = 0; id < midiOutGetNumDevs(); id++)
            {
                OutputCaps caps;
                if (midiOutGetDevCaps(new UIntPtr(id), out caps, (uint)Marshal.SizeOf(typeof(OutputCaps))) == 0)
                    devices.Add(new Device { Id = id, Name = caps.Name });
            }
            return devices;
        }

        public static List<Device> Inputs()
        {
            var devices = new List<Device>();
            for (uint id = 0; id < midiInGetNumDevs(); id++)
            {
                InputCaps caps;
                if (midiInGetDevCaps(new UIntPtr(id), out caps, (uint)Marshal.SizeOf(typeof(InputCaps))) == 0)
                    devices.Add(new Device { Id = id, Name = caps.Name });
            }
            return devices;
        }

        public static Task Play(MidiSequence sequence, Device device, Stopwatch clock, CancellationToken cancellation)
        {
            return Task.Run(() =>
            {
                IntPtr handle;
                Check(midiOutOpen(out handle, device.Id, IntPtr.Zero, IntPtr.Zero, 0), "abrir a saída MIDI");
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    clock.Restart();
                    foreach (var item in sequence.Events)
                    {
                        WaitUntil(clock, item.Seconds, cancellation);
                        Check(midiOutShortMsg(handle, (uint)(item.Status | (item.Data1 << 8) | (item.Data2 << 16))),
                            "enviar um evento MIDI");
                    }
                    WaitUntil(clock, sequence.Duration, cancellation);
                }
                finally
                {
                    midiOutReset(handle);
                    midiOutClose(handle);
                }
            }, cancellation);
        }

        private static void WaitUntil(Stopwatch clock, double time, CancellationToken cancellation)
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                double remaining = (time - clock.Elapsed.TotalSeconds) * 1000;
                if (remaining <= 0) return;
                cancellation.WaitHandle.WaitOne(Math.Max(1, (int)Math.Min(remaining, 40)));
            }
        }

        private static void Check(uint code, string operation)
        {
            if (code != 0) throw new InvalidOperationException("Não foi possível " + operation + " (WinMM " + code + ").");
        }

        internal sealed class Input : IDisposable
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            private delegate void MidiCallback(IntPtr handle, uint message, IntPtr instance, UIntPtr data, UIntPtr timestamp);
            [DllImport("winmm.dll")] private static extern uint midiInOpen(out IntPtr handle, uint id, MidiCallback callback, IntPtr instance, uint flags);
            [DllImport("winmm.dll")] private static extern uint midiInStart(IntPtr handle);
            [DllImport("winmm.dll")] private static extern uint midiInStop(IntPtr handle);
            [DllImport("winmm.dll")] private static extern uint midiInReset(IntPtr handle);
            [DllImport("winmm.dll")] private static extern uint midiInClose(IntPtr handle);

            private IntPtr handle;
            private readonly MidiCallback callback;

            public Input(Device device, Action<int> noteOn)
            {
                callback = (source, message, instance, data, timestamp) =>
                {
                    if (message != 0x3C3) return;
                    ulong value = data.ToUInt64();
                    if ((value & 0xF0) == 0x90 && (value & 0x7F0000) != 0)
                        noteOn((int)((value >> 8) & 127));
                };
                Check(midiInOpen(out handle, device.Id, callback, IntPtr.Zero, 0x30000), "abrir a entrada MIDI");
                try { Check(midiInStart(handle), "iniciar a entrada MIDI"); }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                if (handle == IntPtr.Zero) return;
                midiInStop(handle);
                midiInReset(handle);
                midiInClose(handle);
                handle = IntPtr.Zero;
                GC.KeepAlive(callback);
            }
        }
    }
}
