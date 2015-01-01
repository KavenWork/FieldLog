﻿using System;
using System.Linq;
using System.Windows;

namespace Unclassified.FieldLogViewer.ViewModels
{
	internal class DetailsMessageViewModel : ViewModelBase
	{
		public string Title { get; set; }
		public string Message { get; set; }
		public string IconName { get; set; }
		public bool ShowAutoLoadCheckBox { get; set; }

		public DetailsMessageViewModel(string title, string message, string iconName)
		{
			Title = title;
			Message = message;
			IconName = iconName;
		}

		public DetailsMessageViewModel(string title, string message)
		{
			Title = title;
			Message = message;
		}

		public DetailsMessageViewModel(string title)
		{
			Title = title;
		}

		public Visibility ArrowLeftIconVisibility { get { return IconName == "ArrowLeft" ? Visibility.Visible : Visibility.Collapsed; } }
		public Visibility ArrowUpIconVisibility { get { return IconName == "ArrowUp" ? Visibility.Visible : Visibility.Collapsed; } }
		public Visibility FlashIconVisibility { get { return IconName == "Flash" ? Visibility.Visible : Visibility.Collapsed; } }

		public Visibility AutoLoadCheckBoxVisibility { get { return ShowAutoLoadCheckBox ? Visibility.Visible : Visibility.Collapsed; } }

		public bool AutoLoad
		{
			get { return MainViewModel.Instance.AutoLoadLog; }
			set { MainViewModel.Instance.AutoLoadLog = value; }
		}
	}
}
