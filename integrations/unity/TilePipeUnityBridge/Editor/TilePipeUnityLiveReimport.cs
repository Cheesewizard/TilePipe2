using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TilePipe.UnityBridge.Editor
{
	[InitializeOnLoad]
	public static class TilePipeUnityLiveReimport
	{
		private const double CHECK_INTERVAL_SECONDS = 1.0;
		private const double DEBOUNCE_SECONDS = 0.75;

		private static TilePipeUnitySettings settings;
		private static readonly Dictionary<string, DateTime> lastWriteTimes = new Dictionary<string, DateTime>();
		private static double nextCheckTime;
		private static double pendingImportTime = -1.0;
		private static bool isImporting;

		static TilePipeUnityLiveReimport()
		{
			ReloadSettings();
			EditorApplication.update += Update;
		}

		public static void ReloadSettings()
		{
			settings = TilePipeUnitySettings.Load();
			lastWriteTimes.Clear();
			pendingImportTime = -1.0;
			PrimeWatchedFiles();
		}

		private static void Update()
		{
			if (settings == null || !settings.liveReimport || isImporting)
			{
				return;
			}

			var now = EditorApplication.timeSinceStartup;
			if (pendingImportTime > 0.0 && now >= pendingImportTime)
			{
				RunImport();
				return;
			}

			if (now < nextCheckTime)
			{
				return;
			}

			nextCheckTime = now + CHECK_INTERVAL_SECONDS;
			if (HasWatchedFileChanged())
			{
				pendingImportTime = now + DEBOUNCE_SECONDS;
			}
		}

		private static void RunImport()
		{
			isImporting = true;
			pendingImportTime = -1.0;
			try
			{
				if (TilePipeUnityImporter.GenerateAndImport(settings))
				{
					PrimeWatchedFiles();
				}
			}
			finally
			{
				isImporting = false;
			}
		}

		private static bool HasWatchedFileChanged()
		{
			var watchedFiles = GetWatchedFiles();
			for (var i = 0; i < watchedFiles.Count; i++)
			{
				var path = watchedFiles[i];
				if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
				{
					continue;
				}

				var lastWriteTime = File.GetLastWriteTimeUtc(path);
				if (!lastWriteTimes.TryGetValue(path, out var previousWriteTime))
				{
					lastWriteTimes[path] = lastWriteTime;
					continue;
				}

				if (lastWriteTime > previousWriteTime)
				{
					lastWriteTimes[path] = lastWriteTime;
					return true;
				}
			}
			return false;
		}

		private static void PrimeWatchedFiles()
		{
			var watchedFiles = GetWatchedFiles();
			for (var i = 0; i < watchedFiles.Count; i++)
			{
				var path = watchedFiles[i];
				if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
				{
					lastWriteTimes[path] = File.GetLastWriteTimeUtc(path);
				}
			}
		}

		private static List<string> GetWatchedFiles()
		{
			var watchedFiles = new List<string>();
			var manifestPath = string.IsNullOrWhiteSpace(settings.manifestAssetPath)
				? string.Empty
				: TilePipeUnityImporter.ToAbsolutePath(settings.manifestAssetPath);

			if (File.Exists(manifestPath))
			{
				try
				{
					var manifest = TilePipeUnityImporter.LoadManifest(settings.manifestAssetPath);
					if (manifest.source_files != null)
					{
						AddIfPresent(watchedFiles, manifest.source_files.tile);
						AddIfPresent(watchedFiles, manifest.source_files.texture);
						AddIfPresent(watchedFiles, manifest.source_files.ruleset);
						AddIfPresent(watchedFiles, manifest.source_files.template);
						return watchedFiles;
					}
				}
				catch (Exception exception)
				{
					Debug.LogWarning("TilePipe live reimport could not read manifest dependencies: " + exception.Message);
				}
			}

			if (!string.IsNullOrWhiteSpace(settings.projectDir) && !string.IsNullOrWhiteSpace(settings.tileFile))
			{
				AddIfPresent(watchedFiles, Path.Combine(settings.projectDir, settings.tileFile));
			}
			return watchedFiles;
		}

		private static void AddIfPresent(List<string> paths, string path)
		{
			if (!string.IsNullOrWhiteSpace(path))
			{
				paths.Add(Path.GetFullPath(path));
			}
		}
	}
}
