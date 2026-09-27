using System.Collections.Generic;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Settings;

public static class SettingsInteractiveControlCatalog
{
	public static IReadOnlyList<SettingsInteractiveControlItem> Create()
	{
		return new global::_003C_003Ez__ReadOnlyArray<SettingsInteractiveControlItem>(new SettingsInteractiveControlItem[14]
		{
			new SettingsInteractiveControlItem(Strings.Settings_ControlSecondaryMenuButton, Strings.Settings_ControlCategoryNavigation),
			new SettingsInteractiveControlItem(Strings.Settings_ControlListPageItemButton, Strings.Settings_ControlCategoryNavigation),
			new SettingsInteractiveControlItem(Strings.Settings_ControlDialogButton, Strings.Settings_ControlCategoryButton),
			new SettingsInteractiveControlItem(Strings.Settings_ControlPrimaryButton, Strings.Settings_ControlCategoryButton),
			new SettingsInteractiveControlItem(Strings.Settings_ControlDangerButton, Strings.Settings_ControlCategoryButton),
			new SettingsInteractiveControlItem(Strings.Settings_ControlInlineIconButton, Strings.Settings_ControlCategoryButton),
			new SettingsInteractiveControlItem(Strings.Settings_ControlSwitch, Strings.Settings_ControlCategoryToggle),
			new SettingsInteractiveControlItem(Strings.Settings_ControlSlider, Strings.Settings_ControlCategoryInput),
			new SettingsInteractiveControlItem(Strings.Settings_ControlComboBox, Strings.Settings_ControlCategorySelection),
			new SettingsInteractiveControlItem(Strings.Settings_ControlTextBox, Strings.Settings_ControlCategoryInput),
			new SettingsInteractiveControlItem(Strings.Settings_ControlMultilineTextBox, Strings.Settings_ControlCategoryInput),
			new SettingsInteractiveControlItem(Strings.Settings_ControlSearchBox, Strings.Settings_ControlCategoryInput),
			new SettingsInteractiveControlItem(Strings.Settings_ControlChoiceList, Strings.Settings_ControlCategorySelection),
			new SettingsInteractiveControlItem(Strings.Settings_ControlVirtualizedList, Strings.Settings_ControlCategorySelection)
		});
	}
}
