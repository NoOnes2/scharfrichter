﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Scharfrichter.Codec.Compression
{
	static public class BemaniLZ
	{
		private static int bufferMask = 0x3FF; // 10 bits window
		private static int bufferSize = 0x400;

		static public void Decode(Stream source, Stream target)
		{
			using (MemoryStream mem = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(mem))
				{
					using (BinaryReader reader = new BinaryReader(source))
					{
						byte[] buffer = new byte[bufferSize];
						int bufferOffset = 0;
						byte data = 0;
						int control = 0;
						int distance = 0; // used as a byte-distance
						int length = 0; // used as a counter
						bool loop = false;

						while (true)
						{
							loop = false;

							control >>= 1;
							if (control < 0x100)
								control = reader.ReadByte() | 0xFF00;

							data = reader.ReadByte();

							// direct copy
							if ((control & 1) == 0)
							{
								writer.Write(data);
								buffer[bufferOffset] = data;
								bufferOffset = (bufferOffset + 1) & bufferMask;
								continue;
							}

							// long distance
							if ((data & 0x80) == 0)
							{
								distance = reader.ReadByte() | ((data & 0x3) << 8);
								length = (data >> 2) + 2;
								loop = true;
							}

							// short distance
							if ((data & 0x40) == 0)
							{
								distance = (data & 0xF) + 1;
								length = ((data >> 4) & 0x3) + 1;
								loop = true;
							}

							// loop for jumps
							if (loop)
							{
								while (length-- >= 0)
								{
									data = buffer[(bufferOffset - distance) & bufferMask];
									writer.Write(data);
									buffer[bufferOffset] = data;
									bufferOffset = (bufferOffset + 1) & bufferMask;
								}
								continue;
							}

							// end of stream
							if (data == 0xFF)
								break;

							// block copy
							length = (data & 0xBF) + 7;
							while (length-- >= 0)
							{
								data = reader.ReadByte();
								writer.Write(data);
								buffer[bufferOffset] = data;
								bufferOffset = (bufferOffset + 1) & bufferMask;
							}
						}
					}
				}
				using (BinaryWriter writer = new BinaryWriter(target))
				{
					writer.Write(mem.ToArray());
					writer.Flush();
				}
			}
			target.Flush();
		}

		static public void Encode(Stream source, Stream target)
		{
		}
	}
}
