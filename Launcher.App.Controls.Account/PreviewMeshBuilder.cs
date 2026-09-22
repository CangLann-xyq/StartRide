using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Launcher.App.Controls.Account;

internal sealed class PreviewMeshBuilder
{
	private readonly Point3DCollection positions = new Point3DCollection();

	private readonly PointCollection textureCoordinates = new PointCollection();

	private readonly Int32Collection triangleIndices = new Int32Collection();

	internal int QuadCount => positions.Count / 4;

	internal void AddQuad(Point3D p0, Point3D p1, Point3D p2, Point3D p3, Rect textureRect, bool reverseWinding = false)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		int count = positions.Count;
		positions.Add(p0);
		positions.Add(p1);
		positions.Add(p2);
		positions.Add(p3);
		textureCoordinates.Add(new Point(textureRect.Left, textureRect.Top));
		textureCoordinates.Add(new Point(textureRect.Right, textureRect.Top));
		textureCoordinates.Add(new Point(textureRect.Right, textureRect.Bottom));
		textureCoordinates.Add(new Point(textureRect.Left, textureRect.Bottom));
		if (reverseWinding)
		{
			triangleIndices.Add(count);
			triangleIndices.Add(count + 2);
			triangleIndices.Add(count + 1);
			triangleIndices.Add(count);
			triangleIndices.Add(count + 3);
			triangleIndices.Add(count + 2);
		}
		else
		{
			triangleIndices.Add(count);
			triangleIndices.Add(count + 1);
			triangleIndices.Add(count + 2);
			triangleIndices.Add(count);
			triangleIndices.Add(count + 2);
			triangleIndices.Add(count + 3);
		}
	}

	internal MeshGeometry3D Build(bool anchorUnitTextureCoordinates = false)
	{
		if (anchorUnitTextureCoordinates && positions.Count > 0)
		{
			AddTextureCoordinateAnchor();
		}
		((Freezable)positions).Freeze();
		((Freezable)textureCoordinates).Freeze();
		((Freezable)triangleIndices).Freeze();
		MeshGeometry3D obj = new MeshGeometry3D
		{
			Positions = positions,
			TextureCoordinates = textureCoordinates,
			TriangleIndices = triangleIndices
		};
		((Freezable)obj).Freeze();
		return obj;
	}

	private void AddTextureCoordinateAnchor()
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		int count = positions.Count;
		Point3D value = positions[0];
		positions.Add(value);
		positions.Add(value);
		positions.Add(value);
		textureCoordinates.Add(new Point(0.0, 0.0));
		textureCoordinates.Add(new Point(1.0, 0.0));
		textureCoordinates.Add(new Point(1.0, 1.0));
		triangleIndices.Add(count);
		triangleIndices.Add(count + 1);
		triangleIndices.Add(count + 2);
	}
}
