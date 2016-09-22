using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace WaveOutError
{
    public abstract class WaveOutNative
    {
        public delegate void WaveOutDelegate(IntPtr hdr, int message, int userData, ref WaveOutNative.WaveHeader waveHeader, int param);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WaveFormatEx
        {
            public short formatTag;
            public short channels;
            public int samplesPerSec;
            public int avgBytesPerSec;
            public short blockAlign;
            public short bitsPerSample;
            public short size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WaveHeader
        {
            public IntPtr data;
            public int bufferLength;
            public int bytesRecorded;
            public IntPtr user;
            public int flags;
            public int loops;
            public IntPtr next;
            public int reserved;
        }

        [DllImport("winmm.dll")]
        public extern static int waveOutOpen(ref IntPtr waveOutHandle, int deviceId, ref WaveFormatEx waveFormatEx, WaveOutDelegate callback, int userData, int flags);
        [DllImport("winmm.dll")]
        public extern static int waveOutClose(IntPtr waveOutHandle);
        [DllImport("winmm.dll")]
        public extern static int waveOutPrepareHeader(IntPtr waveOutHandle, ref WaveHeader waveHeader, int size);
        [DllImport("winmm.dll")]
        public extern static int waveOutUnprepareHeader(IntPtr waveOutHandle, ref WaveHeader waveHeader, int size);
        [DllImport("winmm.dll")]
        public extern static int waveOutWrite(IntPtr waveOutHandle, ref WaveHeader waveHeader, int size);
        [DllImport("winmm.dll")]
        public extern static int waveOutReset(IntPtr waveOutHandle);
    }

    public class WaveOutBuffer : IDisposable
    {
        private IntPtr waveOutHandle;
        public WaveOutNative.WaveHeader waveHeader;
        private GCHandle headerHandle;

        public WaveOutBuffer(IntPtr theWaveOutHandle, short[] theBuffer, UInt32 theNumberOfSamples)
        {
            waveOutHandle = theWaveOutHandle;

            headerHandle = GCHandle.Alloc(new short[2 * theNumberOfSamples], GCHandleType.Pinned);
            waveHeader = new WaveOutNative.WaveHeader();
            waveHeader.user = (IntPtr)GCHandle.Alloc(this);
            waveHeader.bufferLength = (int)theNumberOfSamples * 2 * Marshal.SizeOf(typeof(short));
            waveHeader.data = headerHandle.AddrOfPinnedObject();

            Marshal.Copy(theBuffer, 0, waveHeader.data, (int)(2 * theNumberOfSamples));
        }

        ~WaveOutBuffer()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (waveHeader.data != IntPtr.Zero)
            {
                WaveOutNative.waveOutUnprepareHeader(waveOutHandle, ref waveHeader, Marshal.SizeOf(waveHeader));
                headerHandle.Free();
                waveHeader.data = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

    }

    public class WaveOutHandler
    {
        private static int BUFFER_MAX_COUNT = 5;

        private WaveOutNative.WaveFormatEx waveFormat = new WaveOutNative.WaveFormatEx();
        private WaveOutBuffer[] waveOutBuffers = new WaveOutBuffer[BUFFER_MAX_COUNT];
        private int waveHeaderIndex = -1;
        public static int sentBufferCount = 0;
        private IntPtr waveOutHandle;

        private WaveOutNative.WaveOutDelegate dataReceived = new WaveOutNative.WaveOutDelegate(OnWaveOutDataReceived);

        public bool isOpened = false;

        public WaveOutHandler()
        {
            waveFormat.formatTag = 1; // WAVE_FORMAT_PCM
            waveFormat.channels = 2;
            waveFormat.samplesPerSec = 48000;
            waveFormat.bitsPerSample = 16;
            waveFormat.blockAlign = (Int16)(waveFormat.channels * waveFormat.bitsPerSample / 8);
            waveFormat.avgBytesPerSec = waveFormat.blockAlign * waveFormat.samplesPerSec;
            waveFormat.size = 0;

            isOpened = WaveOutNative.waveOutOpen(ref waveOutHandle, -1 /* WAVE_MAPPER */, ref waveFormat, dataReceived, 0, 0x00030000 /* CALLBACK_FUNCTION */) == 0 /* MMSYSERR_NOERROR */;
        }

        public void Reset()
        {
            WaveOutNative.waveOutReset(waveOutHandle);
        }

        public void Close()
        {
            WaveOutNative.waveOutReset(waveOutHandle);
            while (sentBufferCount > 0) Thread.Sleep(10);
            WaveOutNative.waveOutClose(waveOutHandle);
        }

        public void Write(short[] theBuffer, UInt32 theNumberOfSamples)
        {
            if (waveHeaderIndex < BUFFER_MAX_COUNT - 1) waveHeaderIndex++;
            else waveHeaderIndex = 0;

            if (waveOutBuffers[waveHeaderIndex] != null) waveOutBuffers[waveHeaderIndex].Dispose();

            WaveOutBuffer aWaveOutBuffer = new WaveOutBuffer(waveOutHandle, theBuffer, theNumberOfSamples);
            waveOutBuffers[waveHeaderIndex] = aWaveOutBuffer;
            if (WaveOutNative.waveOutPrepareHeader(waveOutHandle, ref aWaveOutBuffer.waveHeader, Marshal.SizeOf(typeof(WaveOutNative.WaveHeader))) != 0 /* MMSYSERR_NOERROR */)
            {
                return;
            }

            sentBufferCount++;

            if (WaveOutNative.waveOutWrite(waveOutHandle, ref aWaveOutBuffer.waveHeader, Marshal.SizeOf(typeof(WaveOutNative.WaveHeader))) != 0 /* MMSYSERR_NOERROR */)
            {
                aWaveOutBuffer.Dispose();
            }
        }

        public static void OnWaveOutDataReceived(IntPtr theHdr, int theMessage, int theUserData, ref WaveOutNative.WaveHeader theWaveHeader, int theParam)
        {
            if (theMessage == 0x3BD /* MM_WOM_DONE */)
            {
                GCHandle aHandle = (GCHandle)theWaveHeader.user;

                WaveOutHandler.sentBufferCount--;
            }
        }
    }
}
