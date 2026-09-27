using System.Windows;
using System.Windows.Media;

namespace StartRide.App.Controls;

internal sealed record ProgressiveBlurVisualParts(FrameworkElement Owner, FrameworkElement ListLayer, FrameworkElement ListVisualSource, FrameworkElement DirectListHost, FrameworkElement BlurBandViewport, FrameworkElement BlurBandUpscaleHost, ScaleTransform BlurBandUpscaleTransform, FrameworkElement BlurBandHorizontalHost, FrameworkElement BlurBandVerticalHost, VisualBrush BlurBandBrush);
