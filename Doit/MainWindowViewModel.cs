﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Doit.ActionProviders;
using Doit.Actions;
using Doit.Infrastructure;
using Doit.Native;

namespace Doit
{
	public class MainWindowViewModel : INotifyPropertyChanged
	{
		private static readonly MainWindowViewModel _instance;

		private readonly ObservableCollection<ActionItem> _actions;
		
		private readonly IActionProvider<IAction>[] _actionProviders;
		
		private readonly Dictionary<Type, IList<IActionProvider<IAction>>> _consumableTypeMap;

		private readonly DispatcherTimer _timer;

		private string _query;
		
		private ActionItem _selectedAction;

		private ObservableCollection<IAction> _accumulatedActions;

		private int _lastActiveWindowHandle;

		static MainWindowViewModel()
		{
			_instance = new MainWindowViewModel();
		}

		private MainWindowViewModel()
		{
			_actions = new ObservableCollection<ActionItem>();
			_accumulatedActions = new ObservableCollection<IAction>();

			_actionProviders = new IActionProvider<IAction>[]
			{
				new ApplicationActionProviders(),
				new RunApplicationActionProvider(),
				new FindActionProvider(),
				new GoToAddressActionProvider(),
				new SearchWebActionProvider("Google", "https://www.google.com/search?q={0}") { Icon = new BitmapImage(new Uri("pack://application:,,,/Images/Google32.png")) },
				new SearchWebActionProvider("Bing", "http://www.bing.com/search?q={0}") { Icon = new BitmapImage(new Uri("pack://application:,,,/Images/Bing32.png")) },
				new SearchWebActionProvider("Duck", "https://duckduckgo.com/?q={0}") { Icon = new BitmapImage(new Uri("pack://application:,,,/Images/Duck32.png")) },
				new SearchWebActionProvider("Wiki", "http://en.wikipedia.org/wiki/Special:Search?search={0}&go=Go") { Icon = new BitmapImage(new Uri("pack://application:,,,/Images/Wiki32.png")) },
				new SearchWebActionProvider("IMDB", "http://www.imdb.com/find?s=all&q={0}") { Icon = new BitmapImage(new Uri("pack://application:,,,/Images/Imdb32.png")) },
				new ZipFileActionProvider(),
				new MailActionProvider(),
				new ExplorerFileActionProvider(),
				new ClipboardFileActionProvider(),
			};

			_consumableTypeMap = new Dictionary<Type, IList<IActionProvider<IAction>>>();

			foreach (var actionProvider in _actionProviders.Where(ap => ap.CanConsume != null && ap.CanConsume.Count > 0))
			{
				foreach (var type in actionProvider.CanConsume)
				{
					IList<IActionProvider<IAction>> providers;

					if (!_consumableTypeMap.TryGetValue(type, out providers))
					{
						providers = new List<IActionProvider<IAction>>();
						_consumableTypeMap[type] = providers;
					}

					providers.Add(actionProvider);
				}
			}

			_query = string.Empty;

			_timer = new DispatcherTimer();
			_timer.Interval = TimeSpan.FromMilliseconds(250d);
			_timer.Tick += (sender, args) =>
			{
				UpdateActions();

				_timer.Stop();
			};
		}

		public event EventHandler<ChangeVisibilityEventArgs> ChangeVisibilityRequested;

		public event PropertyChangedEventHandler PropertyChanged;

		public static MainWindowViewModel Instance
		{
			get { return _instance; }
		}

		public string Query
		{
			get
			{
				return _query;
			}

			set
			{
				_query = value;

				if (_timer != null)
				{
					_timer.Stop();
					_timer.Start();
				}

				OnPropertyChanged();
			}
		}

		public ObservableCollection<ActionItem> Actions
		{
			get { return _actions; }
		}

		public ActionItem SelectedAction
		{
			get
			{
				return _selectedAction;
			}

			set
			{ 
				_selectedAction = value;

				LastSelectedQuery = _selectedAction == null ? string.Empty : Query;

				OnPropertyChanged();
			}
		}

		public string LastSelectedQuery { get; set; }

		public bool ExecuteActionWhenAvailable { get; set; }

		public ObservableCollection<IAction> AccumulatedActions
		{
			get { return _accumulatedActions; }
		}

		public int LastActiveWindowHandle
		{
			get { return _lastActiveWindowHandle; }
		}

		public void UpdateActions()
		{
			Actions.Clear();

			IAction[] actions;

			if (_accumulatedActions.Count == 0)
			{
				actions = _actionProviders
					.SelectMany(ap => ap.Offer(Query))
					.Take(10)
					.ToArray();
			}
			else
			{
				var lastAccumulatedAction = _accumulatedActions.Last();

				var nextActions = new HashSet<IAction>();

				foreach (var map in _consumableTypeMap)
				{
					if (map.Key.IsInstanceOfType(lastAccumulatedAction))
					{
						foreach (var action in map.Value.SelectMany(ap => ap.Offer(lastAccumulatedAction)))
						{
							nextActions.Add(action);
						}
					}
				}

				actions = nextActions.ToArray();
			}

			if (ExecuteActionWhenAvailable)
			{
				ExecuteActionWhenAvailable = false;

				if (actions.Length > 0)
				{
					ExecuteAction(actions[0]);

					return;
				}
			}

			for (var index = 0; index < actions.Length; index++)
			{
				Actions.Add(new ActionItem(actions[index], index + 1));
			}

			SelectedAction = Actions.FirstOrDefault();
		}

		public void ExecuteAction(IAction action)
		{
			Debug.WriteLine("ExecuteAction, Last Selected Query: {0}, Current Query: {1}", LastSelectedQuery, Query);

			if (LastSelectedQuery != Query)
			{
				Debug.WriteLine("Last and Current Queries are different, updating actions...");

				_timer.Stop();
				UpdateActions();

				action = SelectedAction.Action;

				if (action == null)
				{
					Debug.WriteLine("No actions found matching current query, won't execute.");

					return;
				}
			}

			Debug.WriteLine("Executing...");

			var hideOnComplete = true;

			if (AccumulatedActions.Count == 0)
			{
				var context = new ExecutionContext();
				var result = action.Execute(context);

				hideOnComplete = result != ActionResult.PreventHide;
			}
			else
			{
				ActionResult result = null;

				for (var i = 0; i < AccumulatedActions.Count; i++)
				{
					var context = new ExecutionContext
					{
						LastActionResult = result,
						HasNextAction = true,
					};

					result = AccumulatedActions[i].Execute(context);
				}

				action.Execute(new ExecutionContext
				{
					LastActionResult = result,
					HasNextAction = false,
				});
			}

			if (hideOnComplete)
			{
				OnChangeVisibilityRequested(false);
			}
		}

		public void UpdateLastActiveWindowHandle()
		{
			_lastActiveWindowHandle = NativeMethods.GetForegroundWindow();
		}

		protected virtual void OnChangeVisibilityRequested(bool isVisible)
		{
			var handler = ChangeVisibilityRequested;

			if (handler != null)
			{
				handler(this, new ChangeVisibilityEventArgs { IsVisible = isVisible });
			}
		}

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;

			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}