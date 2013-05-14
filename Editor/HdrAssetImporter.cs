using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using System.Text;

public class HdrFile {

	public enum Format {
		RGBE,
		XYZE
	}

	public enum Compression {
		Uncompressed,
		RLE,
		AdaptiveRLE
	}

	Color32[] colors;
	public Format format;
	public Compression compression;
	public int Width { get; private set; }
	public int Height { get; private set; }

	public void SetDimensions (int width, int height)
	{
		this.Width = width;
		this.Height = height;
		colors = new Color32[width * height];
	}

	public void SetColors (Color32[] c)
	{
		colors = c;
	}

	public Texture2D ToTexture (bool mipmap, bool linear)
	{
		var tex = new Texture2D (Width, Height, TextureFormat.ARGB32, mipmap, linear);
		tex.SetPixels32 (colors);
		return tex;
	}
}


// Example implementation:
// http://code.google.com/p/glorg2/source/browse/Glorg2/Glorg2/Resource/HdrImporter.cs?r=cf4dcecac8eae0e7a94bb8f03d6a63e483333a29


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
			var filename = Path.GetFileNameWithoutExtension (hdr);
			var path = Path.GetDirectoryName (hdr) + "/" + filename + ".asset";
			AssetDatabase.CreateAsset (tex, path);
			Debug.Log ("Created HDR texture asset for image file: " + filename);
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
				int width = int.Parse (size[1]);
				int height = int.Parse (size[3]);
				Color32[] buffer = new Color32[width * height];

				hdr.SetDimensions (width, height);

				StringBuilder sb = new StringBuilder ();
				Color lastColor = Color.black;
				int cursor = 0;

				while (cursor < buffer.Length) {
					byte[] rgbe = new byte[4];
					stream.Read (rgbe, 0, 4);

					if (width < 8 || width > 32767) {
						hdr.compression = HdrFile.Compression.Uncompressed;
					} else if (cursor == 0 && rgbe [0] == 2 && rgbe [1] == 2) {
						hdr.compression = HdrFile.Compression.AdaptiveRLE;
					}

					Color32 c = new Color32 (rgbe[0], rgbe[1], rgbe[2], rgbe[3]);
					if (hdr.compression == HdrFile.Compression.AdaptiveRLE) {

						// TODO
						
					} else if (c.r == 255 && c.g == 255 && c.b == 255) {
						// Old run-length encoding
						hdr.compression = HdrFile.Compression.RLE;
						cursor++;
						for (int i = 0; i < c.a; i++) {
							buffer [cursor] = lastColor;
							cursor++;
						}
					} else {
						buffer[cursor] = c;
					}
					lastColor = c;
					cursor++;
				}

				hdr.SetColors (buffer);
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
