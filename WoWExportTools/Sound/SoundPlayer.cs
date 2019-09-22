using System;
using System.IO;
using NAudio.Wave;
using NAudio.Vorbis;
using WoWFormatLib.Utils;

namespace WoWExportTools.Sound
{
    public struct Sound
    {
        public Stream fs;
        public WaveStream ws;
    }

    public class SoundPlayer
    {
        public static uint FORMAT_MP3 = 0x1;
        public static uint FORMAT_VORBIS = 0x2;

        public event EventHandler PlaybackStopped;

        private WaveOut outputDevice;
        private Sound activeSound;

        public bool IsPlaying
        {
            get
            {
                // We don't currently utilize pausing, so assume anything other than 'stopped' is playing.
                return outputDevice != null && outputDevice.PlaybackState != PlaybackState.Stopped;
            }
        }

        public void Play(uint fileID, uint format)
        {
            Stream fs = CASC.OpenFile(fileID);
            WaveStream ws;

            try
            {
                if (format == FORMAT_MP3)
                    ws = new Mp3FileReader(fs);
                else if (format == FORMAT_VORBIS)
                    ws = new VorbisWaveReader(fs);
                else
                    throw new Exception("Unsupported audio format: " + format);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to play sound: " + fileID);
                Console.WriteLine(e.Message);

                fs.Dispose();
                PlaybackStopped(this, EventArgs.Empty);

                return;
            }

            if (outputDevice == null)
            {
                outputDevice = new WaveOut();
                outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            }
            else
            {
                Stop();
            }

            activeSound = new Sound { fs = fs, ws = ws };

            outputDevice.Init(ws);
            outputDevice.Play();
        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Stop();
            PlaybackStopped(this, EventArgs.Empty);
        }

        public void Stop()
        {
            if (outputDevice != null)
            {
                if (outputDevice.PlaybackState != PlaybackState.Stopped)
                {
                    outputDevice.Stop();
                    PlaybackStopped(this, EventArgs.Empty);
                }

                activeSound.fs.Dispose();
                activeSound.ws.Dispose();
            }
        }
    }
}
