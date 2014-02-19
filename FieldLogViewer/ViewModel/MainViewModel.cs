﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using TaskDialogInterop;
using Unclassified.FieldLog;
using Unclassified.FieldLogViewer.View;
using Unclassified.UI;

namespace Unclassified.FieldLogViewer.ViewModel
{
	class MainViewModel : ViewModelBase, IViewCommandSource
	{
		#region Static data

		public static MainViewModel Instance { get; private set; }

		#endregion Static data

		#region Private data

		private Dispatcher dispatcher;
		private ObservableCollection<LogItemViewModelBase> logItems = new ObservableCollection<LogItemViewModelBase>();
		private CollectionViewSource sortedFilters = new CollectionViewSource();
		private CollectionViewSource filteredLogItems = new CollectionViewSource();
		private string loadedBasePath;
		private FieldLogFileGroupReader logFileGroupReader;
		private bool isLiveStopped = true;
		private DateTime insertingItemsSince;
		private Task readerTask;
		private FilterConditionViewModel adhocFilterCondition;
		
		/// <summary>
		/// Buffer for all read items that are collected in the separate Task thread and then
		/// pushed to the UI thread as a new ObservableCollection instance.
		/// </summary>
		private List<LogItemViewModelBase> localLogItems;
		/// <summary>
		/// Synchronises access to the localLogItems variable.
		/// </summary>
		private ReaderWriterLockSlim localLogItemsLock = new ReaderWriterLockSlim();
		/// <summary>
		/// The number of new log items queued for inserting in the UI thread. Must always be
		/// accessed with the Interlocked class.
		/// </summary>
		private int queuedNewItemsCount;
		private AutoResetEvent returnToLocalLogItemsList = new AutoResetEvent(false);

		#endregion Private data

		#region Constructors

		public MainViewModel()
		{
			Instance = this;
			dispatcher = Dispatcher.CurrentDispatcher;

			InitializeCommands();

			UpdateWindowTitle();

			this.BindProperty(vm => vm.IsDebugMonitorActive, AppSettings.Instance, s => s.IsDebugMonitorActive);
			this.BindProperty(vm => vm.ShowRelativeTime, AppSettings.Instance, s => s.ShowRelativeTime);
			this.BindProperty(vm => vm.IsLiveScrollingEnabled, AppSettings.Instance, s => s.IsLiveScrollingEnabled);
			this.BindProperty(vm => vm.IsFlashingEnabled, AppSettings.Instance, s => s.IsFlashingEnabled);
			this.BindProperty(vm => vm.IsSoundEnabled, AppSettings.Instance, s => s.IsSoundEnabled);
			this.BindProperty(vm => vm.IsWindowOnTop, AppSettings.Instance, s => s.IsWindowOnTop);
			this.BindProperty(vm => vm.IndentSize, AppSettings.Instance, s => s.IndentSize);
			this.BindProperty(vm => vm.ItemTimeMode, AppSettings.Instance, s => s.ItemTimeMode);
			
			Filters = new ObservableCollection<FilterViewModel>();
			Filters.ForNewOld(
				f => f.FilterChanged += LogItemsFilterChanged,
				f => f.FilterChanged -= LogItemsFilterChanged);
			Filters.Add(new FilterViewModel(true));
			Filters.CollectionChanged += (s, e) =>
			{
				// Trigger saving the new filter collection.
				// Wait a moment or the new filter will appear twice in the filter lists until
				// something else has changed and we probably come here again. (Unsure why.)
				TaskHelper.WhenLoaded(() => LogItemsFilterChanged(false));
			};

			foreach (string s in AppSettings.Instance.Filters)
			{
				FilterViewModel f = new FilterViewModel();
				try
				{
					f.LoadFromString(s);
				}
				catch (Exception ex)
				{
					MessageBox.Show(
						"A filter could not be restored from the settings.\n" + ex.Message,
						"Error",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					continue;
				}
				Filters.Add(f);
			}

			// If no filter is defined, create some basic filters for a start
			if (Filters.Count == 1)
			{
				// Only the "show all" filter is present
				CreateBasicFilters();
			}
			
			FilterViewModel selectedFilterVM = Filters.FirstOrDefault(f => f.DisplayName == AppSettings.Instance.SelectedFilter);
			if (selectedFilterVM != null)
			{
				SelectedFilter = selectedFilterVM;
			}
			else
			{
				SelectedFilter = Filters[0];
			}

			sortedFilters.Source = Filters;
			sortedFilters.SortDescriptions.Add(new SortDescription("AcceptAll", ListSortDirection.Descending));
			sortedFilters.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));

			filteredLogItems.Source = logItems;
			filteredLogItems.Filter += filteredLogItems_Filter;

			DebugMonitor.MessageReceived += (pid, text) =>
			{
				var itemVM = new DebugMessageViewModel(pid, text);

				Interlocked.Increment(ref queuedNewItemsCount);
				dispatcher.BeginInvoke(
					new Action<LogItemViewModelBase>(this.InsertNewLogItem),
					itemVM);
			};
		}

		#endregion Constructors

		#region Public properties

		#endregion Public properties

		#region Commands

		public DelegateCommand LoadLogCommand { get; private set; }
		public DelegateCommand StopLiveCommand { get; private set; }
		public DelegateCommand ClearCommand { get; private set; }
		public DelegateCommand LoadMapCommand { get; private set; }
		public DelegateCommand DecreaseIndentSizeCommand { get; private set; }
		public DelegateCommand IncreaseIndentSizeCommand { get; private set; }
		public DelegateCommand ClearSearchTextCommand { get; private set; }
		public DelegateCommand SettingsCommand { get; private set; }

		private void InitializeCommands()
		{
			LoadLogCommand = new DelegateCommand(OnLoadLog, CanLoadLog);
			StopLiveCommand = new DelegateCommand(OnStopLive, CanStopLive);
			ClearCommand = new DelegateCommand(OnClear, CanClear);
			LoadMapCommand = new DelegateCommand(OnLoadMap);
			DecreaseIndentSizeCommand = new DelegateCommand(OnDecreaseIndentSize, CanDecreaseIndentSize);
			IncreaseIndentSizeCommand = new DelegateCommand(OnIncreaseIndentSize, CanIncreaseIndentSize);
			ClearSearchTextCommand = new DelegateCommand(OnClearSearchText);
			SettingsCommand = new DelegateCommand(OnSettings);
		}

		private void InvalidateCommands()
		{
			LoadLogCommand.RaiseCanExecuteChanged();
			StopLiveCommand.RaiseCanExecuteChanged();
			ClearCommand.RaiseCanExecuteChanged();
			LoadMapCommand.RaiseCanExecuteChanged();
			SettingsCommand.RaiseCanExecuteChanged();
		}

		private bool CanLoadLog()
		{
			return !IsLoadingFiles && !IsLoadingFilesAgain;
		}
		
		private void OnLoadLog()
		{
			OpenFileDialog dlg = new OpenFileDialog();
			if (dlg.ShowDialog() == true)
			{
				string prefix = GetPrefixFromPath(dlg.FileName);
				if (prefix != null)
				{
					if (CanStopLive())
					{
						OnStopLive();
					}
					OpenFiles(prefix);
				}
			}
		}

		private bool CanStopLive()
		{
			return !isLiveStopped;
		}

		private void OnStopLive()
		{
			if (logFileGroupReader != null)
			{
				logFileGroupReader.Close();
				isLiveStopped = true;
				StopLiveCommand.RaiseCanExecuteChanged();
			}
		}

		private bool CanClear()
		{
			return !IsLoadingFiles;
		}

		private void OnClear()
		{
			logItems.Clear();
		}

		private void OnLoadMap()
		{
		}

		private bool CanDecreaseIndentSize()
		{
			return IndentSize > 4;
		}

		private void OnDecreaseIndentSize()
		{
			IndentSize -= 4;
			if (IndentSize < 4)
			{
				IndentSize = 4;
			}
		}

		private bool CanIncreaseIndentSize()
		{
			return IndentSize < 32;
		}

		private void OnIncreaseIndentSize()
		{
			IndentSize += 4;
			if (IndentSize > 32)
			{
				IndentSize = 32;
			}
		}

		private void OnClearSearchText()
		{
			// Defer until after Render to make it look faster
			Dispatcher.CurrentDispatcher.BeginInvoke(
				new Action(() => AdhocSearchText = ""),
				DispatcherPriority.Background);
		}

		private void OnSettings()
		{
			SettingsWindow win = new SettingsWindow();
			SettingsViewModel vm = new SettingsViewModel();
			win.DataContext = vm;
			win.Owner = MainWindow.Instance;
			win.Show();
		}

		#endregion Commands

		#region Data properties

		public bool IsDebugMonitorActive
		{
			get { return DebugMonitor.IsActive; }
			set
			{
				if (value)
				{
					DebugMonitor.TryStart();
				}
				else
				{
					DebugMonitor.Stop();
				}
				OnPropertyChanged("IsDebugMonitorActive");
			}
		}

		private bool isLiveScrollingEnabled;
		public bool IsLiveScrollingEnabled
		{
			get { return isLiveScrollingEnabled; }
			set
			{
				if (CheckUpdate(value, ref isLiveScrollingEnabled, "IsLiveScrollingEnabled"))
				{
					if (isLiveScrollingEnabled)
					{
						ViewCommandManager.Invoke("ScrollToEnd");
					}
				}
			}
		}

		private bool isSoundEnabled;
		public bool IsSoundEnabled
		{
			get { return isSoundEnabled; }
			set { CheckUpdate(value, ref isSoundEnabled, "IsSoundEnabled"); }
		}

		private bool isFlashingEnabled;
		public bool IsFlashingEnabled
		{
			get { return isFlashingEnabled; }
			set { CheckUpdate(value, ref isFlashingEnabled, "IsFlashingEnabled"); }
		}

		public bool IsWindowOnTop
		{
			get
			{
				return MainWindow.Instance.Topmost;
			}
			set
			{
				if (value != MainWindow.Instance.Topmost)
				{
					MainWindow.Instance.Topmost = value;
					OnPropertyChanged("IsWindowOnTop");
				}
			}
		}

		public ObservableCollection<LogItemViewModelBase> LogItems
		{
			get { return this.logItems; }
		}

		public ICollectionView FilteredLogItems
		{
			get { return filteredLogItems.View; }
		}

		public ObservableCollection<FilterViewModel> Filters { get; private set; }

		public ICollectionView SortedFilters
		{
			get { return sortedFilters.View; }
		}

		private FilterViewModel selectedFilter;
		public FilterViewModel SelectedFilter
		{
			get { return selectedFilter; }
			set
			{
				if (CheckUpdate(value, ref selectedFilter, "SelectedFilter"))
				{
					ViewCommandManager.Invoke("SaveScrolling");
					RefreshLogItemsFilterView();
					ViewCommandManager.Invoke("RestoreScrolling");
					if (selectedFilter != null)
					{
						AppSettings.Instance.SelectedFilter = selectedFilter.DisplayName;
					}
					else
					{
						AppSettings.Instance.SelectedFilter = "";
					}
				}
			}
		}

		private string adhocSearchText;
		public string AdhocSearchText
		{
			get { return adhocSearchText; }
			set
			{
				if (CheckUpdate(value, ref adhocSearchText, "AdhocSearchText"))
				{
					if (!string.IsNullOrWhiteSpace(adhocSearchText))
					{
						adhocFilterCondition = new FilterConditionViewModel(null);
						adhocFilterCondition.Value = adhocSearchText;
					}
					else
					{
						adhocFilterCondition = null;
					}

					ViewCommandManager.Invoke("SaveScrolling");
					RefreshLogItemsFilterView();
					ViewCommandManager.Invoke("RestoreScrolling");
				}
			}
		}

		private bool isLoadingFiles;
		public bool IsLoadingFiles
		{
			get { return isLoadingFiles; }
			set
			{
				if (CheckUpdate(value, ref isLoadingFiles, "IsLoadingFiles"))
				{
					FL.TraceData("IsLoadingFiles", IsLoadingFiles);
					if (isLoadingFiles)
					{
						filteredLogItems.Source = null;
					}
					else
					{
						filteredLogItems.Source = logItems;
						RefreshLogItemsFilterView();
					}
					OnPropertyChanged("FilteredLogItems");
					OnPropertyChanged("LogItemsVisibility");
					OnPropertyChanged("ItemDetailsVisibility");
					OnPropertyChanged("LoadingMsgVisibility");
					InvalidateCommands();
				}
			}
		}

		private bool isLoadingFilesAgain;
		public bool IsLoadingFilesAgain
		{
			get { return isLoadingFilesAgain; }
			set
			{
				if (CheckUpdate(value, ref isLoadingFilesAgain, "IsLoadingFilesAgain"))
				{
					FL.TraceData("IsLoadingFilesAgain", IsLoadingFilesAgain);
					if (!isLoadingFilesAgain)
					{
						filteredLogItems.Source = logItems;
						RefreshLogItemsFilterView();
					}
					OnPropertyChanged("FilteredLogItems");
					InvalidateCommands();
				}
			}
		}

		private int loadedItemsCount;
		public int LoadedItemsCount
		{
			get { return loadedItemsCount; }
			set { CheckUpdate(value, ref loadedItemsCount, "LoadedItemsCount"); }
		}

		public Visibility LogItemsVisibility
		{
			get
			{
				return !IsLoadingFiles ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		public Visibility ItemDetailsVisibility
		{
			get
			{
				return !IsLoadingFiles ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		public Visibility LoadingMsgVisibility
		{
			get
			{
				return IsLoadingFiles ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		private int indentSize;
		public int IndentSize
		{
			get { return indentSize; }
			set
			{
				if (CheckUpdate(value, ref indentSize, "IndentSize"))
				{
					DecreaseIndentSizeCommand.RaiseCanExecuteChanged();
					IncreaseIndentSizeCommand.RaiseCanExecuteChanged();
				}
			}
		}

		private bool highlightSameThread = true;
		public bool HighlightSameThread
		{
			get { return highlightSameThread; }
			set { CheckUpdate(value, ref highlightSameThread, "HighlightSameThread"); }
		}

		private bool showRelativeTime;
		public bool ShowRelativeTime
		{
			get { return showRelativeTime; }
			set { CheckUpdate(value, ref showRelativeTime, "ShowRelativeTime"); }
		}

		private ItemTimeType itemTimeMode;
		public ItemTimeType ItemTimeMode
		{
			get { return itemTimeMode; }
			set
			{
				if (CheckUpdate(value, ref itemTimeMode, "ItemTimeMode"))
				{
					RefreshLogItemsFilterView();
				}
			}
		}

		public int SelectionDummy
		{
			get { return 0; }
		}

		#endregion Data properties

		#region Log items filter

		/// <summary>
		/// Filter implementation for the collection view returned by FilteredLogItems.
		/// </summary>
		private void filteredLogItems_Filter(object sender, FilterEventArgs e)
		{
			if (SelectedFilter != null)
			{
				e.Accepted =
					SelectedFilter.IsMatch(e.Item) &&
					(adhocFilterCondition == null || adhocFilterCondition.IsMatch(e.Item));
			}
			else
			{
				e.Accepted = true;
			}
		}

		public void LogItemsFilterChanged(bool affectsItems)
		{
			if (sortedFilters.View != null)
			{
				sortedFilters.View.Refresh();
			}
			if (affectsItems)
			{
				RefreshLogItemsFilterView();
			}
			AppSettings.Instance.Filters = Filters
				.Where(f => !f.AcceptAll)
				.Select(f => f.SaveToString())
				.Where(s => !string.IsNullOrEmpty(s))
				.ToArray();
		}

		public void RefreshLogItemsFilterView()
		{
			if (filteredLogItems.View != null)
			{
				filteredLogItems.View.Refresh();
			}
			ViewCommandManager.Invoke("UpdateDisplayTime");
		}

		#endregion Log items filter

		#region Log file loading

		/// <summary>
		/// Gets the log file prefix from a full file path.
		/// </summary>
		/// <param name="filePath">One of the log files.</param>
		/// <returns>The file's prefix, or null if it cannot be determined.</returns>
		public string GetPrefixFromPath(string filePath)
		{
			Match m = Regex.Match(filePath, @"^(.*)-[0-9]-[0-9]{18}\.fl$");
			if (m.Success)
			{
				return m.Groups[1].Value;
			}
			return null;
		}

		/// <summary>
		/// Opens the specified log files into the view.
		/// </summary>
		/// <param name="basePath">The base path of the log files to load.</param>
		/// <param name="singleFile">true to load a single file only. <paramref name="basePath"/> must be a full file name then.</param>
		public void OpenFiles(string basePath, bool singleFile = false)
		{
			if (basePath == null) throw new ArgumentNullException("basePath");
			if (basePath.Equals(FL.LogFileBasePath, StringComparison.InvariantCultureIgnoreCase))
			{
				MessageBox.Show(
					"You cannot open the log file that this instance of FieldLogViewer is currently writing to.\n\n" +
						"Trying to read the messages that may be generated while reading messages leads to a locking situation.",
					"Error",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			// First make sure any open reader is fully closed and won't send any new items anymore
			OnStopLive();
			if (readerTask != null)
			{
				readerTask.Wait();
				// Also process any queued operations like OnReadWaiting...
				TaskHelper.DoEvents();
			}

			ViewCommandManager.Invoke("StartedReadingFiles");
			IsLoadingFiles = true;

			this.logItems.Clear();

			isLiveStopped = false;
			StopLiveCommand.RaiseCanExecuteChanged();

			localLogItems = new List<LogItemViewModelBase>();

			loadedBasePath = basePath;
			UpdateWindowTitle();

			// Start the log file reading in a worker thread
			readerTask = Task.Factory.StartNew(() => ReadTask(basePath, singleFile));
		}

		/// <summary>
		/// Opens and reads the specified log files and pushes back all log items to the UI thread.
		/// This method is running in a worker thread.
		/// </summary>
		/// <param name="basePath">The base path of the log files to load.</param>
		/// <param name="singleFile">true to load a single file only. <paramref name="basePath"/> must be a full file name then.</param>
		private void ReadTask(string basePath, bool singleFile)
		{
			// Set current thread name to aid debugging
			Thread.CurrentThread.Name = "MainViewModel.ReadTask";
			
			// Setup and connect the wait handle that is set when all data has been read and we're
			// now waiting for more items to be written to the log files.
			EventWaitHandle readWaitHandle = new AutoResetEvent(false);
			readWaitHandle.WaitAction(
				() => dispatcher.Invoke((Action) OnReadWaiting),
				() => !isLiveStopped);

			// Create the log file group reader and read each next item
			logFileGroupReader = new FieldLogFileGroupReader(basePath, singleFile, readWaitHandle);
			logFileGroupReader.Error += logFileGroupReader_Error;
			List<FieldLogScopeItem> seenScopeItems = new List<FieldLogScopeItem>();
			while (true)
			{
				FieldLogItem item = logFileGroupReader.ReadLogItem();
				if (item == null)
				{
					// Signal the UI that this is it, no more items are coming. (Reader closed)
					readWaitHandle.Set();
					break;
				}
				FieldLogItemViewModel itemVM = FieldLogItemViewModel.Create(item);
				if (itemVM == null) break;   // Cannot happen actually

				var scopeItem = item as FieldLogScopeItem;
				if (scopeItem != null)
				{
					if (scopeItem.IsRepeated)
					{
						// Find existing scope item
						if (seenScopeItems.Any(si => si.SessionId == scopeItem.SessionId && si.EventCounter == scopeItem.EventCounter))
						{
							// Skip this item, we already have it from an earlier file
							continue;
						}
					}
					seenScopeItems.Add(scopeItem);
				}

				bool upgradedLock = false;
				localLogItemsLock.EnterUpgradeableReadLock();
				try
				{
					if (localLogItems == null)
					{
						if (returnToLocalLogItemsList.WaitOne(0))
						{
							FL.Trace("ReadTask: returnToLocalLogItemsList was set", "Waiting for the UI queue to clear before taking the list back.");

							// Wait for all queued items to be processed by the UI thread so that
							// the list is complete and no item is lost
							while (queuedNewItemsCount > 0)
							{
								Thread.Sleep(10);
							}
							// Ensure the items list is current when the queued counter is seen zero
							Thread.MemoryBarrier();

							localLogItemsLock.EnterWriteLock();
							upgradedLock = true;
							
							// Setup everything as if we were still reading an initial set of log
							// files and the the read Task thread use its local buffer again.
							using (FL.NewScope("Copying logItems to localLogItems"))
							{
								localLogItems = new List<LogItemViewModelBase>(logItems);
							}
							FL.Trace("ReadTask: took back the list");
						}
					}

					if (localLogItems != null)
					{
						if (!upgradedLock)
						{
							localLogItemsLock.EnterWriteLock();
							upgradedLock = true;
						}

						localLogItems.InsertSorted(itemVM, new Comparison<LogItemViewModelBase>((a, b) => a.CompareTo(b)));

						if ((localLogItems.Count % 5000) == 0)
						{
							int count = localLogItems.Count;
							FL.TraceData("localLogItems.Count", count);
							dispatcher.BeginInvoke(new Action(() => LoadedItemsCount = count));
						}
					}
					else
					{
						// Don't push a new item to the UI thread if there are currently more than
						// 20 items waiting to be processed.
						while (queuedNewItemsCount >= 20)
						{
							FL.Trace("Already too many items queued, waiting...");
							Thread.Sleep(10);
						}
						
						Interlocked.Increment(ref queuedNewItemsCount);
						dispatcher.BeginInvoke(
							new Action<LogItemViewModelBase>(this.InsertNewLogItem),
							itemVM);
					}
				}
				finally
				{
					if (upgradedLock)
						localLogItemsLock.ExitWriteLock();
					localLogItemsLock.ExitUpgradeableReadLock();
				}
			}
		}

		/// <summary>
		/// Handles an error while reading the log files.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void logFileGroupReader_Error(object sender, ErrorEventArgs e)
		{
			if (!dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke(
					new ErrorEventHandler(logFileGroupReader_Error),
					sender,
					e);
			}
			else
			{
				TaskDialogResult result = TaskDialog.Show(
					owner: MainWindow.Instance,
					allowDialogCancellation: true,
					title: "FieldLogViewer",
					mainInstruction: "An error occured while reading the log files.",
					content: "For details, including the exact problem and the offending file name and position, please open FieldLogViewer's log file from " +
						FL.LogFileBasePath + ".\n\n" +
						"If you continue reading, the loaded items may be incomplete or may not appear until you click the Stop button.",
					customButtons: new string[] { "Continue &reading", "&Cancel" });
				if (result.CustomButtonResult != 0)
				{
					OnStopLive();
				}
			}
		}

		/// <summary>
		/// Inserts a new log item from the read thread to the UI thread's items list.
		/// </summary>
		/// <param name="item">The new log item to insert.</param>
		private void InsertNewLogItem(LogItemViewModelBase item)
		{
			int newIndex = logItems.InsertSorted(item, (a, b) => a.CompareTo(b));
			int prevIndex;

			LoadedItemsCount = logItems.Count;

			// Check for new UtcOffset value
			bool newUtcOffset = false;
			FieldLogScopeItemViewModel scopeItem = item as FieldLogScopeItemViewModel;
			if (scopeItem != null &&
				scopeItem.Type == FieldLogScopeType.LogStart)
			{
				scopeItem.UtcOffset = (int) scopeItem.EnvironmentData.LocalTimeZoneOffset.TotalMinutes;
				newUtcOffset = true;
			}
			else
			{
				FieldLogTextItemViewModel textItem = item as FieldLogTextItemViewModel;
				if (textItem != null &&
					textItem.Details != null &&
					textItem.Details.StartsWith("\u0001UtcOffset="))
				{
					int i;
					if (int.TryParse(textItem.Details.Substring(11), out i))
					{
						// Read changed UTC offset from the generated text log item
						textItem.UtcOffset = i;
						newUtcOffset = true;
					}
				}
			}

			// IndentLevel is only supported for FieldLog items
			FieldLogItemViewModel flItem = item as FieldLogItemViewModel;
			if (flItem != null)
			{
				FieldLogScopeItemViewModel scope = item as FieldLogScopeItemViewModel;
				if (scope != null)
				{
					// Use new IndentLevel from Scope item
					if (scope.Type == FieldLogScopeType.Enter)
					{
						scope.IndentLevel = scope.Level - 1;
					}
					else
					{
						scope.IndentLevel = scope.Level;
					}
				}
				else
				{
					// Use IndentLevel of the previous item in the same session & thread
					prevIndex = newIndex - 1;
					while (prevIndex >= 0)
					{
						FieldLogItemViewModel prevFlItem = logItems[prevIndex] as FieldLogItemViewModel;
						if (prevFlItem != null &&
							prevFlItem.SessionId == flItem.SessionId &&
							prevFlItem.ThreadId == flItem.ThreadId)
						{
							FieldLogScopeItemViewModel prevScope = prevFlItem as FieldLogScopeItemViewModel;
							if (prevScope != null)
							{
								item.IndentLevel = prevScope.Level;
							}
							else
							{
								item.IndentLevel = prevFlItem.IndentLevel;
							}
							break;
						}
						prevIndex--;
					}
				}
			
				// Update all items after the inserted item
				for (int index = newIndex + 1; index < logItems.Count; index++)
				{
					FieldLogItemViewModel nextFlItem = logItems[index] as FieldLogItemViewModel;
					if (nextFlItem != null &&
						nextFlItem.SessionId == flItem.SessionId &&
						nextFlItem.ThreadId == flItem.ThreadId)
					{
						FieldLogScopeItemViewModel nextScope = nextFlItem as FieldLogScopeItemViewModel;
						if (nextScope != null)
						{
							// The next Scope item already had a reference level, stop here
							break;
						}
						nextFlItem.IndentLevel = flItem.IndentLevel;
					}
				}
				
				// Use LastLogStartItem and UtcOffset of the previous item from the same session
				prevIndex = newIndex - 1;
				while (prevIndex >= 0)
				{
					FieldLogItemViewModel prevFlItem = logItems[prevIndex] as FieldLogItemViewModel;
					if (prevFlItem != null &&
						prevFlItem.SessionId == flItem.SessionId)
					{
						flItem.LastLogStartItem = prevFlItem.LastLogStartItem;
						if (!newUtcOffset)
						{
							flItem.UtcOffset = prevFlItem.UtcOffset;
						}
						break;
					}
					prevIndex--;
				}
			}

			// Ensure the items list is current when the queued counter is decremented
			Thread.MemoryBarrier();
			if (queuedNewItemsCount == 1 && IsLoadingFilesAgain)
			{
				// We're about to hand off hte logItems list to the read thread. Make sure all
				// other queued events down to Input priority are processed to have a fluid UI.
				TaskHelper.DoEvents(DispatcherPriority.Input);
			}
			Interlocked.Decrement(ref queuedNewItemsCount);

			// Test whether the UI thread is locked because of reading too many log items at once
			if (insertingItemsSince == DateTime.MinValue)
			{
				FL.Trace("Setting insertingItemsSince");
				insertingItemsSince = DateTime.UtcNow;
				Dispatcher.CurrentDispatcher.BeginInvoke(
					new Action(() => { insertingItemsSince = DateTime.MinValue; FL.Trace("Resetting insertingItemsSince"); }),
					DispatcherPriority.Background);
			}
			if (DateTime.UtcNow > insertingItemsSince.AddMilliseconds(200))
			{
				FL.Trace("InsertNewLogItem: UI thread blocked for 200 ms", "Setting returnToLocalLogItemsList event");

				// Blocking the UI with inserting log items for 200 ms now.
				// Tell the read thread to stop sending new items to the UI thread separately, wait
				// for all items to be handled by the UI thread, and then take back the log items
				// ObservableCollection to a local List for faster inserting of many items.
				returnToLocalLogItemsList.Set();

				// The following actione still need to be performed by the UI thread
				//ViewCommandManager.Invoke("StartedReadingFiles");
				IsLoadingFilesAgain = true;

				isLiveStopped = false;
				StopLiveCommand.RaiseCanExecuteChanged();

				// Do not execute this block again as long as the UI thread is still blocked
				insertingItemsSince = insertingItemsSince.AddDays(1000);
			}
		}

		/// <summary>
		/// Called when the read wait handle has been set. All data has been read and we're now
		/// waiting for more items to be written to the log files. Until now, the read thread was
		/// adding new items to a local List for better performance. Now, this List is copied to
		/// an ObservableCollection, displayed and managed by the UI thread. From now on, new items
		/// will be posted to the UI thread separately for inserting in the items list, calling the
		/// InsertNewLogItem method.
		/// </summary>
		private void OnReadWaiting()
		{
			if (returnToLocalLogItemsList.WaitOne(0))
			{
				FL.Trace("OnReadWaiting: returnToLocalLogItemsList was set", "Reverting UI state, not touching log items lists.");
				
				// The UI thread has been busy inserting queued new items and has detected a long
				// blocking period. It has then decided to signal the read thread to go back to
				// inserting more items into a local List instead of the main ObservableCollection.
				// But the read thread has already finished reading existing items and has
				// indicated this by calling this method. So it won't actually go back to the local
				// items list because it doesn't currently have any new items to read. The UI
				// thread is still waiting for the read thread to return the list to the UI. This
				// needs to be resolved here.

				// returnToLocalLogItemsList is already reset just by testing it (AutoResetEvent).
				// logItems and localLogItems has not yet been touched, nothing to do with that.
				// Revert other UI state:
				IsLoadingFilesAgain = false;
				ViewCommandManager.Invoke("FinishedReadingFiles");
				return;
			}
			
			// Lock the local list so that no item loaded directly afterwards will get lost
			// while we're still preparing the loaded items list to be pushed to the UI
			localLogItemsLock.EnterReadLock();
			try
			{
				if (localLogItems == null) return;   // Nothing to do, just waiting once again in normal monitor mode
			}
			finally
			{
				localLogItemsLock.ExitReadLock();
			}
			FL.Trace("OnReadWaiting: EnterWriteLock");
			localLogItemsLock.EnterWriteLock();
			try
			{
				// Check again because we have released the lock since the last check
				if (localLogItems == null) return;   // Nothing to do, just waiting once again in normal monitor mode

				FL.Trace("Copying localLogItems list to UI thread, " + localLogItems.Count + " items");

				// Apply scope-based indenting and UtcOffset to all items now
				Dictionary<int, int> threadLevels = new Dictionary<int, int>();
				Dictionary<Guid, FieldLogScopeItemViewModel> logStartItems = new Dictionary<Guid, FieldLogScopeItemViewModel>();
				int utcOffset = 0;
				foreach (var item in localLogItems)
				{
					FieldLogScopeItemViewModel scope = item as FieldLogScopeItemViewModel;
					if (scope != null)
					{
						threadLevels[scope.ThreadId] = scope.Level;
						if (scope.Type == FieldLogScopeType.Enter)
						{
							scope.IndentLevel = scope.Level - 1;
						}
						else
						{
							scope.IndentLevel = scope.Level;
						}

						if (scope.Type == FieldLogScopeType.LogStart)
						{
							logStartItems[scope.SessionId] = scope;
							utcOffset = (int) scope.EnvironmentData.LocalTimeZoneOffset.TotalMinutes;
						}
						scope.UtcOffset = utcOffset;
					}
					else
					{
						FieldLogTextItemViewModel textItem = item as FieldLogTextItemViewModel;
						if (textItem != null &&
							textItem.Details != null &&
							textItem.Details.StartsWith("\u0001UtcOffset="))
						{
							int i;
							if (int.TryParse(textItem.Details.Substring(11), out i))
							{
								// Read changed UTC offset from the generated text log item
								utcOffset = i;
							}
						}
						
						FieldLogItemViewModel flItem = item as FieldLogItemViewModel;
						if (flItem != null)
						{
							int level;
							if (threadLevels.TryGetValue(flItem.ThreadId, out level))
							{
								flItem.IndentLevel = level;
							}
							flItem.UtcOffset = utcOffset;
						}
					}
				}
				foreach (var item in localLogItems)
				{
					FieldLogItemViewModel flItem = item as FieldLogItemViewModel;
					if (flItem != null)
					{
						FieldLogScopeItemViewModel scope;
						if (logStartItems.TryGetValue(flItem.SessionId, out scope))
						{
							flItem.LastLogStartItem = scope;
						}
					}
				}

				// Publish loaded items to the UI
				using (FL.NewScope("Copying localLogItems to logItems"))
				{
					this.logItems = new ObservableCollection<LogItemViewModelBase>(localLogItems);
				}
				localLogItems = null;
			}
			finally
			{
				localLogItemsLock.ExitWriteLock();
			}
			// Notify the UI to make it show the new list of items.
			// From now on, newly loaded items are added one by one to the collection that
			// is already bound to the UI, so the new items will become visible.
			OnPropertyChanged("LogItems");
			LoadedItemsCount = logItems.Count;
			if (IsLoadingFiles)
			{
				IsLoadingFiles = false;
				UpdateWindowTitle();
				ViewCommandManager.Invoke("FinishedReadingFiles");
			}
			if (IsLoadingFilesAgain)
			{
				IsLoadingFilesAgain = false;
				ViewCommandManager.InvokeLoaded("FinishedReadingFilesAgain");
			}
		}

		#endregion Log file loading

		#region Other methods

		private void CreateBasicFilters()
		{
			FilterViewModel f;
			FilterConditionGroupViewModel fcg;
			FilterConditionViewModel fc;

			f = new FilterViewModel();
			f.DisplayName = "Errors and up";
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Priority;
			fc.Comparison = FilterComparison.GreaterOrEqual;
			fc.Value = FieldLogPriority.Error.ToString();
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.AnyText;
			fc.Comparison = FilterComparison.Contains;
			fc.Value = "error";
			fcg.Conditions.Add(fc);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Priority;
			fc.Comparison = FilterComparison.GreaterOrEqual;
			fc.Value = FieldLogPriority.Info.ToString();
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			Filters.Add(f);

			f = new FilterViewModel();
			f.DisplayName = "Warnings and up";
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Priority;
			fc.Comparison = FilterComparison.GreaterOrEqual;
			fc.Value = FieldLogPriority.Warning.ToString();
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.AnyText;
			fc.Comparison = FilterComparison.Contains;
			fc.Value = "warning";
			fcg.Conditions.Add(fc);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Priority;
			fc.Comparison = FilterComparison.GreaterOrEqual;
			fc.Value = FieldLogPriority.Info.ToString();
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.AnyText;
			fc.Comparison = FilterComparison.Contains;
			fc.Value = "error";
			fcg.Conditions.Add(fc);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Priority;
			fc.Comparison = FilterComparison.GreaterOrEqual;
			fc.Value = FieldLogPriority.Info.ToString();
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			Filters.Add(f);

			f = new FilterViewModel();
			f.DisplayName = "Relevant exceptions";
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Type;
			fc.Comparison = FilterComparison.Equals;
			fc.Value = FieldLogItemType.Exception.ToString();
			fcg.Conditions.Add(fc);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.ExceptionContext;
			fc.Comparison = FilterComparison.NotEquals;
			fc.Value = "AppDomain.FirstChanceException";
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			Filters.Add(f);

			f = new FilterViewModel();
			f.DisplayName = "No trace";
			fcg = new FilterConditionGroupViewModel(f);
			fc = new FilterConditionViewModel(fcg);
			fc.Column = FilterColumn.Priority;
			fc.Comparison = FilterComparison.GreaterOrEqual;
			fc.Value = FieldLogPriority.Checkpoint.ToString();
			fcg.Conditions.Add(fc);
			f.ConditionGroups.Add(fcg);
			Filters.Add(f);
		}

		private void UpdateWindowTitle()
		{
			string prefix = Path.GetFileName(loadedBasePath);
			string dir = Path.GetDirectoryName(loadedBasePath);

			if (IsLoadingFiles)
			{
				DisplayName = "Loading " + prefix + " in " + dir + "… – FieldLogViewer";
			}
			else if (loadedBasePath != null)
			{
				DisplayName = prefix + " in " + dir + " – FieldLogViewer";
			}
			else
			{
				DisplayName = "FieldLogViewer";
			}
		}

		#endregion Other methods

		#region IViewCommandSource members

		private ViewCommandManager viewCommandManager = new ViewCommandManager();
		public ViewCommandManager ViewCommandManager { get { return this.viewCommandManager; } }

		#endregion IViewCommandSource members
	}
}
