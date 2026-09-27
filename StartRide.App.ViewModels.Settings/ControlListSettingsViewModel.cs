using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Settings;

public sealed class ControlListSettingsViewModel : SettingsSectionViewModelBase
{
	[ObservableProperty]
	private string controlDemoInputText = Strings.Settings_ControlDemoDefaultInput;

	[ObservableProperty]
	private string controlDemoMultilineText = Strings.Settings_ControlDemoDefaultMultilineInput;

	[ObservableProperty]
	private string controlDemoSearchText = string.Empty;

	[ObservableProperty]
	private string controlDemoStatusText = Strings.Settings_ControlDemoStatusReady;

	[ObservableProperty]
	private bool controlDemoSwitchEnabled = true;

	[ObservableProperty]
	private double controlDemoSliderValue = 48.0;

	[ObservableProperty]
	private bool controlDemoSecondaryMenuSelected = true;

	[ObservableProperty]
	private int controlDemoProgress = 64;

	[ObservableProperty]
	private SettingsInteractiveControlItem? selectedControlDemoComboOption;

	[ObservableProperty]
	private SettingsInteractiveControlItem? selectedInteractiveControl;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	private RelayCommand? runControlDemoActionCommand;

	public ObservableCollection<SettingsInteractiveControlItem> InteractiveControls { get; } = new ObservableCollection<SettingsInteractiveControlItem>();

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ControlDemoInputText
	{
		get
		{
			return controlDemoInputText;
		}
		[MemberNotNull("controlDemoInputText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(controlDemoInputText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoInputText);
				controlDemoInputText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoInputText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ControlDemoMultilineText
	{
		get
		{
			return controlDemoMultilineText;
		}
		[MemberNotNull("controlDemoMultilineText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(controlDemoMultilineText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoMultilineText);
				controlDemoMultilineText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoMultilineText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ControlDemoSearchText
	{
		get
		{
			return controlDemoSearchText;
		}
		[MemberNotNull("controlDemoSearchText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(controlDemoSearchText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoSearchText);
				controlDemoSearchText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoSearchText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public string ControlDemoStatusText
	{
		get
		{
			return controlDemoStatusText;
		}
		[MemberNotNull("controlDemoStatusText")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(controlDemoStatusText, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoStatusText);
				controlDemoStatusText = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoStatusText);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool ControlDemoSwitchEnabled
	{
		get
		{
			return controlDemoSwitchEnabled;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(controlDemoSwitchEnabled, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoSwitchEnabled);
				controlDemoSwitchEnabled = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoSwitchEnabled);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public double ControlDemoSliderValue
	{
		get
		{
			return controlDemoSliderValue;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(controlDemoSliderValue, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoSliderValue);
				controlDemoSliderValue = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoSliderValue);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public bool ControlDemoSecondaryMenuSelected
	{
		get
		{
			return controlDemoSecondaryMenuSelected;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(controlDemoSecondaryMenuSelected, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoSecondaryMenuSelected);
				controlDemoSecondaryMenuSelected = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoSecondaryMenuSelected);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public int ControlDemoProgress
	{
		get
		{
			return controlDemoProgress;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(controlDemoProgress, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ControlDemoProgress);
				controlDemoProgress = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ControlDemoProgress);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsInteractiveControlItem? SelectedControlDemoComboOption
	{
		get
		{
			return selectedControlDemoComboOption;
		}
		set
		{
			if (!EqualityComparer<SettingsInteractiveControlItem>.Default.Equals(selectedControlDemoComboOption, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedControlDemoComboOption);
				selectedControlDemoComboOption = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedControlDemoComboOption);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public SettingsInteractiveControlItem? SelectedInteractiveControl
	{
		get
		{
			return selectedInteractiveControl;
		}
		set
		{
			if (!EqualityComparer<SettingsInteractiveControlItem>.Default.Equals(selectedInteractiveControl, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SelectedInteractiveControl);
				selectedInteractiveControl = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SelectedInteractiveControl);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.4.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand RunControlDemoActionCommand => runControlDemoActionCommand ?? (runControlDemoActionCommand = new RelayCommand(RunControlDemoAction));

	internal ControlListSettingsViewModel(SettingsPersistenceCoordinator persistence)
		: base(persistence)
	{
		foreach (SettingsInteractiveControlItem item in SettingsInteractiveControlCatalog.Create())
		{
			InteractiveControls.Add(item);
		}
		selectedControlDemoComboOption = InteractiveControls.FirstOrDefault();
		selectedInteractiveControl = InteractiveControls.FirstOrDefault();
	}

	[RelayCommand]
	private void RunControlDemoAction()
	{
		ControlDemoProgress = ((ControlDemoProgress >= 100) ? 20 : (ControlDemoProgress + 20));
		ControlDemoStatusText = Strings.Settings_ControlDemoStatusClicked;
		ControlDemoSecondaryMenuSelected = !ControlDemoSecondaryMenuSelected;
	}
}
