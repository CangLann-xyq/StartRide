using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace Launcher.App.Controls;

public partial class LaunchSettingsEditor : UserControl, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty ShowModeSelectorProperty;

	public static readonly DependencyProperty LaunchSettingsModeOptionsProperty;

	public static readonly DependencyProperty SelectedLaunchSettingsModeOptionProperty;

	public static readonly DependencyProperty AreLaunchSettingsOverridesEnabledProperty;

	public static readonly DependencyProperty ShowMemorySettingsProperty;

	public static readonly DependencyProperty MemorySettingsModeOptionsProperty;

	public static readonly DependencyProperty SelectedMemorySettingsModeOptionProperty;

	public static readonly DependencyProperty MemoryMbProperty;

	public static readonly DependencyProperty MemoryMinimumMbProperty;

	public static readonly DependencyProperty MemoryMaximumMbProperty;

	public static readonly DependencyProperty MemoryStepMbProperty;

	public static readonly DependencyProperty IsMemorySliderEnabledProperty;

	public static readonly DependencyProperty IsMemorySliderVisibleProperty;

	public static readonly DependencyProperty IsAutomaticMemorySummaryVisibleProperty;

	public static readonly DependencyProperty AutomaticMemoryTextProperty;

	public static readonly DependencyProperty MemoryValueTextProperty;

	public static readonly DependencyProperty SystemTotalMemoryTextProperty;

	public static readonly DependencyProperty SystemAvailableMemoryTextProperty;

	public static readonly DependencyProperty SystemMemorySummaryTextProperty;

	public static readonly DependencyProperty CanEditAutoRepairMissingFilesProperty;

	public static readonly DependencyProperty LaunchCheckFilesBeforeLaunchEnabledProperty;

	public static readonly DependencyProperty LaunchAutoRepairMissingFilesEnabledProperty;

	public static readonly DependencyProperty LaunchMinimizeLauncherAfterLaunchEnabledProperty;

	public static readonly DependencyProperty LaunchFullScreenEnabledProperty;

	public static readonly DependencyProperty LaunchAutoJoinServerAddressProperty;

	public static readonly DependencyProperty LaunchPreLaunchCommandProperty;

	public static readonly DependencyProperty LaunchWaitForPreLaunchCommandProperty;

	public static readonly DependencyProperty LaunchPostExitCommandProperty;

	public static readonly DependencyProperty LaunchJvmArgumentsProperty;

	public static readonly DependencyProperty LaunchGameArgumentsProperty;

	public bool ShowModeSelector
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(ShowModeSelectorProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ShowModeSelectorProperty, (object)value);
		}
	}

	public IEnumerable? LaunchSettingsModeOptions
	{
		get
		{
			return (IEnumerable)((DependencyObject)this).GetValue(LaunchSettingsModeOptionsProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchSettingsModeOptionsProperty, (object)value);
		}
	}

	public object? SelectedLaunchSettingsModeOption
	{
		get
		{
			return ((DependencyObject)this).GetValue(SelectedLaunchSettingsModeOptionProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SelectedLaunchSettingsModeOptionProperty, value);
		}
	}

	public bool AreLaunchSettingsOverridesEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(AreLaunchSettingsOverridesEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(AreLaunchSettingsOverridesEnabledProperty, (object)value);
		}
	}

	public bool ShowMemorySettings
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(ShowMemorySettingsProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ShowMemorySettingsProperty, (object)value);
		}
	}

	public IEnumerable? MemorySettingsModeOptions
	{
		get
		{
			return (IEnumerable)((DependencyObject)this).GetValue(MemorySettingsModeOptionsProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MemorySettingsModeOptionsProperty, (object)value);
		}
	}

	public object? SelectedMemorySettingsModeOption
	{
		get
		{
			return ((DependencyObject)this).GetValue(SelectedMemorySettingsModeOptionProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SelectedMemorySettingsModeOptionProperty, value);
		}
	}

	public double MemoryMb
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(MemoryMbProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MemoryMbProperty, (object)value);
		}
	}

	public double MemoryMinimumMb
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(MemoryMinimumMbProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MemoryMinimumMbProperty, (object)value);
		}
	}

	public double MemoryMaximumMb
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(MemoryMaximumMbProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MemoryMaximumMbProperty, (object)value);
		}
	}

	public double MemoryStepMb
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(MemoryStepMbProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MemoryStepMbProperty, (object)value);
		}
	}

	public bool IsMemorySliderEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsMemorySliderEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsMemorySliderEnabledProperty, (object)value);
		}
	}

	public bool IsMemorySliderVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsMemorySliderVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsMemorySliderVisibleProperty, (object)value);
		}
	}

	public bool IsAutomaticMemorySummaryVisible
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsAutomaticMemorySummaryVisibleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsAutomaticMemorySummaryVisibleProperty, (object)value);
		}
	}

	public string AutomaticMemoryText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(AutomaticMemoryTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(AutomaticMemoryTextProperty, (object)value);
		}
	}

	public string MemoryValueText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(MemoryValueTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MemoryValueTextProperty, (object)value);
		}
	}

	public string SystemTotalMemoryText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SystemTotalMemoryTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SystemTotalMemoryTextProperty, (object)value);
		}
	}

	public string SystemAvailableMemoryText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SystemAvailableMemoryTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SystemAvailableMemoryTextProperty, (object)value);
		}
	}

	public string SystemMemorySummaryText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(SystemMemorySummaryTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SystemMemorySummaryTextProperty, (object)value);
		}
	}

	public bool CanEditAutoRepairMissingFiles
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(CanEditAutoRepairMissingFilesProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(CanEditAutoRepairMissingFilesProperty, (object)value);
		}
	}

	public bool LaunchCheckFilesBeforeLaunchEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(LaunchCheckFilesBeforeLaunchEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchCheckFilesBeforeLaunchEnabledProperty, (object)value);
		}
	}

	public bool LaunchAutoRepairMissingFilesEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(LaunchAutoRepairMissingFilesEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchAutoRepairMissingFilesEnabledProperty, (object)value);
		}
	}

	public bool LaunchMinimizeLauncherAfterLaunchEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(LaunchMinimizeLauncherAfterLaunchEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchMinimizeLauncherAfterLaunchEnabledProperty, (object)value);
		}
	}

	public bool LaunchFullScreenEnabled
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(LaunchFullScreenEnabledProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchFullScreenEnabledProperty, (object)value);
		}
	}

	public string LaunchAutoJoinServerAddress
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(LaunchAutoJoinServerAddressProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchAutoJoinServerAddressProperty, (object)value);
		}
	}

	public string LaunchPreLaunchCommand
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(LaunchPreLaunchCommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchPreLaunchCommandProperty, (object)value);
		}
	}

	public bool LaunchWaitForPreLaunchCommand
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(LaunchWaitForPreLaunchCommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchWaitForPreLaunchCommandProperty, (object)value);
		}
	}

	public string LaunchPostExitCommand
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(LaunchPostExitCommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchPostExitCommandProperty, (object)value);
		}
	}

	public string LaunchJvmArguments
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(LaunchJvmArgumentsProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchJvmArgumentsProperty, (object)value);
		}
	}

	public string LaunchGameArguments
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(LaunchGameArgumentsProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(LaunchGameArgumentsProperty, (object)value);
		}
	}

	public LaunchSettingsEditor()
	{
		InitializeComponent();
	}

	private void AdvancedLaunchTextBox_OnLoaded(object sender, RoutedEventArgs e)
	{
		QueueAdvancedLaunchTextBoxHeightUpdate(sender as TextBox);
	}

	private void AdvancedLaunchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		QueueAdvancedLaunchTextBoxHeightUpdate(sender as TextBox);
	}

	private void AdvancedLaunchTextBox_OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (e.WidthChanged)
		{
			QueueAdvancedLaunchTextBoxHeightUpdate(sender as TextBox);
		}
	}

	private void QueueAdvancedLaunchTextBoxHeightUpdate(TextBox? textBox)
	{
		if (textBox != null)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				UpdateAdvancedLaunchTextBoxHeight(textBox);
			}, (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private static void UpdateAdvancedLaunchTextBoxHeight(TextBox textBox)
	{
		textBox.UpdateLayout();
		int num = Math.Max(1, textBox.LineCount);
		textBox.VerticalContentAlignment = ((num <= 1) ? VerticalAlignment.Center : VerticalAlignment.Top);
		double num2 = TextBlock.GetLineHeight((DependencyObject)(object)textBox);
		if (double.IsNaN(num2) || num2 <= 0.0)
		{
			num2 = Math.Max(textBox.FontFamily.LineSpacing * textBox.FontSize, textBox.FontSize * 1.35);
		}
		double num3 = 16.0;
		textBox.Height = Math.Max(34.0, Math.Ceiling((double)num * num2 + num3));
	}


	static LaunchSettingsEditor()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Expected O, but got Unknown
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Expected O, but got Unknown
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Expected O, but got Unknown
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Expected O, but got Unknown
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Expected O, but got Unknown
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Expected O, but got Unknown
		//IL_02bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Expected O, but got Unknown
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Expected O, but got Unknown
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		//IL_0321: Expected O, but got Unknown
		//IL_0344: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Expected O, but got Unknown
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Expected O, but got Unknown
		//IL_039f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a9: Expected O, but got Unknown
		ShowModeSelectorProperty = DependencyProperty.Register("ShowModeSelector", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)true));
		LaunchSettingsModeOptionsProperty = DependencyProperty.Register("LaunchSettingsModeOptions", typeof(IEnumerable), typeof(LaunchSettingsEditor), new PropertyMetadata((PropertyChangedCallback)null));
		SelectedLaunchSettingsModeOptionProperty = DependencyProperty.Register("SelectedLaunchSettingsModeOption", typeof(object), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		AreLaunchSettingsOverridesEnabledProperty = DependencyProperty.Register("AreLaunchSettingsOverridesEnabled", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)true));
		ShowMemorySettingsProperty = DependencyProperty.Register("ShowMemorySettings", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)false));
		MemorySettingsModeOptionsProperty = DependencyProperty.Register("MemorySettingsModeOptions", typeof(IEnumerable), typeof(LaunchSettingsEditor), new PropertyMetadata((PropertyChangedCallback)null));
		SelectedMemorySettingsModeOptionProperty = DependencyProperty.Register("SelectedMemorySettingsModeOption", typeof(object), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		MemoryMbProperty = DependencyProperty.Register("MemoryMb", typeof(double), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)4096.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		MemoryMinimumMbProperty = DependencyProperty.Register("MemoryMinimumMb", typeof(double), typeof(LaunchSettingsEditor), new PropertyMetadata((object)1024.0));
		MemoryMaximumMbProperty = DependencyProperty.Register("MemoryMaximumMb", typeof(double), typeof(LaunchSettingsEditor), new PropertyMetadata((object)32768.0));
		MemoryStepMbProperty = DependencyProperty.Register("MemoryStepMb", typeof(double), typeof(LaunchSettingsEditor), new PropertyMetadata((object)512.0));
		IsMemorySliderEnabledProperty = DependencyProperty.Register("IsMemorySliderEnabled", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)false));
		IsMemorySliderVisibleProperty = DependencyProperty.Register("IsMemorySliderVisible", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)false));
		IsAutomaticMemorySummaryVisibleProperty = DependencyProperty.Register("IsAutomaticMemorySummaryVisible", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)false));
		AutomaticMemoryTextProperty = DependencyProperty.Register("AutomaticMemoryText", typeof(string), typeof(LaunchSettingsEditor), new PropertyMetadata((object)string.Empty));
		MemoryValueTextProperty = DependencyProperty.Register("MemoryValueText", typeof(string), typeof(LaunchSettingsEditor), new PropertyMetadata((object)string.Empty));
		SystemTotalMemoryTextProperty = DependencyProperty.Register("SystemTotalMemoryText", typeof(string), typeof(LaunchSettingsEditor), new PropertyMetadata((object)string.Empty));
		SystemAvailableMemoryTextProperty = DependencyProperty.Register("SystemAvailableMemoryText", typeof(string), typeof(LaunchSettingsEditor), new PropertyMetadata((object)string.Empty));
		SystemMemorySummaryTextProperty = DependencyProperty.Register("SystemMemorySummaryText", typeof(string), typeof(LaunchSettingsEditor), new PropertyMetadata((object)string.Empty));
		CanEditAutoRepairMissingFilesProperty = DependencyProperty.Register("CanEditAutoRepairMissingFiles", typeof(bool), typeof(LaunchSettingsEditor), new PropertyMetadata((object)true));
		LaunchCheckFilesBeforeLaunchEnabledProperty = DependencyProperty.Register("LaunchCheckFilesBeforeLaunchEnabled", typeof(bool), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchAutoRepairMissingFilesEnabledProperty = DependencyProperty.Register("LaunchAutoRepairMissingFilesEnabled", typeof(bool), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchMinimizeLauncherAfterLaunchEnabledProperty = DependencyProperty.Register("LaunchMinimizeLauncherAfterLaunchEnabled", typeof(bool), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchFullScreenEnabledProperty = DependencyProperty.Register("LaunchFullScreenEnabled", typeof(bool), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchAutoJoinServerAddressProperty = DependencyProperty.Register("LaunchAutoJoinServerAddress", typeof(string), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchPreLaunchCommandProperty = DependencyProperty.Register("LaunchPreLaunchCommand", typeof(string), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchWaitForPreLaunchCommandProperty = DependencyProperty.Register("LaunchWaitForPreLaunchCommand", typeof(bool), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchPostExitCommandProperty = DependencyProperty.Register("LaunchPostExitCommand", typeof(string), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchJvmArgumentsProperty = DependencyProperty.Register("LaunchJvmArguments", typeof(string), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
		LaunchGameArgumentsProperty = DependencyProperty.Register("LaunchGameArguments", typeof(string), typeof(LaunchSettingsEditor), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
	}
}
