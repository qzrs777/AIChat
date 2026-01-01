using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AIChat.Core
{
    public static class AudioUtils
    {
        public static AudioClip TrimAudioClip(AudioClip original, int endPosition)
        {
            float[] data = new float[endPosition * original.channels];
            original.GetData(data, 0);

            AudioClip newClip = AudioClip.Create("TrimmedVoice", endPosition, original.channels, original.frequency, false);
            newClip.SetData(data, 0);
            return newClip;
        }

        // ================= 【新增 WAV 编码工具】 =================
        public static byte[] EncodeToWAV(AudioClip clip)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                // 1. 获取数据
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                // 2. 写入 WAV 头 (44 bytes)
                int hz = clip.frequency;
                int channels = clip.channels;
                int samplesCount = samples.Length;

                Byte[] riff = Encoding.UTF8.GetBytes("RIFF");
                stream.Write(riff, 0, 4);

                Byte[] chunkSize = BitConverter.GetBytes(samplesCount * 2 + 36);
                stream.Write(chunkSize, 0, 4);

                Byte[] wave = Encoding.UTF8.GetBytes("WAVE");
                stream.Write(wave, 0, 4);

                Byte[] fmt = Encoding.UTF8.GetBytes("fmt ");
                stream.Write(fmt, 0, 4);

                Byte[] subChunk1 = BitConverter.GetBytes(16);
                stream.Write(subChunk1, 0, 4);

                UInt16 one = 1;
                Byte[] audioFormat = BitConverter.GetBytes(one);
                stream.Write(audioFormat, 0, 2);

                Byte[] numChannels = BitConverter.GetBytes(channels);
                stream.Write(numChannels, 0, 2);

                Byte[] sampleRate = BitConverter.GetBytes(hz);
                stream.Write(sampleRate, 0, 4);

                Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2);
                stream.Write(byteRate, 0, 4);

                UInt16 blockAlign = (ushort)(channels * 2);
                stream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

                UInt16 bps = 16;
                Byte[] bitsPerSample = BitConverter.GetBytes(bps);
                stream.Write(bitsPerSample, 0, 2);

                Byte[] datastring = Encoding.UTF8.GetBytes("data");
                stream.Write(datastring, 0, 4);

                Byte[] subChunk2 = BitConverter.GetBytes(samplesCount * 2);
                stream.Write(subChunk2, 0, 4);

                // 3. 写入数据 (将 float -1.0~1.0 转换为 short -32768~32767)
                Int16[] intData = new Int16[samplesCount];
                Byte[] bytesData = new Byte[samplesCount * 2];
                int rescaleFactor = 32767;

                for (int i = 0; i < samplesCount; i++)
                {
                    intData[i] = (short)(samples[i] * rescaleFactor);
                    Byte[] byteArr = BitConverter.GetBytes(intData[i]);
                    byteArr.CopyTo(bytesData, i * 2);
                }

                stream.Write(bytesData, 0, bytesData.Length);
                return stream.ToArray();
            }
        }
    }
}
