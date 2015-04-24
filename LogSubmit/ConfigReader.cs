﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Unclassified.LogSubmit
{
	class ConfigReader
	{
		private string fileName;

		public ConfigReader(string fileName)
		{
			this.fileName = fileName;
		}

		public void Read()
		{
			using (var reader = new StreamReader(fileName, Encoding.UTF8))
			{
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine().Trim();

					string[] chunks = line.Split(new char[] { '=' }, 2);
					string item = chunks[0].Trim().ToLowerInvariant();
					string value = chunks.Length > 1 ? chunks[1].Trim() : null;

					if (value != null)
					{
						switch (item)
						{
							case "transport.mail.address":
								SharedData.Instance.MailTransportRecipientAddress = value;
								break;
						}
					}
				}
			}
		}

		public string ReadPath()
		{
			try
			{
				using (var reader = new StreamReader(fileName, Encoding.UTF8))
				{
					while (!reader.EndOfStream)
					{
						string line = reader.ReadLine().Trim();

						string[] chunks = line.Split(new char[] { '=' }, 2);
						string item = chunks[0].Trim().ToLowerInvariant();
						string value = chunks.Length > 1 ? chunks[1].Trim() : null;

						if (value != null)
						{
							switch (item)
							{
								case "path":
									if (!string.IsNullOrEmpty(value))
									{
										if (!Path.IsPathRooted(value))
										{
											value = Path.Combine(Path.GetDirectoryName(fileName), value);
										}
										return value;
									}
									break;
							}
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}
	}
}