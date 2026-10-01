using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace StartRide.App.Controls;

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

		TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(SecondaryMenuFrame), new PropertyMetadata((object)string.Empty));
		MenuContentProperty = DependencyProperty.Register("MenuContent", typeof(object), typeof(SecondaryMenuFrame), new PropertyMetadata((object)null, new PropertyChangedCallback(OnContentHostPropertyChanged)));
		UseInternalScrollViewerProperty = DependencyProperty.Register("UseInternalScrollViewer", typeof(bool), typeof(SecondaryMenuFrame), new PropertyMetadata((object)true, new PropertyChangedCallback(OnContentHostPropertyChanged)));
		FooterContentProperty = DependencyProperty.Register("FooterContent", typeof(object), typeof(SecondaryMenuFrame), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
