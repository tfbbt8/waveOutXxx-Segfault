using KnightRyderAudioPlayback;
using System;
using System.IO;
using System.Timers;

namespace WaveOutError
{
    class MemoryStreamPlayer
    {
        // File to play in memory
        private MemoryStream MStream;
        private BinaryWriter MStreamWriter;
        private BinaryReader MStreamReader;

        // Audio player
        private WaveOutHandler wav = new WaveOutHandler();

        // Wave Header offset in bytes
        public int HeaderOffset { get; set; } = 44;

        // Timer to buffer more samples for waveout
        private Timer timer;

        // Lock
        private object lk = new object();

        public MemoryStreamPlayer()
        {
            MStream = new MemoryStream();
            MStreamWriter = new BinaryWriter(MStream);
            MStreamReader = new BinaryReader(MStream);

            timer = new Timer();
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true;
            timer.Interval = 50;
        }

        // Queue any new samples
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (lk)
            {
                // Error checking
                if (wav == null || MStream == null) return;

                // No new data in stream
                if (MStream.Position >= MStream.Length) return;

                // Byte buffer to read into from memory stream
                byte[] bbuffer = new byte[MStream.Length - MStream.Position];

                // Read in bytes from position to end
                MStreamReader.Read(bbuffer, 0, (int)(MStream.Length - MStream.Position));

                // Short buffer for waveout
                short[] buffer = new short[bbuffer.Length / sizeof(short)];

                // Copy bytes to short buffer
                Buffer.BlockCopy(bbuffer, 0, buffer, 0, bbuffer.Length);

                // Write samples to waveout
                wav.Write(buffer, (uint)buffer.Length / 2);

                // Advance position to end
                MStream.Position = MStream.Length;
            }
        }

        // MemoryStream operations
        public void Write(short[] buffer)
        {
            lock (lk)
            {
                long save = MStream.Position;

                MStream.Position = MStream.Length;
                byte[] bbuffer = new byte[buffer.Length * sizeof(short)];
                Buffer.BlockCopy(buffer, 0, bbuffer, 0, bbuffer.Length);
                MStreamWriter.Write(bbuffer, 0, bbuffer.Length);

                MStream.Position = save;
            }
        }

        // Playback operations
        public void Stop()
        {
            lock (lk)
            {
                wav.Close();
                wav = null;
            }
        }

        public void Reset()
        {
            wav.Reset();
        }

        public void Stream()
        {
            if (wav == null) wav = new WaveOutHandler();
            timer.Start();
        }
    }
}
