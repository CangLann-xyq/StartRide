using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Launcher.App.Controls.Account;

internal sealed record PreviewTextureAtlas(BitmapSource Bitmap, IReadOnlyDictionary<Int32Rect, Rect> TextureCoordinates, int SourcePixelWidth, int SourcePixelHeight);
