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
using Launcher.Application.Accounts;
using Serilog;

namespace Launcher.App.Controls.Account;

public sealed class CapeCarousel3DControl : Grid
{
	private sealed record CapeSlotBuildRequest(CapeCarouselSlot Slot, AccountCapeOption? Cape, double Brightness, BitmapSource? Texture, CapeModelCacheKey Key);

	private readonly record struct CapeModelCacheKey(string Identity, string? TextureSource, double Brightness, int TextureVersion, bool HasTexture);

	private sealed record SlotVisual(AccountCapeOption Cape, CapeCarouselSlot Slot, ScaleTransform3D Scale, TranslateTransform3D Translate, CapeCarouselSlotPlacement TargetPlacement)
	{
		public CapeCarouselSlotPlacement GetCurrentPlacement()
		{
			return new CapeCarouselSlotPlacement(Translate.OffsetX, Scale.ScaleX);
		}
	}

	private const int AnimationMilliseconds = 600;

	private const int CapeModelCacheCapacity = 8;

	private const double CenterBrightness = 1.0;

	private const double SideBrightness = 0.48;

	public static readonly DependencyProperty PreviousCapeProperty;

	public static readonly DependencyProperty SelectedCapeProperty;

	public static readonly DependencyProperty NextCapeProperty;

	public static readonly DependencyProperty PreviousCommandProperty;

	public static readonly DependencyProperty NextCommandProperty;

	private readonly Viewport3D viewport = new Viewport3D();

	private readonly Viewport3DIdleRenderCache viewportRenderCache;

	private readonly Border leftHoverHint;

	private readonly Border rightHoverHint;

	private readonly Dictionary<Model3D, CapeCarouselSlot> hitSlots = new Dictionary<Model3D, CapeCarouselSlot>();

	private readonly Dictionary<CapeCarouselSlot, SlotVisual> currentSlotVisuals = new Dictionary<CapeCarouselSlot, SlotVisual>();

	private readonly Dictionary<string, BitmapSource> capeTextureCache = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);

	private readonly HashSet<string> capeTextureRequests = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, int> capeTextureVersions = new Dictionary<string, int>(StringComparer.Ordinal);

	private readonly Dictionary<CapeModelCacheKey, Model3DGroup> capeModelCache = new Dictionary<CapeModelCacheKey, Model3DGroup>();

	private readonly Queue<CapeModelCacheKey> capeModelCacheOrder = new Queue<CapeModelCacheKey>();

	private CancellationTokenSource? sceneBuildCancellation;

	private bool rebuildQueued;

	private bool isRebuilding;

	private bool isAnimating;

	private bool rebuildAfterAnimation;

	private int animationGeneration;

	private int sceneBuildGeneration;

	private CapeCarouselDirection? pendingDirection;

	private AccountCapeOption? previousRenderedCape;

	private AccountCapeOption? selectedRenderedCape;

	private AccountCapeOption? nextRenderedCape;

	public AccountCapeOption? PreviousCape
	{
		get
		{
			return (AccountCapeOption)((DependencyObject)this).GetValue(PreviousCapeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(PreviousCapeProperty, (object)value);
		}
	}

	public AccountCapeOption? SelectedCape
	{
		get
		{
			return (AccountCapeOption)((DependencyObject)this).GetValue(SelectedCapeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(SelectedCapeProperty, (object)value);
		}
	}

	public AccountCapeOption? NextCape
	{
		get
		{
			return (AccountCapeOption)((DependencyObject)this).GetValue(NextCapeProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(NextCapeProperty, (object)value);
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

	public CapeCarousel3DControl()
	{
		viewportRenderCache = new Viewport3DIdleRenderCache(viewport, "Cape");
		base.ClipToBounds = true;
		base.Focusable = false;
		viewport.ClipToBounds = false;
		viewport.Camera = new PerspectiveCamera(new Point3D(0.0, 8.0, 46.0), new Vector3D(0.0, 0.0, -46.0), new Vector3D(0.0, 1.0, 0.0), 28.0);
		Grid grid = CreateHoverLayer();
		leftHoverHint = CreateHoverHint();
		rightHoverHint = CreateHoverHint();
		PositionHoverHint(leftHoverHint, CapeCarouselSlot.Left);
		PositionHoverHint(rightHoverHint, CapeCarouselSlot.Right);
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

	private static void OnCapePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((CapeCarousel3DControl)(object)d).QueueRebuild();
	}

	private static void OnSelectedCapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		CapeCarousel3DControl capeCarousel3DControl = (CapeCarousel3DControl)(object)d;
		if (e.NewValue is AccountCapeOption left)
		{
			if (CapeCarousel3DLayout.CapesRepresentSameVisualItem(left, capeCarousel3DControl.nextRenderedCape))
			{
				capeCarousel3DControl.pendingDirection = CapeCarouselDirection.Next;
			}
			else if (CapeCarousel3DLayout.CapesRepresentSameVisualItem(left, capeCarousel3DControl.previousRenderedCape))
			{
				capeCarousel3DControl.pendingDirection = CapeCarouselDirection.Previous;
			}
		}
		capeCarousel3DControl.QueueRebuild();
	}

	private void QueueRebuild()
	{
		viewportRenderCache.Disable("SceneChange");
		sceneBuildGeneration++;
		sceneBuildCancellation?.Cancel();
		QueueRebuildCore();
	}

	private void QueueRebuildCore()
	{
		if (!rebuildQueued)
		{
			rebuildQueued = true;
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(Rebuild), (DispatcherPriority)4, Array.Empty<object>());
		}
	}

	private async void Rebuild()
	{
		rebuildQueued = false;
		isRebuilding = true;
		int generation = sceneBuildGeneration;
		sceneBuildCancellation?.Dispose();
		CancellationTokenSource cancellation = new CancellationTokenSource();
		sceneBuildCancellation = cancellation;
		AccountCapeOption previousCape = PreviousCape;
		AccountCapeOption selectedCape = SelectedCape;
		AccountCapeOption nextCape = NextCape;
		List<SlotVisual> oldSlotVisuals = currentSlotVisuals.Values.ToList();
		CapeCarouselDirection? direction = (CapeCarousel3DLayout.CanAnimateTransition(pendingDirection, previousRenderedCape, selectedRenderedCape, nextRenderedCape, previousCape, selectedCape, nextCape) ? pendingDirection : ((CapeCarouselDirection?)null));
		pendingDirection = null;
		CapeSlotBuildRequest[] slotRequests = new CapeSlotBuildRequest[3]
		{
			CreateCapeBuildRequest(CapeCarouselSlot.Left, previousCape, 0.48),
			CreateCapeBuildRequest(CapeCarouselSlot.Center, selectedCape, 1.0),
			CreateCapeBuildRequest(CapeCarouselSlot.Right, nextCape, 0.48)
		};
		Dictionary<CapeModelCacheKey, Model3DGroup> prepared = new Dictionary<CapeModelCacheKey, Model3DGroup>();
		Dictionary<CapeModelCacheKey, CapeSlotBuildRequest> misses = new Dictionary<CapeModelCacheKey, CapeSlotBuildRequest>();
		CapeSlotBuildRequest[] array = slotRequests;
		foreach (CapeSlotBuildRequest capeSlotBuildRequest in array)
		{
			if (capeSlotBuildRequest.Cape != null)
			{
				if (capeModelCache.TryGetValue(capeSlotBuildRequest.Key, out Model3DGroup value))
				{
					prepared[capeSlotBuildRequest.Key] = value;
				}
				else
				{
					misses.TryAdd(capeSlotBuildRequest.Key, capeSlotBuildRequest);
				}
			}
		}
		try
		{
			Dictionary<CapeModelCacheKey, Model3DGroup> dictionary = await Task.Run(() => BuildMissingCapeModels(misses, cancellation.Token), cancellation.Token);
			cancellation.Token.ThrowIfCancellationRequested();
			if (generation != sceneBuildGeneration || !base.IsLoaded)
			{
				return;
			}
			foreach (KeyValuePair<CapeModelCacheKey, Model3DGroup> item in dictionary)
			{
				prepared[item.Key] = item.Value;
				AddCapeModelToCache(item.Key, item.Value);
			}
			if (prepared.Count == 0 && oldSlotVisuals.Count > 0 && slotRequests.Any((CapeSlotBuildRequest item) => item.Cape != null))
			{
				isRebuilding = false;
				Log.Warning("Account cape scene retained because every requested model failed to build; generation={Generation}", generation);
				QueueViewportRenderCache();
				return;
			}
			FreezeCurrentAnimations(oldSlotVisuals);
			viewport.Children.Clear();
			currentSlotVisuals.Clear();
			hitSlots.Clear();
			UpdateHoverHint(null);
			Model3DGroup model3DGroup = new Model3DGroup
			{
				Children = 
				{
					(Model3D)MinecraftCapePreviewModelBuilder.CreateAmbientLight(),
					(Model3D)MinecraftCapePreviewModelBuilder.CreateDirectionalLight()
				}
			};
			AddPreparedSlot(model3DGroup, slotRequests[0], prepared, oldSlotVisuals, direction);
			AddPreparedSlot(model3DGroup, slotRequests[1], prepared, oldSlotVisuals, direction);
			AddPreparedSlot(model3DGroup, slotRequests[2], prepared, oldSlotVisuals, direction);
			viewport.Children.Add(new ModelVisual3D
			{
				Content = model3DGroup
			});
			previousRenderedCape = previousCape;
			selectedRenderedCape = selectedCape;
			nextRenderedCape = nextCape;
			isRebuilding = false;
			if (direction.HasValue && currentSlotVisuals.Count > 0)
			{
				AnimateSlots();
				return;
			}
			isAnimating = false;
			animationGeneration++;
			if (rebuildAfterAnimation)
			{
				rebuildAfterAnimation = false;
				QueueRebuild();
			}
			else
			{
				QueueViewportRenderCache();
			}
		}
		catch (OperationCanceledException)
		{
			if (generation == sceneBuildGeneration)
			{
				isRebuilding = false;
			}
		}
		catch (Exception exception)
		{
			if (generation == sceneBuildGeneration)
			{
				isRebuilding = false;
				if (!isAnimating)
				{
					QueueViewportRenderCache();
				}
			}
			Log.Warning(exception, "Account cape scene build failed; generation={Generation}", generation);
		}
	}

	private CapeSlotBuildRequest CreateCapeBuildRequest(CapeCarouselSlot slot, AccountCapeOption? cape, double brightness)
	{
		BitmapSource bitmapSource = null;
		if (cape != null)
		{
			bitmapSource = GetOrRequestCapeTexture(cape);
		}
		string identity = ((cape == null) ? string.Empty : (cape.Id ?? cape.ImageUrl ?? (cape.IsNone ? "none" : cape.DisplayName)));
		string text = cape?.ImageUrl;
		int textureVersion = ((text != null && capeTextureVersions.TryGetValue(text, out var value)) ? value : 0);
		return new CapeSlotBuildRequest(slot, cape, brightness, bitmapSource, new CapeModelCacheKey(identity, text, brightness, textureVersion, bitmapSource != null));
	}

	private static Dictionary<CapeModelCacheKey, Model3DGroup> BuildMissingCapeModels(IReadOnlyDictionary<CapeModelCacheKey, CapeSlotBuildRequest> requests, CancellationToken cancellationToken)
	{
		Dictionary<CapeModelCacheKey, Model3DGroup> dictionary = new Dictionary<CapeModelCacheKey, Model3DGroup>();
		foreach (KeyValuePair<CapeModelCacheKey, CapeSlotBuildRequest> request in requests)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				dictionary[request.Key] = MinecraftCapePreviewModelBuilder.BuildCapeModel(request.Value.Cape, request.Value.Brightness, request.Value.Texture);
			}
			catch when (!cancellationToken.IsCancellationRequested)
			{
			}
		}
		return dictionary;
	}

	private void AddPreparedSlot(Model3DGroup scene, CapeSlotBuildRequest request, IReadOnlyDictionary<CapeModelCacheKey, Model3DGroup> prepared, IReadOnlyList<SlotVisual> oldSlotVisuals, CapeCarouselDirection? direction)
	{
		AccountCapeOption cape = request.Cape;
		if (cape != null && prepared.TryGetValue(request.Key, out Model3DGroup value))
		{
			CapeCarouselSlotPlacement placement = CapeCarousel3DLayout.GetPlacement(request.Slot);
			CapeCarouselSlotPlacement capeCarouselSlotPlacement = ResolveStartPlacement(cape, request.Slot, oldSlotVisuals, direction);
			ScaleTransform3D scaleTransform3D = new ScaleTransform3D(capeCarouselSlotPlacement.Scale, capeCarouselSlotPlacement.Scale, capeCarouselSlotPlacement.Scale, 0.0, 8.0, 0.0);
			TranslateTransform3D translateTransform3D = new TranslateTransform3D(capeCarouselSlotPlacement.X, 0.0, 0.0);
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
			currentSlotVisuals[request.Slot] = new SlotVisual(cape, request.Slot, scaleTransform3D, translateTransform3D, placement);
		}
	}

	private void AddCapeModelToCache(CapeModelCacheKey key, Model3DGroup model)
	{
		if (!capeModelCache.ContainsKey(key))
		{
			capeModelCache[key] = model;
			capeModelCacheOrder.Enqueue(key);
			while (capeModelCacheOrder.Count > 8)
			{
				capeModelCache.Remove(capeModelCacheOrder.Dequeue());
			}
		}
	}

	private BitmapSource? GetOrRequestCapeTexture(AccountCapeOption cape)
	{
		if (cape.IsNone || string.IsNullOrWhiteSpace(cape.ImageUrl))
		{
			return null;
		}
		string imageUrl = cape.ImageUrl;
		if (capeTextureCache.TryGetValue(imageUrl, out BitmapSource value))
		{
			return value;
		}
		if (capeTextureRequests.Add(imageUrl))
		{
			BeginLoadCapeTexture(imageUrl);
		}
		return null;
	}

	private void BeginLoadCapeTexture(string source)
	{
		try
		{
			BitmapImage bitmap = new BitmapImage();
			bitmap.BeginInit();
			bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
			Uri uri = new Uri(source, UriKind.RelativeOrAbsolute);
			if (uri.IsAbsoluteUri && uri.IsFile)
			{
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
			}
			bitmap.UriSource = uri;
			bitmap.EndInit();
			if (bitmap.IsDownloading)
			{
				bitmap.DownloadCompleted += delegate
				{
					StoreLoadedCapeTexture(source, bitmap);
				};
				bitmap.DownloadFailed += delegate
				{
					capeTextureRequests.Remove(source);
				};
			}
			else
			{
				StoreLoadedCapeTexture(source, bitmap);
			}
		}
		catch
		{
			capeTextureRequests.Remove(source);
		}
	}

	private void StoreLoadedCapeTexture(string source, BitmapSource texture)
	{
		try
		{
			BitmapSource value = FreezeTexture(texture);
			capeTextureCache[source] = value;
			capeTextureVersions[source] = ((!capeTextureVersions.TryGetValue(source, out var value2)) ? 1 : (value2 + 1));
			CapeModelCacheKey[] array = capeModelCache.Keys.Where((CapeModelCacheKey capeModelCacheKey) => string.Equals(capeModelCacheKey.TextureSource, source, StringComparison.Ordinal)).ToArray();
			foreach (CapeModelCacheKey key in array)
			{
				capeModelCache.Remove(key);
			}
			capeTextureRequests.Remove(source);
			QueueTextureRefresh();
		}
		catch
		{
			capeTextureRequests.Remove(source);
		}
	}

	private void QueueTextureRefresh()
	{
		if (isRebuilding || isAnimating)
		{
			rebuildAfterAnimation = true;
		}
		else
		{
			QueueRebuild();
		}
	}

	private static BitmapSource FreezeTexture(BitmapSource texture)
	{
		BitmapSource result = texture;
		if (texture is BitmapImage { IsDownloading: not false })
		{
			return result;
		}
		if (!((Freezable)texture).IsFrozen && ((Freezable)texture).CanFreeze)
		{
			((Freezable)texture).Freeze();
			result = texture;
		}
		return result;
	}

	private static Transform3D CombineTransforms(Transform3D existing, Transform3D added)
	{
		if (existing == Transform3D.Identity)
		{
			return added;
		}
		return new Transform3DGroup
		{
			Children = { existing, added }
		};
	}

	private static CapeCarouselSlotPlacement ResolveStartPlacement(AccountCapeOption cape, CapeCarouselSlot targetSlot, IReadOnlyList<SlotVisual> oldSlotVisuals, CapeCarouselDirection? direction)
	{
		if (!direction.HasValue)
		{
			return CapeCarousel3DLayout.GetPlacement(targetSlot);
		}
		return (CapeCarouselSlotPlacement)(oldSlotVisuals.FirstOrDefault((SlotVisual visual) => CapesMatch(visual.Cape, cape))?.GetCurrentPlacement() ?? (direction switch
		{
			CapeCarouselDirection.Next => CapeCarousel3DLayout.GetEntryPlacement(CapeCarouselDirection.Next), 
			CapeCarouselDirection.Previous => CapeCarousel3DLayout.GetEntryPlacement(CapeCarouselDirection.Previous), 
			_ => CapeCarousel3DLayout.GetPlacement(targetSlot), 
		}));
	}

	private void AnimateSlots()
	{
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		viewportRenderCache.Disable("CarouselAnimation");
		PowerEase easing = new PowerEase
		{
			EasingMode = EasingMode.EaseOut,
			Power = 7.0
		};
		isAnimating = true;
		int generation = ++animationGeneration;
		foreach (SlotVisual value in currentSlotVisuals.Values)
		{
			value.Translate.BeginAnimation(TranslateTransform3D.OffsetXProperty, CreateAnimation(value.TargetPlacement.X, easing));
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
				if (!rebuildAfterAnimation)
				{
					QueueViewportRenderCache();
				}
				else
				{
					rebuildAfterAnimation = false;
					QueueRebuild();
				}
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
		capeTextureRequests.Clear();
		rebuildQueued = false;
		isRebuilding = false;
		isAnimating = false;
		rebuildAfterAnimation = false;
	}

	private void QueueViewportRenderCache()
	{
		viewportRenderCache.QueueEnable(() => !isAnimating && !isRebuilding && !rebuildQueued && currentSlotVisuals.Count > 0);
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
		CapeCarouselSlot? capeCarouselSlot = HitTestSlot(e.GetPosition(this));
		if (capeCarouselSlot.HasValue && capeCarouselSlot.GetValueOrDefault() == CapeCarouselSlot.Left)
		{
			ExecuteCommand(PreviousCommand);
		}
		else if (capeCarouselSlot.HasValue && capeCarouselSlot == CapeCarouselSlot.Right)
		{
			ExecuteCommand(NextCommand);
		}
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		CapeCarouselSlot? capeCarouselSlot = HitTestSlot(e.GetPosition(this));
		bool flag = CanClickSlot(capeCarouselSlot);
		base.Cursor = (flag ? Cursors.Hand : Cursors.Arrow);
		UpdateHoverHint(flag ? capeCarouselSlot : ((CapeCarouselSlot?)null));
	}

	private CapeCarouselSlot? HitTestSlot(Point point)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		CapeCarouselSlot? slot = null;
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

	private bool CanClickSlot(CapeCarouselSlot? slot)
	{
		if (isAnimating)
		{
			return false;
		}
		return slot switch
		{
			CapeCarouselSlot.Left => PreviousCommand?.CanExecute(null) ?? false, 
			CapeCarouselSlot.Right => NextCommand?.CanExecute(null) ?? false, 
			_ => false, 
		};
	}

	private void UpdateHoverHint(CapeCarouselSlot? slot)
	{
		leftHoverHint.Visibility = (((slot ?? CapeCarouselSlot.Center) != CapeCarouselSlot.Left) ? Visibility.Collapsed : Visibility.Visible);
		rightHoverHint.Visibility = ((!slot.HasValue || slot != CapeCarouselSlot.Right) ? Visibility.Collapsed : Visibility.Visible);
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
		border.Width = 116.0;
		border.Height = 154.0;
		border.CornerRadius = new CornerRadius(10.0);
		border.HorizontalAlignment = HorizontalAlignment.Center;
		border.VerticalAlignment = VerticalAlignment.Center;
		border.Visibility = Visibility.Collapsed;
		border.SetResourceReference(Border.BackgroundProperty, "Brush.Control.Hover");
		return border;
	}

	private static void PositionHoverHint(Border hint, CapeCarouselSlot slot)
	{
		int num = ((slot == CapeCarouselSlot.Left) ? (-175) : 175);
		hint.RenderTransform = new TranslateTransform(num, 0.0);
	}

	private static void ExecuteCommand(ICommand? command)
	{
		if (command != null && command.CanExecute(null))
		{
			command.Execute(null);
		}
	}

	private void RegisterHitModels(Model3D model, CapeCarouselSlot slot)
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

	private static bool CapesMatch(AccountCapeOption? left, AccountCapeOption? right)
	{
		return CapeCarousel3DLayout.CapesRepresentSameVisualItem(left, right);
	}

	static CapeCarousel3DControl()
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
		PreviousCapeProperty = DependencyProperty.Register("PreviousCape", typeof(AccountCapeOption), typeof(CapeCarousel3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnCapePropertyChanged)));
		SelectedCapeProperty = DependencyProperty.Register("SelectedCape", typeof(AccountCapeOption), typeof(CapeCarousel3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnSelectedCapeChanged)));
		NextCapeProperty = DependencyProperty.Register("NextCape", typeof(AccountCapeOption), typeof(CapeCarousel3DControl), new PropertyMetadata((object)null, new PropertyChangedCallback(OnCapePropertyChanged)));
		PreviousCommandProperty = DependencyProperty.Register("PreviousCommand", typeof(ICommand), typeof(CapeCarousel3DControl), new PropertyMetadata((PropertyChangedCallback)null));
		NextCommandProperty = DependencyProperty.Register("NextCommand", typeof(ICommand), typeof(CapeCarousel3DControl), new PropertyMetadata((PropertyChangedCallback)null));
	}
}
