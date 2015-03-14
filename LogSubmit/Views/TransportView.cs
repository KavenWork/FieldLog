﻿using System;
using System.Linq;
using System.Windows.Forms;
using Unclassified.LogSubmit.Transports;
using Unclassified.TxLib;
using Unclassified.UI;

namespace Unclassified.LogSubmit.Views
{
	public partial class TransportView : UserControl, IView
	{
		#region Private data

		private FileTransportView fileTransportView = new FileTransportView();
		private MailTransportView mailTransportView = new MailTransportView();

		#endregion Private data

		#region Constructors

		public TransportView()
		{
			InitializeComponent();

			TxDictionaryBinding.AddTextBindings(this);

			Dock = DockStyle.Fill;

			if (mailTransportView.Transport.CanExecute())
			{
				TransportComboBox.Items.Add(new TransportMethod { Text = Tx.T("transport.mail"), View = mailTransportView, Transport = mailTransportView.Transport });
			}
			TransportComboBox.Items.Add(new TransportMethod { Text = Tx.T("transport.file"), View = fileTransportView, Transport = fileTransportView.Transport });
			TransportComboBox.SelectedIndex = 0;
		}

		#endregion Constructors

		#region Event handlers

		private void TransportView_FontChanged(object sender, EventArgs e)
		{
			UIPreferences.UpdateFont(fileTransportView, fileTransportView.Font, Font);
			UIPreferences.UpdateFont(mailTransportView, mailTransportView.Font, Font);
		}

		#endregion Event handlers

		#region Control event handlers

		private void TransportComboBox_SelectedIndexChanged(object sender, EventArgs args)
		{
			TransportMethod method = (TransportMethod) TransportComboBox.SelectedItem;
			SetView(method.View);
			SharedData.Instance.Transport = method.Transport;
		}

		#endregion Control event handlers

		#region Public methods

		public void Activate(bool forward)
		{
			Control view = GetView();
			if (view != null) ((IView) view).Activate(forward);
		}

		public void Deactivate(bool forward)
		{
			Control view = GetView();
			if (view != null) ((IView) view).Deactivate(forward);
		}

		#endregion Public methods

		#region Private methods

		private Control GetView()
		{
			if (ContentPanel.Controls.Count == 0) return null;
			return ContentPanel.Controls[0];
		}

		private void SetView(Control view)
		{
			Control oldView = GetView();
			if (oldView != null)
			{
				if (view == oldView) return;
				((IView) oldView).Deactivate(true);
			}
			ContentPanel.Controls.Clear();

			ContentPanel.Controls.Add(view);
			if (MainForm.Instance != null)
			{
				((IView) view).Activate(true);
			}
		}

		#endregion Private methods

		#region Classes

		private class TransportMethod
		{
			public string Text { get; set; }
			public Control View { get; set; }
			public TransportBase Transport { get; set; }

			public override string ToString()
			{
				return Text;
			}
		}

		#endregion Classes
	}
}
