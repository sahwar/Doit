﻿using System.ComponentModel;
using System.Xml.Serialization;

namespace Doit.Settings
{
	[XmlType]
	public class WebQuerySettings
	{
		public WebQuerySettings()
		{
			IsEnabled = true;
		}

		[XmlAttribute]
		[DefaultValue(true)]
		public bool IsEnabled { get; set; }

		[XmlAttribute]
		[DefaultValue(false)]
		public bool IsFallback { get; set; }

		[XmlAttribute]
		public string Verb { get; set; }

		[XmlAttribute]
		public string Query { get; set; }

		[XmlAttribute]
		public string IconPath { get; set; }
	}
}