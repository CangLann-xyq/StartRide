using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace StartRide.App.Controls;

public partial class SearchTextBox : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(SearchTextBox), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	public string Text
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(TextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(TextProperty, (object)value);
		}
	}

	public SearchTextBox()
	{
		InitializeComponent();
	}

}
