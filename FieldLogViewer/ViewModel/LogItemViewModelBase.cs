﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unclassified.FieldLogViewer.ViewModel
{
	class LogItemViewModelBase : ViewModelBase, IComparable<LogItemViewModelBase>
	{
		public int EventCounter { get; set; }
		public DateTime Time { get; set; }

		private int indentLevel;
		public int IndentLevel
		{
			get { return indentLevel; }
			set { CheckUpdate(value, ref indentLevel, "IndentLevel"); }
		}

		public virtual int CompareTo(LogItemViewModelBase other)
		{
			// First compare the items by time
			int i = this.Time.CompareTo(other.Time);
			if (i != 0) return i;

			// If the time is equal, consider the event counter value and handle overflows
			const int range = 10000;
			if (EventCounter > int.MaxValue - range && other.EventCounter < int.MinValue + range)
			{
				// Overflow, other is newer
				return -1;
			}
			if (EventCounter < int.MinValue + range && other.EventCounter > int.MaxValue - range)
			{
				// Overflow, other is older
				return 1;
			}
			return EventCounter.CompareTo(other.EventCounter);
		}
	}
}
