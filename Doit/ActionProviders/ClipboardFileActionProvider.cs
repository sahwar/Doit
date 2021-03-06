﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows;
using Doit.Actions;
using Doit.Infrastructure;

namespace Doit.ActionProviders
{
	public class ClipboardFileActionProvider : IActionProvider<FileAction>
	{
		public ICollection<Type> CanConsume { get; private set; }
		
		public bool IsFallback { get; set; }

		public IEnumerable<FileAction> Offer(string query)
		{
			if (!string.IsNullOrEmpty(query))
			{
				yield break;
			}

			StringCollection fileDropList = null;

			Utils.RunOnStaThread(() => fileDropList = Clipboard.GetFileDropList());

			if (fileDropList.Count == 1)
			{
				FileAction fileAction = null;

				try
				{
					fileAction = new FileAction(fileDropList[0]) { Hint = "On clipboard" };
				}
				catch (ArgumentException)
				{
				}

				yield return fileAction;
			}

			if (fileDropList.Count > 1)
			{
				yield return new FileAction(fileDropList.Cast<string>().ToArray()) { Hint = "On clipboard" };
			}
		}

		public IEnumerable<FileAction> Offer(IAction action, string query)
		{
			return Enumerable.Empty<FileAction>();
		}
	}
}