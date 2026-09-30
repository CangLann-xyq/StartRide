using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StartRide.App.Behaviors;

/// <summary>
/// 「液态玻璃」按钮的指针追光（iOS 26 Liquid Glass 那一挂的观感）。
/// </summary>
/// <remarks>
/// 先说清楚边界：WPF 的 ShaderEffect 只能采样**元素自己**的渲染结果，
/// 拿不到按钮下方的像素，所以真正的"折射背景"在 WPF 里做不到。
/// 这里复刻的是玻璃表面最抓眼球的三个特征：
///   1. 高光斑跟着指针走（本行为负责）；
///   2. 透镜穹顶的亮点随指针小幅偏移，像玻璃在指下微微一鼓（本行为负责）；
///   3. 指针离开时，高光带一点回弹收回中心（本行为负责）。
/// 穹顶亮环、顶部天光、底部反光这些"不跟手"的层由各个按钮模板自己的
/// Storyboard 管，不归这里。
///
/// 用法：模板里放两个 <c>x:Name="PART_GlassGlossSpot"</c> /
/// <c>x:Name="PART_GlassDomeSpot"</c> 的 <see cref="FrameworkElement"/>（放在 Canvas 上，
/// 由本行为写 Canvas.Left / Canvas.Top 来移动），然后在按钮上
/// <c>behaviors:GlassHighlightBehavior.IsEnabled="True"</c>。
///
/// 刻意只抓 FrameworkElement、不抓画刷：模板里带 x:Name 的 Freezable
/// 在 FindName 上的行为在不同 WPF 版本里并不一致，
/// 而元素一定在模板命名域里，这条路径不会踩空。
/// 找不到元素就静默跳过 —— 反编译的模板里混着几十个按钮，
/// 少一个光斑名字绝不能让启动器崩掉。
/// </remarks>
public static class GlassHighlightBehavior
{
	private const string GlossSpotName = "PART_GlassGlossSpot";

	private const string DomeSpotName = "PART_GlassDomeSpot";

	/// <summary>光点回中心用的时长。太快会像被"嗖"地弹回去，反而假。</summary>
	private static readonly Duration RecenterDuration = new Duration(TimeSpan.FromMilliseconds(520));

	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(GlassHighlightBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, Hook> Hooks = new System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, Hook>();

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, value);
	}

	private sealed class Hook
	{
		internal MouseEventHandler OnMove;

		internal MouseEventHandler OnLeave;

		internal FrameworkElement GlossSpot;

		internal FrameworkElement DomeSpot;

		internal bool Resolved;
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (!(dependencyObject is FrameworkElement element))
		{
			return;
		}

		if (e.NewValue is true)
		{
			Attach(element);
		}
		else
		{
			Detach(element);
		}
	}

	private static void Attach(FrameworkElement element)
	{
		if (Hooks.TryGetValue(element, out Hook _))
		{
			return;
		}

		Hook hook = new Hook();
		hook.OnMove = delegate(object sender, MouseEventArgs args)
		{
			MoveSpots(element, hook, args);
		};
		hook.OnLeave = delegate
		{
			Recenter(element, hook);
		};

		Hooks.Add(element, hook);
		element.MouseMove += hook.OnMove;
		element.MouseLeave += hook.OnLeave;
	}

	private static void Detach(FrameworkElement element)
	{
		if (!Hooks.TryGetValue(element, out Hook hook))
		{
			return;
		}

		element.MouseMove -= hook.OnMove;
		element.MouseLeave -= hook.OnLeave;
		Hooks.Remove(element);
	}

	private static void MoveSpots(FrameworkElement element, Hook hook, MouseEventArgs e)
	{
		double width = element.ActualWidth;
		double height = element.ActualHeight;
		if (width <= 1.0 || height <= 1.0)
		{
			return;
		}

		ResolveSpots(element, hook);
		if (hook.GlossSpot == null && hook.DomeSpot == null)
		{
			return;
		}

		Point position = e.GetPosition(element);
		double normalizedX = Math.Clamp(position.X / width, 0.0, 1.0);
		double normalizedY = Math.Clamp(position.Y / height, 0.0, 1.0);

		// 高光斑：整体跟手。
		PlaceSpot(hook.GlossSpot, width, height, normalizedX, normalizedY);
		// 透镜亮点：只跟着走一小段 —— 幅度一大就变成"液体在晃"，不像玻璃了。
		PlaceSpot(hook.DomeSpot, width, height, 0.5 + (normalizedX - 0.5) * 0.34, 0.44 + (normalizedY - 0.5) * 0.3);
	}

	/// <summary>指针离开：高光悠悠地滑回中心，带一点过冲（BackEase）—— 这一步是"灵动"的来源。</summary>
	private static void Recenter(FrameworkElement element, Hook hook)
	{
		ResolveSpots(element, hook);
		if (hook.GlossSpot == null && hook.DomeSpot == null)
		{
			return;
		}

		double width = element.ActualWidth;
		double height = element.ActualHeight;
		BackEase ease = new BackEase
		{
			EasingMode = EasingMode.EaseOut,
			Amplitude = 0.7
		};

		AnimateCentre(hook.GlossSpot, width, height, 0.5, 0.36, ease);
		AnimateCentre(hook.DomeSpot, width, height, 0.5, 0.44, ease);
	}

	private static void PlaceSpot(FrameworkElement spot, double width, double height, double normalizedX, double normalizedY)
	{
		if (spot == null)
		{
			return;
		}

		// ⚠️ 属性上还挂着"回中心"的动画时，直接赋值会被动画盖掉（动画优先级高于本地值），
		//    所以先把动画摘掉再写值。
		spot.BeginAnimation(Canvas.LeftProperty, null);
		spot.BeginAnimation(Canvas.TopProperty, null);
		Canvas.SetLeft(spot, normalizedX * width - spot.Width / 2.0);
		Canvas.SetTop(spot, normalizedY * height - spot.Height / 2.0);
	}

	private static void AnimateCentre(FrameworkElement spot, double width, double height, double normalizedX, double normalizedY, IEasingFunction ease)
	{
		if (spot == null || width <= 1.0 || height <= 1.0)
		{
			return;
		}

		DoubleAnimation left = new DoubleAnimation(normalizedX * width - spot.Width / 2.0, RecenterDuration)
		{
			EasingFunction = ease
		};
		DoubleAnimation top = new DoubleAnimation(normalizedY * height - spot.Height / 2.0, RecenterDuration)
		{
			EasingFunction = ease
		};

		spot.BeginAnimation(Canvas.LeftProperty, left);
		spot.BeginAnimation(Canvas.TopProperty, top);
	}

	private static void ResolveSpots(FrameworkElement element, Hook hook)
	{
		if (hook.Resolved)
		{
			return;
		}

		hook.GlossSpot = ResolveSpot(element, GlossSpotName);
		hook.DomeSpot = ResolveSpot(element, DomeSpotName);
		hook.Resolved = hook.GlossSpot != null || hook.DomeSpot != null;
	}

	private static FrameworkElement ResolveSpot(FrameworkElement element, string name)
	{
		// 首选模板命名域：这是模板里 x:Name 的"官方"归属地。
		if (element is Control control && control.Template != null)
		{
			try
			{
				if (control.Template.FindName(name, control) is FrameworkElement fromTemplate)
				{
					return fromTemplate;
				}
			}
			catch (InvalidOperationException)
			{
				// 模板尚未应用时 FindName 会抛，忽略即可 —— 下一次鼠标移动还会再来。
			}
		}

		// 兜底：某些模板把元素放进了嵌套模板，命名域不在这里，直接按可视树找。
		return FindByName(element, name) as FrameworkElement;
	}

	private static DependencyObject FindByName(DependencyObject root, string name)
	{
		int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
			if (child is FrameworkElement frameworkElement && frameworkElement.Name == name)
			{
				return child;
			}

			DependencyObject nested = FindByName(child, name);
			if (nested != null)
			{
				return nested;
			}
		}

		return null;
	}
}
