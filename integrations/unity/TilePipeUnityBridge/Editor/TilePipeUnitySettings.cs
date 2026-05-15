using System;
using UnityEditor;

namespace TilePipe.UnityBridge.Editor
{
	[Serializable]
	public sealed class TilePipeUnitySettings
	{
		private const string GODOT_EXECUTABLE_KEY = "TilePipe.UnityBridge.GodotExecutable";
		private const string TILEPIPE_ROOT_KEY = "TilePipe.UnityBridge.TilePipeRoot";
		private const string PROJECT_DIR_KEY = "TilePipe.UnityBridge.ProjectDir";
		private const string TILE_FILE_KEY = "TilePipe.UnityBridge.TileFile";
		private const string ATLAS_ASSET_KEY = "TilePipe.UnityBridge.AtlasAsset";
		private const string MANIFEST_ASSET_KEY = "TilePipe.UnityBridge.ManifestAsset";
		private const string RULE_TILE_ASSET_KEY = "TilePipe.UnityBridge.RuleTileAsset";
		private const string LIVE_REIMPORT_KEY = "TilePipe.UnityBridge.LiveReimport";

		public string godotExecutable;
		public string tilePipeRoot;
		public string projectDir;
		public string tileFile;
		public string atlasAssetPath;
		public string manifestAssetPath;
		public string ruleTileAssetPath;
		public bool liveReimport;

		public static TilePipeUnitySettings Load()
		{
			return new TilePipeUnitySettings
			{
				godotExecutable = EditorPrefs.GetString(GODOT_EXECUTABLE_KEY, "godot"),
				tilePipeRoot = EditorPrefs.GetString(TILEPIPE_ROOT_KEY, string.Empty),
				projectDir = EditorPrefs.GetString(PROJECT_DIR_KEY, string.Empty),
				tileFile = EditorPrefs.GetString(TILE_FILE_KEY, string.Empty),
				atlasAssetPath = EditorPrefs.GetString(ATLAS_ASSET_KEY, "Assets/TilePipe/GeneratedAtlas.png"),
				manifestAssetPath = EditorPrefs.GetString(MANIFEST_ASSET_KEY, "Assets/TilePipe/GeneratedAtlas.tilepipe-unity.json"),
				ruleTileAssetPath = EditorPrefs.GetString(RULE_TILE_ASSET_KEY, "Assets/TilePipe/GeneratedRuleTile.asset"),
				liveReimport = EditorPrefs.GetBool(LIVE_REIMPORT_KEY, false)
			};
		}

		public void Save()
		{
			EditorPrefs.SetString(GODOT_EXECUTABLE_KEY, godotExecutable ?? string.Empty);
			EditorPrefs.SetString(TILEPIPE_ROOT_KEY, tilePipeRoot ?? string.Empty);
			EditorPrefs.SetString(PROJECT_DIR_KEY, projectDir ?? string.Empty);
			EditorPrefs.SetString(TILE_FILE_KEY, tileFile ?? string.Empty);
			EditorPrefs.SetString(ATLAS_ASSET_KEY, atlasAssetPath ?? string.Empty);
			EditorPrefs.SetString(MANIFEST_ASSET_KEY, manifestAssetPath ?? string.Empty);
			EditorPrefs.SetString(RULE_TILE_ASSET_KEY, ruleTileAssetPath ?? string.Empty);
			EditorPrefs.SetBool(LIVE_REIMPORT_KEY, liveReimport);
		}
	}
}
