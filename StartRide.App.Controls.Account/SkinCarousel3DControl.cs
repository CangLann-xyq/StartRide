using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Launcher.Domain.Models;
using Serilog;

namespace StartRide.App.Controls.Account;

public sealed class SkinCarousel3DControl : Grid
{
	private sealed record SkinSlotBuildRequest(SkinCarouselSlot Slot, LauncherSkinRecord? Skin, double Brightness);

	private sealed record SlotVisual(LauncherSkinRecord Skin, SkinCarouselSlot Slot, ScaleTransform3D Scale, TranslateTransform3D Translate, SkinCarouselSlotPlacement TargetPlacement)
	{
		public SkinCarouselSlotPlacement GetCurrentPlacement()
		{
			return new SkinCarouselSlotPlacement(Translate.OffsetX, Scale.ScaleX);
		}
	}

	private readonly record struct PlayerModelCacheKey(string Identity, MinecraftSkinModel SkinModel, double Brightness);

	private const int AnimationMilliseconds = 600;

	private const int PlayerModelCacheCapacity = 8;

	private const double CenterBrightness = 1.0;

	private const double SideBrightness = 0.48;

	public static readonly DependencyProperty PreviousSkinProperty;

	public static readonly DependencyProperty SelectedSkinProperty;

	public static readonly DependencyProperty NextSkinProperty;

	public static readonly DependencyProperty PreviousCommandProperty;

	public static readonly DependencyProperty NextCommandProperty;

	private readonly Viewport3D viewport = new Viewport3D();

	private readonly Viewport3DIdleRenderCache viewportRenderCache;

	private readonly Border leftHoverHint;

	private readonly Border rightHoverHint;

	private readonly Dictionary<Model3D, SkinCarouselSlot> hitSlots = new Dictionary<Model3D, SkinCarouselSlot>();

	private readonly Dictionary<SkinCarouselSlot, SlotVisual> currentSlotVisuals = new Dictionary<SkinCarouselSlot, SlotVisual>();

	private readonly Dictionary<PlayerModelCacheKey, Model3DGroup> playerModelCache = new Dictionary<PlayerModelCacheKey, Model3DGroup>();

	private readonly Queue<PlayerModelCacheKey> playerModelCacheOrder = new Queue<PlayerModelCacheKey>();

	private CancellationTokenSource? sceneBuildCancellation;

	private bool rebuildQueued;

	private int sceneBuildGeneration;

	private int animationGeneration;

	private bool isAnimating;

	private SkinCarouselDirection? pendingDirection;

	private LauncherSkinRecord? previousRenderedSkin;

	private LauncherSkinRecord? selectedRenderedSkin;

	private LauncherSkinRecord? nextRenderedSkin;

	public LauncherSkinRecord? PreviousSkin
	{
		get
		{
			return (LauncherSkinRecord)((DependencyObject)this).GetValue(PreviousSkinProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(PreviousSkinProperty, (object)value);
		}
	}

	public LauncherSkinRecord? SelectedSkin
	{
		get
		{
			return (LauncherSkinRecord)((DependencyObject)this).GetValue(SelectedSkinProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SelectedSkinProperty, (object)value);
		}
	}

	public LauncherSkinRecord? NextSkin
	{
		get
		{
			return (LauncherSkinRecord)((DependencyObject)this).GetValue(NextSkinProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(NextSkinProperty, (object)value);
		}
	}

	public ICommand? PreviousCommand
	{
		get
		{
			return (ICommand)((DependencyObject)this).GetValue(PreviousCommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(PreviousCommandProperty, (object)value);
		}
	}

	public ICommand? NextCommand
	{
		get
		{
			return (ICommand)((DependencyObject)this).GetValue(NextCommandProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(NextCommandProperty, (object)value);
		}
	}

	public SkinCarousel3DControl()
	{
		viewportRenderCache = new Viewport3DIdleRenderCache(viewport, "Skin");
		base.ClipToBounds = true;
		base.Focusable = false;
		viewport.ClipToBounds = false;
		viewport.Camera = new PerspectiveCamera(new Point3D(0.0, 4.0, 62.0), new Vector3D(0.0, 0.0, -62.0), new Vector3D(0.0, 1.0, 0.0), 28.0);
		Grid grid = CreateHoverLayer();
		leftHoverHint = CreateHoverHint();
		rightHoverHint = CreateHoverHint();
		PositionHoverHint(leftHoverHint, SkinCarouselSlot.Left);
		PositionHoverHint(rightHoverHint, SkinCarouselSlot.Right);
		grid.Children.Add(leftHoverHint);
		grid.Children.Add(rightHoverHint);
		base.Children.Add(grid);
		base.Children.Add(viewport);
		base.MouseLeftButtonUp += OnMouseLeftButtonUp;
		base.MouseMove += OnMouseMove;
		base.Loaded += OnLoaded;
		base.Unloaded += OnUnloaded;
		base.MouseLeave += delegate
		{
			base.Cursor = Cursors.Arrow;
			UpdateHoverHint(null);
		};
	}

	private static void OnSkinPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((SkinCarousel3DControl)(object)d).QueueRebuild();
	}

	private static void OnSelectedSkinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SkinCarousel3DControl skinCarousel3DControl = (SkinCarousel3DControl)(object)d;
		if (e.NewValue is LauncherSkinRecord left)
		{
			if (SkinCarousel3DLayout.SkinsRepresentSameVisualItem(left, skinCarousel3DControl.nextRenderedSkin))
			{
				skinCarousel3DControl.pendingDirection = SkinCarouselDirection.Next;
			}
			else if (SkinCarousel3DLayout.SkinsRepresentSameVisualItem(left, skinCarousel3DControl.previousRenderedSkin))
			{
				skinCarousel3DControl.pendingDirection = SkinCarouselDirection.Previous;
			}
		}
		skinCarousel3DControl.QueueRebuild();
	}

	private void QueueRebuild()
	{
		viewportRenderCache.Disable("SceneChange");
		sceneBuildGeneration++;
		sceneBuildCancellation?.Cancel();
		if (!rebuildQueued)
		{
			rebuildQueued = true;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(Rebuild), (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private async void Rebuild()
	{
		rebuildQueued = false;
		int generation = sceneBuildGeneration;
		sceneBuildCancellation?.Dispose();
		CancellationTokenSource cancellation = new CancellationTokenSource();
		sceneBuildCancellation = cancellation;
		LauncherSkinRecord previousSkin = PreviousSkin;
		LauncherSkinRecord selectedSkin = SelectedSkin;
		LauncherSkinRecord nextSkin = NextSkin;
		List<SlotVisual> oldSlotVisuals = currentSlotVisuals.Values.ToList();
		SkinCarouselDirection? direction = (SkinCarousel3DLayout.CanAnimateTransition(pendingDirection, previousRenderedSkin, selectedRenderedSkin, nextRenderedSkin, previousSkin, selectedSkin, nextSkin) ? pendingDirection : ((SkinCarouselDirection?)null));
		pendingDirection = null;
		SkinSlotBuildRequest[] slotRequests = new SkinSlotBuildRequest[3]
		{
			new SkinSlotBuildRequest(SkinCarouselSlot.Left, previousSkin, 0.48),
			new SkinSlotBuildRequest(SkinCarouselSlot.Center, selectedSkin, 1.0),
			new SkinSlotBuildRequest(SkinCarouselSlot.Right, nextSkin, 0.48)
		};
		Dictionary<PlayerModelCacheKey, Model3DGroup> prepared = new Dictionary<PlayerModelCacheKey, Model3DGroup>();
		Dictionary<PlayerModelCacheKey, SkinSlotBuildRequest> misses = new Dictionary<PlayerModelCacheKey, SkinSlotBuildRequest>();
		SkinSlotBuildRequest[] array = slotRequests;
		foreach (SkinSlotBuildRequest skinSlotBuildRequest in array)
		{
			if (skinSlotBuildRequest.Skin != null && !string.IsNullOrWhiteSpace(skinSlotBuildRequest.Skin.Source))
			{
				PlayerModelCacheKey key = CreatePlayerModelCacheKey(skinSlotBuildRequest.Skin, skinSlotBuildRequest.Brightness);
				if (playerModelCache.TryGetValue(key, out Model3DGroup value))
				{
					prepared[key] = value;
				}
				else
				{
					misses.TryAdd(key, skinSlotBuildRequest);
				}
			}
		}
		try
		{
			Dictionary<PlayerModelCacheKey, Model3DGroup> dictionary = await Task.Run(() => BuildMissingPlayerModels(misses, cancellation.Token), cancellation.Token);
			cancellation.Token.ThrowIfCancellationRequested();
			if (generation != sceneBuildGeneration || !base.IsLoaded)
			{
				return;
			}
			foreach (KeyValuePair<PlayerModelCacheKey, Model3DGroup> item in dictionary)
			{
				prepared[item.Key] = item.Value;
				AddPlayerModelToCache(item.Key, item.Value);
			}
			if (prepared.Count == 0 && oldSlotVisuals.Count > 0 && slotRequests.Any((SkinSlotBuildRequest item) => item.Skin != null))
			{
				Log.Warning("Account skin scene retained because every requested model failed to build; generation={Generation}", generation);
				QueueViewportRenderCache();
				return;
			}
			FreezeCurrentAnimations(oldSlotVisuals);
			isAnimating = false;
			viewport.Children.Clear();
			currentSlotVisuals.Clear();
			hitSlots.Clear();
			UpdateHoverHint(null);
			Model3DGroup model3DGroup = new Model3DGroup
			{
				Children = 
				{
					(Model3D)MinecraftSkinPreviewModelBuilder.CreateAmbientLight(),
					(Model3D)MinecraftSkinPreviewModelBuilder.CreateDirectionalLight()
				}
			};
			AddPreparedSlot(model3DGroup, slotRequests[0], prepared, oldSlotVisuals, direction);
			AddPreparedSlot(model3DGroup, slotRequests[1], prepared, oldSlotVisuals, direction);
			AddPreparedSlot(model3DGroup, slotRequests[2], prepared, oldSlotVisuals, direction);
			viewport.Children.Add(new ModelVisual3D
			{
				Content = model3DGroup
			});
			previousRenderedSkin = previousSkin;
			selectedRenderedSkin = selectedSkin;
			nextRenderedSkin = nextSkin;
			if (direction.HasValue && currentSlotVisuals.Count > 0)
			{
				AnimateSlots();
				return;
			}
			animationGeneration++;
			QueueViewportRenderCache();
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Account skin scene build failed; generation={Generation}", generation);
			if (generation == sceneBuildGeneration && !isAnimating)
			{
				QueueViewportRenderCache();
			}
		}
	}

	private static Dictionary<PlayerModelCacheKey, Model3DGroup> BuildMissingPlayerModels(IReadOnlyDictionary<PlayerModelCacheKey, SkinSlotBuildRequest> requests, CancellationToken cancellationToken)
	{
		Dictionary<PlayerModelCacheKey, Model3DGroup> dictionary = new Dictionary<PlayerModelCacheKey, Model3DGroup>();
		foreach (KeyValuePair<PlayerModelCacheKey, SkinSlotBuildRequest> request in requests)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				LauncherSkinRecord skin = request.Value.Skin;
				BitmapImage skin2 = MinecraftSkinPreviewModelBuilder.LoadSkinBitmap(skin.Source);
				cancellationToken.ThrowIfCancellationRequested();
				dictionary[request.Key] = MinecraftSkinPreviewModelBuilder.BuildPlayerModel(skin2, skin.SkinModel, request.Value.Brightness);
			}
			catch when (!cancellationToken.IsCancellationRequested)
			{
			}
		}
		return dictionary;
	}

	private void AddPreparedSlot(Model3DGroup scene, SkinSlotBuildRequest request, IReadOnlyDictionary<PlayerModelCacheKey, Model3DGroup> prepared, IReadOnlyList<SlotVisual> oldSlotVisuals, SkinCarouselDirection? direction)
	{
		LauncherSkinRecord skin = request.Skin;
		if (skin != null && !string.IsNullOrWhiteSpace(skin.Source) && prepared.TryGetValue(CreatePlayerModelCacheKey(skin, request.Brightness), out Model3DGroup value))
		{
			SkinCarouselSlotPlacement placement = SkinCarousel3DLayout.GetPlacement(request.Slot);
			SkinCarouselSlotPlacement skinCarouselSlotPlacement = ResolveStartPlacement(skin, request.Slot, oldSlotVisuals, direction);
			ScaleTransform3D scaleTransform3D = new ScaleTransform3D(skinCarouselSlotPlacement.Scale, skinCarouselSlotPlacement.Scale, skinCarouselSlotPlacement.Scale, 0.0, 4.0, 0.0);
			TranslateTransform3D translateTransform3D = new TranslateTransform3D(skinCarouselSlotPlacement.X, 0.0, 0.0);
			Transform3DGroup transform3DGroup = new Transform3DGroup();
			transform3DGroup.Children.Add(scaleTransform3D);
			transform3DGroup.Children.Add(translateTransform3D);
			Model3DGroup model3DGroup = new Model3DGroup
			{
				Transform = transform3DGroup
			};
			model3DGroup.Children.Add(value);
			scene.Children.Add(model3DGroup);
			RegisterHitModels(model3DGroup, request.Slot);
			currentSlotVisuals[request.Slot] = new SlotVisual(skin, request.Slot, scaleTransform3D, translateTransform3D, placement);
		}
	}

	private static PlayerModelCacheKey CreatePlayerModelCacheKey(LauncherSkinRecord skin, double brightness)
	{
		return new PlayerModelCacheKey((!string.IsNullOrWhiteSpace(skin.ContentHash)) ? skin.ContentHash : skin.Source, skin.SkinModel, brightness);
	}

	private void AddPlayerModelToCache(PlayerModelCacheKey key, Model3DGroup model)
	{
		if (!playerModelCache.ContainsKey(key))
		{
			playerModelCache[key] = model;
			playerModelCacheOrder.Enqueue(key);
			while (playerModelCacheOrder.Count > 8)
			{
				playerModelCache.Remove(playerModelCacheOrder.Dequeue());
			}
		}
	}

	private static SkinCarouselSlotPlacement ResolveStartPlacement(LauncherSkinRecord skin, SkinCarouselSlot targetSlot, IReadOnlyList<SlotVisual> oldSlotVisuals, SkinCarouselDirection? direction)
	{
		if (!direction.HasValue)
		{
			return SkinCarousel3DLayout.GetPlacement(targetSlot);
		}
		return (SkinCarouselSlotPlacement)(oldSlotVisuals.FirstOrDefault((SlotVisual visual) => SkinsMatch(visual.Skin, skin))?.GetCurrentPlacement() ?? (direction switch
		{
			SkinCarouselDirection.Next => SkinCarousel3DLayout.GetEntryPlacement(SkinCarouselDirection.Next), 
			SkinCarouselDirection.Previous => SkinCarousel3DLayout.GetEntryPlacement(SkinCarouselDirection.Previous), 
			_ => SkinCarousel3DLayout.GetPlacement(targetSlot), 
		}));
	}

	private void AnimateSlots()
	{
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected O, but got Unknown
		viewportRenderCache.Disable("CarouselAnimation");
		isAnimating = true;
		PowerEase easing = new PowerEase
		{
			EasingMode = EasingMode.EaseOut,
			Power = 7.0
		};
		int generation = ++animationGeneration;
		foreach (SlotVisual value in currentSlotVisuals.Values)
		{
			DoubleAnimation animation = CreateAnimation(value.TargetPlacement.X, easing);
			value.Translate.BeginAnimation(TranslateTransform3D.OffsetXProperty, animation);
			value.Scale.BeginAnimation(ScaleTransform3D.ScaleXProperty, CreateAnimation(value.TargetPlacement.Scale, easing));
			value.Scale.BeginAnimation(ScaleTransform3D.ScaleYProperty, CreateAnimation(value.TargetPlacement.Scale, easing));
			value.Scale.BeginAnimation(ScaleTransform3D.ScaleZProperty, CreateAnimation(value.TargetPlacement.Scale, easing));
		}
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(640.0)
		};
		timer.Tick += delegate
		{
			timer.Stop();
			if (generation == animationGeneration)
			{
				CompleteCurrentAnimations();
				isAnimating = false;
				QueueViewportRenderCache();
			}
		};
		timer.Start();
	}

	private void CompleteCurrentAnimations()
	{
		foreach (SlotVisual value in currentSlotVisuals.Values)
		{
			CarouselAnimationLifecycle.CompleteAndRemoveClocks(value.Scale, value.Translate, value.TargetPlacement.X, value.TargetPlacement.Scale);
		}
	}

	private static void FreezeCurrentAnimations(IEnumerable<SlotVisual> visuals)
	{
		foreach (SlotVisual visual in visuals)
		{
			CarouselAnimationLifecycle.CaptureCurrentAndRemoveClocks(visual.Scale, visual.Translate);
		}
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		QueueRebuild();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		viewportRenderCache.Disable("Unloaded");
		sceneBuildGeneration++;
		animationGeneration++;
		sceneBuildCancellation?.Cancel();
		sceneBuildCancellation?.Dispose();
		sceneBuildCancellation = null;
		FreezeCurrentAnimations(currentSlotVisuals.Values);
		viewport.Children.Clear();
		currentSlotVisuals.Clear();
		hitSlots.Clear();
		rebuildQueued = false;
		isAnimating = false;
	}

	private void QueueViewportRenderCache()
	{
		viewportRenderCache.QueueEnable(() => !isAnimating && !rebuildQueued && currentSlotVisuals.Count > 0);
	}

	private static DoubleAnimation CreateAnimation(double to, IEasingFunction easing)
	{
		return new DoubleAnimation(to, TimeSpan.FromMilliseconds(600.0))
		{
			EasingFunction = easing,
			FillBehavior = FillBehavior.HoldEnd
		};
	}

	private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SkinCarouselSlot? skinCarouselSlot = HitTestSlot(e.GetPosition(this));
		if (skinCarouselSlot.HasValue && skinCarouselSlot.GetValueOrDefault() == SkinCarouselSlot.Left)
		{
			ExecuteCommand(PreviousCommand);
		}
		else if (skinCarouselSlot.HasValue && skinCarouselSlot == SkinCarouselSlot.Right)
		{
			ExecuteCommand(NextCommand);
		}
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		SkinCarouselSlot? skinCarouselSlot = HitTestSlot(e.GetPosition(this));
		bool flag = CanClickSlot(skinCarouselSlot);
		base.Cursor = (flag ? Cursors.Hand : Cursors.Arrow);
		UpdateHoverHint(flag ? skinCarouselSlot : ((SkinCarouselSlot?)null));
	}

	private SkinCarouselSlot? HitTestSlot(Point point)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		SkinCarouselSlot? slot = null;
		VisualTreeHelper.HitTest(viewport, null, delegate(HitTestResult result)
		{
			if (result is RayHitTestResult { ModelHit: not null } rayHitTestResult && hitSlots.TryGetValue(rayHitTestResult.ModelHit, out var value))
			{
				slot = value;
				return HitTestResultBehavior.Stop;
			}
			return HitTestResultBehavior.Continue;
		}, new PointHitTestParameters(TranslatePoint(point, viewport)));
		return slot;
	}

	private bool CanClickSlot(SkinCarouselSlot? slot)
	{
		return slot switch
		{
			SkinCarouselSlot.Left => PreviousCommand?.CanExecute(null) ?? false, 
			SkinCarouselSlot.Right => NextCommand?.CanExecute(null) ?? false, 
			_ => false, 
		};
	}

	private void UpdateHoverHint(SkinCarouselSlot? slot)
	{
		leftHoverHint.Visibility = (((slot ?? SkinCarouselSlot.Center) != SkinCarouselSlot.Left) ? Visibility.Collapsed : Visibility.Visible);
		rightHoverHint.Visibility = ((!slot.HasValue || slot != SkinCarouselSlot.Right) ? Visibility.Collapsed : Visibility.Visible);
	}

	private static Grid CreateHoverLayer()
	{
		return new Grid
		{
			IsHitTestVisible = false,
			VerticalAlignment = VerticalAlignment.Stretch
		};
	}

	private static Border CreateHoverHint()
	{
		Border border = new Border();
		border.Width = 96.0;
		border.Height = 148.0;
		border.CornerRadius = new CornerRadius(10.0);
		border.HorizontalAlignment = HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Center;
		border.Visibility = Visibility.Collapsed;
		border.SetResourceReference(Border.BackgroundProperty, "Brush.Control.Hover");
		return border;
	}

	private static void PositionHoverHint(Border hint, SkinCarouselSlot slot)
	{
		int num = ((slot == SkinCarouselSlot.Left) ? (-175) : 175);
		hint.RenderTransform = new TranslateTransform(num, 0.0);
	}

	private static void ExecuteCommand(ICommand? command)
	{
		if (command != null && command.CanExecute(null))
		{
			command.Execute(null);
		}
	}

	private void RegisterHitModels(Model3D model, SkinCarouselSlot slot)
	{
		hitSlots[model] = slot;
		if (!(model is Model3DGroup model3DGroup))
		{
			return;
		}
		foreach (Model3D child in model3DGroup.Children)
		{
			RegisterHitModels(child, slot);
		}
	}

	private static bool SkinsMatch(LauncherSkinRecord? left, LauncherSkinRecord? right)
	{
		return SkinCarousel3DLayout.SkinsRepresentSameVisualItem(left, right);
	}

	static SkinCarousel3DControl()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		PreviousSkinProperty = DependencyProperty.Register("PreviousSkin", typeof(LauncherSkinRecord), typeof(SkinCarousel3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnSkinPropertyChanged)));
		SelectedSkinProperty = DependencyProperty.Register("SelectedSkin", typeof(LauncherSkinRecord), typeof(SkinCarousel3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnSelectedSkinChanged)));
		NextSkinProperty = DependencyProperty.Register("NextSkin", typeof(LauncherSkinRecord), typeof(SkinCarousel3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnSkinPropertyChanged)));
		PreviousCommandProperty = DependencyProperty.Register("PreviousCommand", typeof(ICommand), typeof(SkinCarousel3DControl), new PropertyMetadata((PropertyChangedCallback)null));
		NextCommandProperty = DependencyProperty.Register("NextCommand", typeof(ICommand), typeof(SkinCarousel3DControl), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
