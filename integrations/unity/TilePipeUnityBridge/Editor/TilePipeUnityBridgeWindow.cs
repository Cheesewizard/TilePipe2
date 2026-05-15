using UnityEditor;
using UnityEngine;

namespace TilePipe.UnityBridge.Editor
{
	public sealed class TilePipeUnityBridgeWindow : EditorWindow
	{
		private TilePipeUnitySettings settings;

		[MenuItem("Tools/TilePipe/Unity Bridge")]
		public static void Open()
		{
			GetWindow<TilePipeUnityBridgeWindow>("TilePipe");
		}

		private void OnEnable()
		{
			settings = TilePipeUnitySettings.Load();
		}

		private void OnGUI()
		{
			if (settings == null)
			{
				settings = TilePipeUnitySettings.Load();
			}

			EditorGUILayout.LabelField("TilePipe Headless", EditorStyles.boldLabel);
			settings.godotExecutable = EditorGUILayout.TextField("Godot Executable", settings.godotExecutable);
			settings.tilePipeRoot = EditorGUILayout.TextField("TilePipe2 Root", settings.tilePipeRoot);
			settings.projectDir = EditorGUILayout.TextField("TilePipe Project Dir", settings.projectDir);
			settings.tileFile = EditorGUILayout.TextField("Tile File", settings.tileFile);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Unity Outputs", EditorStyles.boldLabel);
			settings.atlasAssetPath = EditorGUILayout.TextField("Atlas Asset", settings.atlasAssetPath);
			settings.manifestAssetPath = EditorGUILayout.TextField("Manifest Asset", settings.manifestAssetPath);
			settings.ruleTileAssetPath = EditorGUILayout.TextField("RuleTile Asset", settings.ruleTileAssetPath);

			EditorGUILayout.Space();
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Generate And Import"))
				{
					SaveAndConfigureLiveReimport();
					TilePipeUnityImporter.GenerateAndImport(settings);
				}

				if (GUILayout.Button("Import Existing Manifest"))
				{
					SaveAndConfigureLiveReimport();
					TilePipeUnityImporter.ImportManifest(settings.manifestAssetPath, settings.ruleTileAssetPath);
				}
			}

			EditorGUILayout.Space();
			var nextLiveReimport = EditorGUILayout.Toggle("Live Reimport", settings.liveReimport);
			if (nextLiveReimport != settings.liveReimport)
			{
				settings.liveReimport = nextLiveReimport;
				SaveAndConfigureLiveReimport();
			}

			if (GUILayout.Button("Save Settings"))
			{
				SaveAndConfigureLiveReimport();
			}
		}

		private void SaveAndConfigureLiveReimport()
		{
			settings.Save();
			TilePipeUnityLiveReimport.ReloadSettings();
		}
	}
}
