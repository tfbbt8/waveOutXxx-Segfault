using System;
using System.IO;
using System.Threading;
using System.Runtime;
using System.Windows.Threading;

namespace WaveOutError
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                MemoryStreamPlayer msp = new MemoryStreamPlayer();

                Action<string> burst = new Action<string>((string file) =>
                {
                    // Open file and reader (16 bit 48000 hz PCM wav)
                    FileStream fs = new FileStream(file, FileMode.Open);
                    BinaryReader br = new BinaryReader(fs);

                    // Buffers for whole file and PCM only
                    byte[] bbuffer = new byte[fs.Length];
                    short[] buffer = new short[(bbuffer.Length - 44) / 2];

                    // Read in whole file
                    br.Read(bbuffer, 0, bbuffer.Length);

                    // Copy PCM into buffer without header
                    Buffer.BlockCopy(bbuffer, 44, buffer, 0, bbuffer.Length - 44);

                    Console.WriteLine("Loop gen starting");
                    // i is bytes
                    for (int i = 0; i < buffer.Length * sizeof(short); i += (2432 * 4))
                    {
                        short[] buf = new short[2432 * 2];
                        Buffer.BlockCopy(buffer, i, buf, 0, buf.Length * sizeof(short));
                        msp.Write(buf);

                        // Simulate interval on radio
                        Thread.Sleep(50);
                    }
                    Console.WriteLine("Loop gen done");
                });

                // Generator
                Thread generator_loop = new Thread(() =>
                {
                    burst(@"93300_2016-09-19_085439.wav");
                });

                Thread generator_once = new Thread(() =>
                {
                    // Open file and reader (16 bit 48000 hz PCM wav)
                    FileStream fs = new FileStream(@"93300_2016-09-19_112552.wav", FileMode.Open);
                    byte[] bbuffer = new byte[fs.Length];
                    fs.Read(bbuffer, 0, bbuffer.Length);
                    short[] sbuffer = new short[(bbuffer.Length - 44) / sizeof(short)];
                    Buffer.BlockCopy(bbuffer, 44, sbuffer, 0, (bbuffer.Length - 44));

                    Thread.Sleep(2000);
                    Console.WriteLine("Starting gen once");
                    msp.Write(sbuffer);
                    Console.WriteLine("Gen Once done");
                });

                // Start checking for new audio samples and play as they come
                msp.Stream();

                generator_once.Start();
                generator_loop.Start();
                
                // Allow audio to play and problems to occur
                Thread.Sleep(20000);

                generator_loop.Join();
                generator_once.Join();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
