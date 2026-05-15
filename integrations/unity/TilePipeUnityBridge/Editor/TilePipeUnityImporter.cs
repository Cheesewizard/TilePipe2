using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TilePipe.UnityBridge.Editor
{
	public static class TilePipeUnityImporter
	{
		private const string MANIFEST_FORMAT = "tilepipe2_unity_ruletile_manifest";
		private const int NEIGHBOR_THIS = 1;
		private const int NEIGHBOR_NOT_THIS = 2;

		private static readonly Vector3Int[] neighborPositions =
		{
			new Vector3Int(0, 1, 0),
			new Vector3Int(1, 1, 0),
			new Vector3Int(1, 0, 0),
			new Vector3Int(1, -1, 0),
			new Vector3Int(0, -1, 0),
			new Vector3Int(-1, -1, 0),
			new Vector3Int(-1, 0, 0),
			new Vector3Int(-1, 1, 0)
		};

		private static readonly int[] maskValues =
		{
			1,
			2,
			4,
			8,
			16,
			32,
			64,
			128
		};

		public static bool GenerateAndImport(TilePipeUnitySettings settings)
		{
			try
			{
				ValidateGenerationSettings(settings);
				EnsureAssetParentDirectory(settings.atlasAssetPath);
				EnsureAssetParentDirectory(settings.manifestAssetPath);
				EnsureAssetParentDirectory(settings.ruleTileAssetPath);

				var requestPath = Path.Combine(Path.GetTempPath(), "tilepipe_unity_request_" + Guid.NewGuid().ToString("N") + ".json");
				var responsePath = Path.Combine(Path.GetTempPath(), "tilepipe_unity_response_" + Guid.NewGuid().ToString("N") + ".json");
				var temporaryAtlasPath = Path.Combine(Path.GetTempPath(), "tilepipe_unity_atlas_" + Guid.NewGuid().ToString("N") + ".png");
				var temporaryManifestPath = Path.Combine(Path.GetTempPath(), "tilepipe_unity_manifest_" + Guid.NewGuid().ToString("N") + ".json");
				File.WriteAllText(
					requestPath,
					BuildRequestJson(settings, temporaryAtlasPath, temporaryManifestPath),
					Encoding.UTF8);

				var process = new Process();
				process.StartInfo = new ProcessStartInfo
				{
					FileName = settings.godotExecutable,
					Arguments = BuildGodotArguments(settings, requestPath, responsePath),
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};

				process.Start();
				var standardOutput = process.StandardOutput.ReadToEnd();
				var standardError = process.StandardError.ReadToEnd();
				process.WaitForExit();

				var response = File.Exists(responsePath)
					? JsonUtility.FromJson<TilePipeHeadlessResponse>(File.ReadAllText(responsePath))
					: null;

				DeleteTemporaryFile(requestPath);
				DeleteTemporaryFile(responsePath);

				if (process.ExitCode != 0 || response == null || !response.ok)
				{
					DeleteTemporaryFile(temporaryAtlasPath);
					DeleteTemporaryFile(temporaryManifestPath);
					var message = BuildFailureMessage(process.ExitCode, response, standardOutput, standardError);
					Debug.LogError(message);
					EditorUtility.DisplayDialog("TilePipe export failed", message, "OK");
					return false;
				}

				PromoteGeneratedFiles(settings, temporaryAtlasPath, temporaryManifestPath);
				DeleteTemporaryFile(temporaryAtlasPath);
				DeleteTemporaryFile(temporaryManifestPath);
				return ImportManifest(settings.manifestAssetPath, settings.ruleTileAssetPath);
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				EditorUtility.DisplayDialog("TilePipe import failed", exception.Message, "OK");
				return false;
			}
		}

		public static bool ImportManifest(string manifestAssetPath, string ruleTileAssetPath)
		{
			try
			{
				if (!File.Exists(ToAbsolutePath(manifestAssetPath)))
				{
					throw new FileNotFoundException("Manifest asset was not found.", manifestAssetPath);
				}

				var manifest = LoadManifest(manifestAssetPath);
				var atlasAssetPath = ToAssetPath(manifest.atlas_path);
				ImportAtlasSprites(manifest, atlasAssetPath);
				CreateOrUpdateRuleTile(manifest, atlasAssetPath, ruleTileAssetPath);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				Debug.Log("TilePipe Unity import completed: " + ruleTileAssetPath);
				return true;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				EditorUtility.DisplayDialog("TilePipe import failed", exception.Message, "OK");
				return false;
			}
		}

		public static TilePipeUnityManifest LoadManifest(string manifestAssetPath)
		{
			var manifestText = File.ReadAllText(ToAbsolutePath(manifestAssetPath));
			var manifest = JsonUtility.FromJson<TilePipeUnityManifest>(manifestText);
			if (manifest == null || manifest.format != MANIFEST_FORMAT)
			{
				throw new InvalidDataException("The selected file is not a TilePipe Unity RuleTile manifest.");
			}
			if (manifest.version != 1)
			{
				throw new InvalidDataException("Unsupported TilePipe Unity manifest version: " + manifest.version);
			}
			if (manifest.sprites == null || manifest.sprites.Length == 0)
			{
				throw new InvalidDataException("TilePipe Unity manifest contains no sprites.");
			}
			return manifest;
		}

		public static string ToAbsolutePath(string assetOrAbsolutePath)
		{
			if (Path.IsPathRooted(assetOrAbsolutePath))
			{
				return Path.GetFullPath(assetOrAbsolutePath);
			}
			var projectRoot = Directory.GetParent(Application.dataPath);
			if (projectRoot == null)
			{
				throw new InvalidOperationException("Could not resolve the Unity project root.");
			}
			return Path.GetFullPath(Path.Combine(projectRoot.FullName, assetOrAbsolutePath));
		}

		public static string ToAssetPath(string assetOrAbsolutePath)
		{
			var normalized = assetOrAbsolutePath.Replace('\\', '/');
			if (normalized.StartsWith("Assets/", StringComparison.Ordinal))
			{
				return normalized;
			}

			var absolute = Path.GetFullPath(assetOrAbsolutePath).Replace('\\', '/');
			var dataPath = Application.dataPath.Replace('\\', '/');
			if (!absolute.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Path must be inside this Unity project's Assets folder: " + assetOrAbsolutePath);
			}
			return "Assets/" + absolute.Substring(dataPath.Length + 1);
		}

		private static void ValidateGenerationSettings(TilePipeUnitySettings settings)
		{
			if (string.IsNullOrWhiteSpace(settings.godotExecutable))
			{
				throw new InvalidOperationException("Godot executable path is required.");
			}
			if (string.IsNullOrWhiteSpace(settings.tilePipeRoot) || !Directory.Exists(settings.tilePipeRoot))
			{
				throw new DirectoryNotFoundException("TilePipe2 root folder was not found: " + settings.tilePipeRoot);
			}
			if (string.IsNullOrWhiteSpace(settings.projectDir) || !Directory.Exists(settings.projectDir))
			{
				throw new DirectoryNotFoundException("TilePipe project folder was not found: " + settings.projectDir);
			}
			if (string.IsNullOrWhiteSpace(settings.tileFile))
			{
				throw new InvalidOperationException("TilePipe tile file is required.");
			}
			if (!settings.atlasAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Atlas output must be an Assets-relative path.");
			}
			if (!settings.manifestAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Manifest output must be an Assets-relative path.");
			}
			if (!settings.ruleTileAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
			{
				throw new InvalidOperationException("RuleTile output must be an Assets-relative path.");
			}
			if (!settings.ruleTileAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("RuleTile output must use the .asset extension.");
			}
		}

		private static void EnsureAssetParentDirectory(string assetPath)
		{
			var absolutePath = ToAbsolutePath(assetPath);
			var directory = Path.GetDirectoryName(absolutePath);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
		}

		private static string BuildRequestJson(
			TilePipeUnitySettings settings,
			string atlasPath,
			string manifestPath)
		{
			return "{\n" +
				"\t\"command\": \"export_unity_rule_tile\",\n" +
				"\t\"project_dir\": \"" + EscapeJson(settings.projectDir) + "\",\n" +
				"\t\"tile_file\": \"" + EscapeJson(settings.tileFile) + "\",\n" +
				"\t\"output_path\": \"" + EscapeJson(atlasPath) + "\",\n" +
				"\t\"manifest_path\": \"" + EscapeJson(manifestPath) + "\"\n" +
				"}";
		}

		private static void PromoteGeneratedFiles(
			TilePipeUnitySettings settings,
			string temporaryAtlasPath,
			string temporaryManifestPath)
		{
			var atlasPath = ToAbsolutePath(settings.atlasAssetPath);
			var manifestPath = ToAbsolutePath(settings.manifestAssetPath);
			File.Copy(temporaryAtlasPath, atlasPath, true);

			var manifest = JsonUtility.FromJson<TilePipeUnityManifest>(File.ReadAllText(temporaryManifestPath));
			if (manifest == null)
			{
				throw new InvalidDataException("TilePipe generated an invalid Unity manifest.");
			}
			manifest.atlas_path = atlasPath;
			File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
		}

		private static string BuildGodotArguments(TilePipeUnitySettings settings, string requestPath, string responsePath)
		{
			return "--no-window --path " + Quote(settings.tilePipeRoot) +
				" --script res://src/headless/TilePipeHeadless.gd" +
				" --request " + Quote(requestPath) +
				" --response " + Quote(responsePath);
		}

		private static string BuildFailureMessage(
			int exitCode,
			TilePipeHeadlessResponse response,
			string standardOutput,
			string standardError)
		{
			var builder = new StringBuilder();
			builder.AppendLine("TilePipe headless export failed with exit code " + exitCode + ".");
			if (response != null && response.errors != null)
			{
				for (var i = 0; i < response.errors.Length; i++)
				{
					builder.AppendLine(response.errors[i]);
				}
			}
			if (!string.IsNullOrWhiteSpace(standardError))
			{
				builder.AppendLine(standardError.Trim());
			}
			if (!string.IsNullOrWhiteSpace(standardOutput))
			{
				builder.AppendLine(standardOutput.Trim());
			}
			return builder.ToString();
		}

		private static void DeleteTemporaryFile(string path)
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}

		private static string EscapeJson(string value)
		{
			return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		private static string Quote(string value)
		{
			return "\"" + value.Replace("\"", "\\\"") + "\"";
		}

		private static void ImportAtlasSprites(TilePipeUnityManifest manifest, string atlasAssetPath)
		{
			AssetDatabase.ImportAsset(atlasAssetPath, ImportAssetOptions.ForceUpdate);
			var importer = AssetImporter.GetAtPath(atlasAssetPath) as TextureImporter;
			if (importer == null)
			{
				throw new InvalidOperationException("Atlas is not a texture asset: " + atlasAssetPath);
			}

			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Multiple;
			importer.filterMode = FilterMode.Point;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.mipmapEnabled = false;
			importer.SaveAndReimport();

			var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasAssetPath);
			if (texture == null)
			{
				throw new InvalidOperationException("Could not load imported atlas texture: " + atlasAssetPath);
			}

			var factory = new SpriteDataProviderFactories();
			factory.Init();
			var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
			dataProvider.InitSpriteEditorDataProvider();

			var spriteRects = new SpriteRect[manifest.sprites.Length];
			var nameFileIdPairs = new List<SpriteNameFileIdPair>(manifest.sprites.Length);
			for (var i = 0; i < manifest.sprites.Length; i++)
			{
				var spriteData = manifest.sprites[i];
				var spriteId = GUID.Generate();
				var unityY = texture.height - spriteData.rect.y - spriteData.rect.height;
				spriteRects[i] = new SpriteRect
				{
					name = spriteData.name,
					spriteID = spriteId,
					rect = new Rect(spriteData.rect.x, unityY, spriteData.rect.width, spriteData.rect.height),
					alignment = SpriteAlignment.Center,
					pivot = new Vector2(0.5f, 0.5f)
				};
				nameFileIdPairs.Add(new SpriteNameFileIdPair(spriteData.name, spriteId));
			}

			dataProvider.SetSpriteRects(spriteRects);
			var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
			if (nameFileIdProvider != null)
			{
				nameFileIdProvider.SetNameFileIdPairs(nameFileIdPairs);
			}
			dataProvider.Apply();
			importer.SaveAndReimport();
		}

		private static void CreateOrUpdateRuleTile(TilePipeUnityManifest manifest, string atlasAssetPath, string ruleTileAssetPath)
		{
			var ruleTileType = GetRuleTileType();
			var ruleTile = AssetDatabase.LoadAssetAtPath(ruleTileAssetPath, ruleTileType);
			if (ruleTile == null)
			{
				ruleTile = ScriptableObject.CreateInstance(ruleTileType);
				AssetDatabase.CreateAsset(ruleTile, ruleTileAssetPath);
			}

			var spritesByName = LoadSpritesByName(atlasAssetPath);
			var spritesByMask = BuildSpritesByMask(manifest, spritesByName);
			SetField(ruleTile, "m_DefaultSprite", GetDefaultSprite(spritesByMask));
			SetRuleTileRules(ruleTile, ruleTileType, spritesByMask);
			EditorUtility.SetDirty(ruleTile);
		}

		private static Type GetRuleTileType()
		{
			var ruleTileType = Type.GetType("UnityEngine.RuleTile, Unity.2D.Tilemap.Extras");
			if (ruleTileType == null)
			{
				var assemblies = AppDomain.CurrentDomain.GetAssemblies();
				for (var i = 0; i < assemblies.Length; i++)
				{
					ruleTileType = assemblies[i].GetType("UnityEngine.RuleTile");
					if (ruleTileType != null)
					{
						break;
					}
				}
			}
			if (ruleTileType == null)
			{
				throw new InvalidOperationException(
					"Unity 2D Tilemap Extras is required. Install package com.unity.2d.tilemap.extras from Package Manager.");
			}
			return ruleTileType;
		}

		private static Dictionary<string, Sprite> LoadSpritesByName(string atlasAssetPath)
		{
			var sprites = new Dictionary<string, Sprite>();
			var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(atlasAssetPath);
			for (var i = 0; i < assets.Length; i++)
			{
				if (assets[i] is Sprite sprite)
				{
					sprites[sprite.name] = sprite;
				}
			}
			return sprites;
		}

		private static Dictionary<int, List<Sprite>> BuildSpritesByMask(
			TilePipeUnityManifest manifest,
			Dictionary<string, Sprite> spritesByName)
		{
			var spritesByMask = new Dictionary<int, List<Sprite>>();
			for (var i = 0; i < manifest.sprites.Length; i++)
			{
				var spriteData = manifest.sprites[i];
				if (!spritesByName.TryGetValue(spriteData.name, out var sprite))
				{
					throw new InvalidOperationException("Could not find imported sprite: " + spriteData.name);
				}
				if (!spritesByMask.TryGetValue(spriteData.mask, out var maskSprites))
				{
					maskSprites = new List<Sprite>();
					spritesByMask.Add(spriteData.mask, maskSprites);
				}
				maskSprites.Add(sprite);
			}
			return spritesByMask;
		}

		private static Sprite GetDefaultSprite(Dictionary<int, List<Sprite>> spritesByMask)
		{
			if (spritesByMask.TryGetValue(255, out var fullSprites) && fullSprites.Count > 0)
			{
				return fullSprites[0];
			}
			foreach (var entry in spritesByMask)
			{
				if (entry.Value.Count > 0)
				{
					return entry.Value[0];
				}
			}
			return null;
		}

		private static void SetRuleTileRules(
			UnityEngine.Object ruleTile,
			Type ruleTileType,
			Dictionary<int, List<Sprite>> spritesByMask)
		{
			var tilingRuleType = ruleTileType.GetNestedType("TilingRule", BindingFlags.Public);
			if (tilingRuleType == null)
			{
				throw new InvalidOperationException("Could not find RuleTile.TilingRule type.");
			}

			var listType = typeof(List<>).MakeGenericType(tilingRuleType);
			var ruleList = (IList)Activator.CreateInstance(listType);
			var masks = new List<int>(spritesByMask.Keys);
			masks.Sort();
			for (var i = masks.Count - 1; i >= 0; i--)
			{
				var mask = masks[i];
				var rule = Activator.CreateInstance(tilingRuleType);
				SetField(rule, "m_NeighborPositions", new List<Vector3Int>(neighborPositions));
				SetField(rule, "m_Neighbors", BuildNeighborRules(mask));
				SetField(rule, "m_Sprites", spritesByMask[mask].ToArray());
				SetEnumField(rule, "m_Output", spritesByMask[mask].Count > 1 ? "Random" : "Single");
				SetEnumField(rule, "m_RuleTransform", "Fixed");
				SetField(rule, "m_PerlinScale", 0.5f);
				ruleList.Add(rule);
			}
			SetField(ruleTile, "m_TilingRules", ruleList);
		}

		private static List<int> BuildNeighborRules(int mask)
		{
			var neighbors = new List<int>(maskValues.Length);
			for (var i = 0; i < maskValues.Length; i++)
			{
				neighbors.Add((mask & maskValues[i]) != 0 ? NEIGHBOR_THIS : NEIGHBOR_NOT_THIS);
			}
			return neighbors;
		}

		private static void SetEnumField(object target, string fieldName, string valueName)
		{
			var field = GetField(target.GetType(), fieldName);
			if (field == null)
			{
				return;
			}
			var value = Enum.Parse(field.FieldType, valueName);
			field.SetValue(target, value);
		}

		private static void SetField(object target, string fieldName, object value)
		{
			var field = GetField(target.GetType(), fieldName);
			if (field == null)
			{
				throw new InvalidOperationException("Could not find field " + fieldName + " on " + target.GetType().FullName + ".");
			}
			field.SetValue(target, value);
		}

		private static FieldInfo GetField(Type type, string fieldName)
		{
			while (type != null)
			{
				var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (field != null)
				{
					return field;
				}
				type = type.BaseType;
			}
			return null;
		}
	}
}
