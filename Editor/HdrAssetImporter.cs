using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using System.Text;

public class HdrFile {

	public enum Format
	{
		RGBE,
		XYZE
	}

	Color32[] colors;
	public Format format;
	public int Width { get; private set; }
	public int Height { get; private set; }

	int cursor;

	public HdrFile ()
	{
	}

	public void SetDimensions (int width, int height)
	{
		this.Width = width;
		this.Height = height;
		colors = new Color32[width * height];
	}

	public void AddColor (Color32 color)
	{
		colors[cursor] = color;
		cursor++;
	}

	public Texture2D ToTexture (bool mipmap, bool linear)
	{
		var tex = new Texture2D (Width, Height, TextureFormat.ARGB32, mipmap, linear);
		tex.SetPixels32 (colors);
		return tex;
	}
}

public class HdrAssetImporter : AssetPostprocessor {

	static void OnPostprocessAllAssets (string[] imported, string[] deleted, string[] moved, string[] movedFromPath)
	{
		List<string> hdrFiles = new List<string> ();
		foreach (var f in imported) {
			if (Path.GetExtension (f) == ".hdr") {
				hdrFiles.Add (f);
			}
		}

		foreach (var hdr in hdrFiles) {
			var data = ReadHdrFile (hdr);
			var tex = data.ToTexture (false, true);
			var dir = Path.GetPathRoot (hdr);
			AssetDatabase.CreateAsset (tex, dir + "/" + Path.GetFileNameWithoutExtension (hdr) + ".asset");
		}
	}

	static HdrFile ReadHdrFile (string path)
	{
		if (!File.Exists (path)) {
			throw new System.Exception ("File does not exist");
		}

		HdrFile hdr = new HdrFile ();

		using (FileStream stream = new FileStream (path, FileMode.Open)) {
			using (StreamReader sr = new StreamReader (stream)) {
				string header = sr.ReadLine ().Trim ();
				Debug.Log (header);
				if (header != "#?RADIANCE") {
					throw new System.Exception ("File is not in Radiance HDR format.");
				}
				while (!sr.EndOfStream) {
					string line = sr.ReadLine ();
					if (line.StartsWith ("FORMAT")) {
						switch (line.Substring (7)) {
						case "32-bit_rle_rgbe":
							hdr.format = HdrFile.Format.RGBE;
							break;
						case "32-bit_rle_xyze":
							hdr.format = HdrFile.Format.XYZE;
							throw new System.Exception ("XYZE format is not supported.");
						default:
							throw new System.Exception ("HDR file is of an unknown format.");
						}
					}
					// end of header
					if (line == string.Empty) {
						break;
					}
				}
				// Read the dimensions
				string[] size = sr.ReadLine ().Split (' ');
				// FIXME assuming that it's -Y +X layout
				hdr.SetDimensions (int.Parse (size[1]), int.Parse (size[3]));
				
				StringBuilder sb = new StringBuilder ();
				while (sr.Peek () > 0) {
					byte[] bytes = new byte[4];
					stream.Read (bytes, 0, 4);
					Color32 c = new Color32 (bytes[0], bytes[1], bytes[2], bytes[3]);
					hdr.AddColor (c);
				}
			}
		}
		return hdr;
	}

//	static Color32 RgbeToColor (byte[] bytes)
//	{
//		var c = new Color32 ();
//		c.r = RgbeToColorComponent (bytes[0], bytes[3]);
//		c.g = RgbeToColorComponent (bytes[1], bytes[3]);
//		c.b = RgbeToColorComponent (bytes[2], bytes[3]);
//		c.a = 1;
//		return c;
//	}

//	static byte RgbeToColorComponent (byte v, byte e)
//	{
//		var exp = System.Convert.ToSingle (e) * 255 - 128;
//		double f = System.Convert.ToSingle (v) * System.Math.Pow (2, exp);
//		return System.Convert.ToByte (f);
//	}
}
