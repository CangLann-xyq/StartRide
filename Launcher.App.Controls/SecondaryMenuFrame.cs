using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Launcher.App.Controls;

public partial class SecondaryMenuFrame : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TitleProperty;

	public static readonly DependencyProperty MenuContentProperty;

	public static readonly DependencyProperty UseInternalScrollViewerProperty;

	public static readonly DependencyProperty FooterContentProperty;

	public string Title
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TitleProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TitleProperty, (object)value);
		}
	}

	public object? MenuContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(MenuContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(MenuContentProperty, value);
		}
	}

	public bool UseInternalScrollViewer
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(UseInternalScrollViewerProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(UseInternalScrollViewerProperty, (object)value);
		}
	}

	public object? FooterContent
	{
		get
		{
			return ((DependencyObject)this).GetValue(FooterContentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(FooterContentProperty, value);
		}
	}

	public SecondaryMenuFrame()
	{
		InitializeComponent();
		UpdateContentHost();
	}

	private static void OnContentHostPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((SecondaryMenuFrame)(object)d).UpdateContentHost();
	}

	private void UpdateContentHost()
	{
		if (ScrollableContentHost != null && DirectContentHost != null)
		{
			if (UseInternalScrollViewer)
			{
				DirectContentHost.Content = null;
				ScrollableContentHost.Content = MenuContent;
			}
			else
			{
				ScrollableContentHost.Content = null;
				DirectContentHost.Content = MenuContent;
			}
		}
	}


	static SecondaryMenuFrame()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(SecondaryMenuFrame), new PropertyMetadata((object)string.Empty));
		MenuContentProperty = DependencyProperty.Register("MenuContent", typeof(object), typeof(SecondaryMenuFrame), new PropertyMetadata((object)null, new PropertyChangedCallback(OnContentHostPropertyChanged)));
		UseInternalScrollViewerProperty = DependencyProperty.Register("UseInternalScrollViewer", typeof(bool), typeof(SecondaryMenuFrame), new PropertyMetadata((object)true, new PropertyChangedCallback(OnContentHostPropertyChanged)));
		FooterContentProperty = DependencyProperty.Register("FooterContent", typeof(object), typeof(SecondaryMenuFrame), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
