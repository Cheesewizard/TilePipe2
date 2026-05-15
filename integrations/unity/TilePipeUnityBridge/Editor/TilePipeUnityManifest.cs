using System;

namespace TilePipe.UnityBridge.Editor
{
	[Serializable]
	public sealed class TilePipeUnityManifest
	{
		public string format;
		public int version;
		public string tile_name;
		public string atlas_path;
		public string project_dir;
		public string tile_file;
		public TilePipeSourceFiles source_files;
		public TilePipeVector2 tile_size;
		public TilePipeVector2 spacing;
		public TilePipeVector2 template_size;
		public TilePipeVector2 frame_size;
		public int frame_count;
		public TilePipeSpriteRect[] sprites;
	}

	[Serializable]
	public sealed class TilePipeSourceFiles
	{
		public string tile;
		public string texture;
		public string ruleset;
		public string template;
	}

	[Serializable]
	public sealed class TilePipeVector2
	{
		public float x;
		public float y;
	}

	[Serializable]
	public sealed class TilePipeSpriteRect
	{
		public string name;
		public int mask;
		public int frame_index;
		public int variant_index;
		public TilePipeVector2 template_position;
		public TilePipeRect rect;
	}

	[Serializable]
	public sealed class TilePipeRect
	{
		public int x;
		public int y;
		public int width;
		public int height;
	}

	[Serializable]
	public sealed class TilePipeHeadlessResponse
	{
		public bool ok;
		public string command;
		public string[] warnings;
		public string[] errors;
	}
}
