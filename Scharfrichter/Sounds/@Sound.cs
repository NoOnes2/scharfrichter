﻿using NAudio;
using NAudio.Codecs;
using NAudio.Wave;
using NAudio.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Scharfrichter.Codec.Sounds
{
	public class Sound
	{
		public byte[] Data;
		public WaveFormat Format;
		public float Panning = 0.5f;
		public float Volume = 1.0f;
		public int Channel = -1;

		public Sound()
		{
			Data = new byte[] { };
			Format = null;
		}

		public Sound(byte[] newData, WaveFormat newFormat)
		{
			SetSound(newData, newFormat);
		}

		static public Sound Read(Stream source)
		{
			Sound result = new Sound();
			WaveFileReader reader = new WaveFileReader(source);
			if (reader.Length > 0)
			{
				result.Data = new byte[reader.Length];
				reader.Read(result.Data, 0, result.Data.Length);
				result.Format = reader.WaveFormat;
			}
			else
			{
				result.Data = new byte[] { };
				result.Format = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 44100, 2, 44100 * 4, 4, 16);
			}
			result.Panning = 0.5f;
			result.Volume = 1.0f;
			return result;
		}

		public byte[] Render(float masterVolume)
		{
			// due to the way NAudio works, the source files must be provided twice.
			// this is because all channels are kept in sync by the mux, and the unused
			// channel data is discarded. If we tried to use the same source for both
			// muxes, it would try to read 2x the data present in the buffer!
			// If only we had a way to create separate WaveProviders from within the
			// MultiplexingWaveProvider..

			MemoryStream sourceLeft = new MemoryStream(Data);
			MemoryStream sourceRight = new MemoryStream(Data);
			RawSourceWaveStream waveLeft = new RawSourceWaveStream(sourceLeft, Format);
			RawSourceWaveStream waveRight = new RawSourceWaveStream(sourceRight, Format);

			// step 1: separate the stereo stream
			MultiplexingWaveProvider demuxLeft = new MultiplexingWaveProvider(new IWaveProvider[] { waveLeft }, 1);
			MultiplexingWaveProvider demuxRight = new MultiplexingWaveProvider(new IWaveProvider[] { waveRight }, 1);
			demuxLeft.ConnectInputToOutput(0, 0);
			demuxRight.ConnectInputToOutput(1, 0);

			// step 2: adjust the volume of a stereo stream
			VolumeWaveProvider16 volLeft = new VolumeWaveProvider16(demuxLeft);
			VolumeWaveProvider16 volRight = new VolumeWaveProvider16(demuxRight);

			// note: use logarithmic scale
#if (true)
			// log scale is applied to each operation
			float volumeValueLeft = (float)Math.Pow(1.0f - Panning, 0.5f);
			float volumeValueRight = (float)Math.Pow(Panning, 0.5f);
			// ensure 1:1 conversion
			volumeValueLeft /= (float)Math.Sqrt(0.5);
			volumeValueRight /= (float)Math.Sqrt(0.5);
			// apply volume
			volumeValueLeft *= (float)Math.Pow(Volume, 0.5f);
			volumeValueRight *= (float)Math.Pow(Volume, 0.5f);
			// clamp
			volumeValueLeft = Math.Min(Math.Max(volumeValueLeft, 0.0f), 1.0f);
			volumeValueRight = Math.Min(Math.Max(volumeValueRight, 0.0f), 1.0f);
#else
					// log scale is applied to the result of the operations
					float volumeValueLeft = (float)Math.Pow(1.0f - Panning, 0.5f);
					float volumeValueRight = (float)Math.Pow(Panning, 0.5f);
					// ensure 1:1 conversion
					volumeValueLeft /= (float)Math.Sqrt(0.5);
					volumeValueRight /= (float)Math.Sqrt(0.5);
					// apply volume
					volumeValueLeft *= Volume;
					volumeValueRight *= Volume;
					// apply log scale
					volumeValueLeft = (float)Math.Pow(volumeValueLeft, 0.5f);
					volumeValueRight = (float)Math.Pow(volumeValueRight, 0.5f);
					// clamp
					volumeValueLeft = Math.Min(Math.Max(volumeValueLeft, 0.0f), 1.0f);
					volumeValueRight = Math.Min(Math.Max(volumeValueRight, 0.0f), 1.0f);
#endif
			// use linear scale for master volume
			volLeft.Volume = volumeValueLeft * masterVolume;
			volRight.Volume = volumeValueRight * masterVolume;

			// step 3: combine them again
			IWaveProvider[] tracks = new IWaveProvider[] { volLeft, volRight };
			MultiplexingWaveProvider mux = new MultiplexingWaveProvider(tracks, 2);

			// step 4: export them to a byte array
			byte[] finalData = new byte[Data.Length];
			mux.Read(finalData, 0, finalData.Length);

			return finalData;
		}

		public void SetSound(byte[] data, WaveFormat sourceFormat)
		{
			MemoryStream dataStream = new MemoryStream(data);
			RawSourceWaveStream wavStream = new RawSourceWaveStream(dataStream, sourceFormat);
			WaveStream wavConvertStream = WaveFormatConversionStream.CreatePcmStream(wavStream);

			// using a mux, we force all sounds to be 2 channels
			MultiplexingWaveProvider sourceProvider = new MultiplexingWaveProvider(new IWaveProvider[] { wavConvertStream }, 2);
			int bytesToRead = (int)((wavConvertStream.Length * 2) / wavConvertStream.WaveFormat.Channels);
			byte[] rawWaveData = new byte[bytesToRead];
			int bytesRead = sourceProvider.Read(rawWaveData, 0, bytesToRead);

			Data = rawWaveData;
			Format = sourceProvider.WaveFormat;
		}

		public void Write(Stream target, float masterVolume)
		{
			if (Data != null && Data.Length > 0)
			{
				using (MemoryStream mem = new MemoryStream())
				{
					using (WaveFileWriter writer = new WaveFileWriter(new IgnoreDisposeStream(mem), Format))
					{
						byte[] finalData = Render(masterVolume);
						writer.Write(finalData, 0, finalData.Length);
						writer.Flush();
						target.Write(mem.ToArray(), 0, (int)mem.Length);
					}
				}
			}
		}

		public void WriteFile(string targetFile, float masterVolume)
		{
			using (MemoryStream target = new MemoryStream())
			{
				Write(target, masterVolume);
				target.Flush();
				File.WriteAllBytes(targetFile, target.ToArray());
			}
		}
	}
}
