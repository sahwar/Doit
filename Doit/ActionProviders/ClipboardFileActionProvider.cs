﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Doit.Actions;

namespace Doit.ActionProviders
{
	public class ClipboardFileActionProvider : IActionProvider<FileAction>
	{
		public ICollection<Type> CanConsume { get; private set; }

		public IEnumerable<FileAction> Offer(string query)
		{
			var fileDropList = Clipboard.GetFileDropList();

			if (fileDropList.Count == 1)
			{
				yield return new FileAction(fileDropList[0]) { Hint = "On clipboard" };
			}

			if (fileDropList.Count > 1)
			{
				yield return new FileAction(fileDropList.Cast<string>().ToArray()) { Hint = "On clipboard" };
			}
		}

		public IEnumerable<FileAction> Offer(IAction action)
		{
			return Enumerable.Empty<FileAction>();
		}
	}
}