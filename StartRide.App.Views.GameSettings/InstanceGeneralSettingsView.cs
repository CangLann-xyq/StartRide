using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace StartRide.App.Views.GameSettings;

public partial class InstanceGeneralSettingsView : UserControl, IComponentConnector
{
	public InstanceGeneralSettingsView()
	{
		InitializeComponent();
	}

	private void DescriptionTextBox_OnLoaded(object sender, RoutedEventArgs e)
	{
		QueueDescriptionTextBoxHeightUpdate();
	}

	private void DescriptionTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		QueueDescriptionTextBoxHeightUpdate();
	}

	private void DescriptionTextBox_OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (e.WidthChanged)
		{
			QueueDescriptionTextBoxHeightUpdate();
		}
	}

	private void QueueDescriptionTextBoxHeightUpdate()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(UpdateDescriptionTextBoxHeight), (DispatcherPriority)4, Array.Empty<object>());
	}

	private void UpdateDescriptionTextBoxHeight()
	{
		DescriptionTextBox.UpdateLayout();
		int num = Math.Max(1, DescriptionTextBox.LineCount);
		DescriptionTextBox.VerticalContentAlignment = ((num <= 1) ? VerticalAlignment.Center : VerticalAlignment.Top);
		double num2 = TextBlock.GetLineHeight((DependencyObject)(object)DescriptionTextBox);
		if (double.IsNaN(num2) || num2 <= 0.0)
		{
			num2 = Math.Max(DescriptionTextBox.FontFamily.LineSpacing * DescriptionTextBox.FontSize, DescriptionTextBox.FontSize * 1.35);
		}
		double num3 = 16.0;
		DescriptionTextBox.Height = Math.Max(34.0, Math.Ceiling((double)num * num2 + num3));
	}

}
