﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using Unclassified.UI;

namespace Unclassified.FieldLogViewer.ViewModel
{
	class FilterViewModel : ViewModelBase
	{
		#region Private data

		private string fixedDisplayName;

		#endregion Private data

		#region Constructors

		public FilterViewModel()
		{
			ConditionGroups = new ObservableCollection<FilterConditionGroupViewModel>();

			InitializeCommands();
		}

		public FilterViewModel(bool acceptAll)
			: this()
		{
			AcceptAll = acceptAll;

			if (AcceptAll)
			{
				fixedDisplayName = "Show all";
				DisplayName = fixedDisplayName;
			}
		}

		#endregion Constructors

		#region Event handlers

		private bool isLoading;

		private void ConditionGroups_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (ConditionGroups.Count > 0)
			{
				UpdateFirstStatus();
				OnFilterChanged(true);
			}
			else if (!isLoading)
			{
				Dispatcher.CurrentDispatcher.BeginInvoke((Action) OnCreateConditionGroup, DispatcherPriority.Normal);
			}
		}

		private void UpdateFirstStatus()
		{
			bool isFirst = true;
			foreach (var cg in ConditionGroups)
			{
				cg.IsFirst = isFirst;
				isFirst = false;
			}
		}

		#endregion Event handlers

		#region Commands

		public DelegateCommand CreateConditionGroupCommand { get; private set; }

		private void InitializeCommands()
		{
			CreateConditionGroupCommand = new DelegateCommand(OnCreateConditionGroup);
		}

		private void OnCreateConditionGroup()
		{
			FilterConditionGroupViewModel cg = new FilterConditionGroupViewModel(this);
			cg.CreateConditionCommand.Execute();
			ConditionGroups.Add(cg);
		}

		#endregion Commands

		#region Data properties

		private ObservableCollection<FilterConditionGroupViewModel> conditionGroups;
		public ObservableCollection<FilterConditionGroupViewModel> ConditionGroups
		{
			get { return conditionGroups; }
			private set
			{
				if (CheckUpdate(value, ref conditionGroups, "ConditionGroups"))
				{
					ConditionGroups.CollectionChanged += ConditionGroups_CollectionChanged;
					UpdateFirstStatus();
				}
			}
		}

		public bool AcceptAll { get; private set; }

		#endregion Data properties

		#region Loading and saving

		public void LoadFromString(string data)
		{
			isLoading = true;
			ConditionGroups.Clear();
			IEnumerable<string> lines = data.Split('\n').Select(s => s.Trim('\r'));
			List<string> lineBuffer = new List<string>();
			bool haveName = false;
			foreach (string line in lines)
			{
				if (!haveName)
				{
					// The first line contains only the filter name
					DisplayName = line;
					haveName = true;
				}
				else
				{
					if (lineBuffer.Count > 0 && line.StartsWith("or,"))
					{
						// Load buffer
						FilterConditionGroupViewModel grp = new FilterConditionGroupViewModel(this);
						grp.LoadFromString(lineBuffer);
						ConditionGroups.Add(grp);
						lineBuffer.Clear();
					}
					// Save line to buffer
					lineBuffer.Add(line);
				}
			}
			// Load buffer
			FilterConditionGroupViewModel grp2 = new FilterConditionGroupViewModel(this);
			grp2.LoadFromString(lineBuffer);
			ConditionGroups.Add(grp2);
			isLoading = false;
		}

		public string SaveToString()
		{
			return DisplayName + Environment.NewLine +
				ConditionGroups.Select(c => c.SaveToString()).Aggregate((a, b) => a + Environment.NewLine + b);
		}

		#endregion Loading and saving

		#region Change notification

		/// <summary>
		/// Raised when the filter definition has changed.
		/// </summary>
		public event Action<bool> FilterChanged;

		/// <summary>
		/// Raises the FilterChanged event.
		/// </summary>
		public void OnFilterChanged(bool affectsItems)
		{
			var handler = FilterChanged;
			if (handler != null)
			{
				handler(affectsItems);
			}
		}

		protected override void OnDisplayNameChanged()
		{
			if (AcceptAll)
			{
				// The accept-all filter cannot be renamed. Revert the change.
				TaskHelper.WhenLoaded(() => { DisplayName = fixedDisplayName; });
			}
			else
			{
				OnFilterChanged(false);
			}
		}

		#endregion Change notification

		#region Filter logic

		/// <summary>
		/// Determines whether the specified log item matches any condition group of this filter.
		/// </summary>
		/// <param name="item">The log item to evaluate.</param>
		/// <returns></returns>
		public bool IsMatch(FieldLogItemViewModel item)
		{
			if (AcceptAll) return true;
			
			return ConditionGroups.Any(c => c.IsMatch(item));
		}

		#endregion Filter logic

		#region Create new

		public static FilterViewModel CreateNew()
		{
			FilterViewModel newFilter = new FilterViewModel();
			newFilter.DisplayName = DateTime.Now.ToString();
			FilterConditionGroupViewModel newGroup = new FilterConditionGroupViewModel(newFilter);
			newGroup.Conditions.Add(new FilterConditionViewModel(newGroup));
			newFilter.ConditionGroups.Add(newGroup);
			return newFilter;
		}

		#endregion Create new

		#region Duplicate

		public FilterViewModel GetDuplicate()
		{
			FilterViewModel newFilter = new FilterViewModel();
			newFilter.DisplayName = this.DisplayName + " (copy)";
			newFilter.ConditionGroups = new ObservableCollection<FilterConditionGroupViewModel>(ConditionGroups.Select(cg => cg.GetDuplicate(newFilter)));
			return newFilter;
		}

		#endregion Duplicate
	}
}
