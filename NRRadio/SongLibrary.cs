using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NRRadio
{
	/// <summary>
	/// Scene-specific music folders: Music/&lt;scene&gt;/song.ogg. A folder name may list several scenes
	/// separated by spaces (e.g. "garage main_menu"), and a folder called "all" is used in every scene.
	/// </summary>
	internal static class SongLibrary
	{
		public static readonly string RootPath = Path.Combine(BepInEx.Paths.GameRootPath, "Music");

		public static readonly GodConstant.Music_State[] Scenes =
		{
			GodConstant.Music_State.main_menu,
			GodConstant.Music_State.garage,
			GodConstant.Music_State.cruise,
			GodConstant.Music_State.meetspot,
			GodConstant.Music_State.race,
		};

		private static readonly Dictionary<string, AudioType> Types = new(StringComparer.OrdinalIgnoreCase)
		{
			[".wav"] = AudioType.WAV,
			[".ogg"] = AudioType.OGGVORBIS,
			[".mp3"] = AudioType.MPEG,
			[".flac"] = AudioType.FLAC,
		};

		public static bool IsSceneSupported(GodConstant.Music_State scene) => Scenes.Contains(scene);

		public static AudioType TypeOf(string path) => Types[Path.GetExtension(path)];

		public static void EnsureFolders()
		{
			foreach (var scene in Scenes)
				Directory.CreateDirectory(Path.Combine(RootPath, scene.ToString()));
			Directory.CreateDirectory(Path.Combine(RootPath, "all"));

			var readme = Path.Combine(RootPath, "README.txt");
			if (!File.Exists(readme))
			{
				File.WriteAllText(readme,
					"Put songs (.ogg .wav .mp3 .flac) in the folder for the scene they should play in:\r\n" +
					"  main_menu, garage, cruise, meetspot, race\r\n" +
					"Songs in 'all' play in every scene.\r\n" +
					"To use a song in several scenes, make a folder named after them separated by spaces,\r\n" +
					"for example: 'garage cruise'.\r\n");
			}
		}

		/// <summary>Every supported audio file that applies to this scene.</summary>
		public static List<string> TracksFor(GodConstant.Music_State scene)
		{
			var name = scene.ToString();
			var result = new List<string>();
			if (!Directory.Exists(RootPath)) return result;

			foreach (var dir in Directory.GetDirectories(RootPath))
			{
				var parts = Path.GetFileName(dir).Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (!parts.Contains(name, StringComparer.OrdinalIgnoreCase) &&
				    !parts.Contains("all", StringComparer.OrdinalIgnoreCase)) continue;

				result.AddRange(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
					.Where(p => Types.ContainsKey(Path.GetExtension(p))));
			}
			return result.Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
		}
	}

	/// <summary>Shuffle bag + history for one scene, so every song plays once before repeats.</summary>
	internal class Playlist
	{
		private readonly System.Random _rng = new();
		private readonly Queue<string> _bag = new();
		private readonly List<string> _history = new();
		private int _cursor = -1; // index into _history of the song currently playing

		public string Next(GodConstant.Music_State scene)
		{
			if (_cursor + 1 < _history.Count) return _history[++_cursor];

			if (_bag.Count == 0)
			{
				var all = SongLibrary.TracksFor(scene);
				if (all.Count == 0) return null;
				var last = _history.Count > 0 ? _history[^1] : null;
				var shuffled = all.OrderBy(_ => _rng.Next()).ToList();
				if (shuffled.Count > 1 && shuffled[0] == last)
				{
					shuffled.Add(shuffled[0]);
					shuffled.RemoveAt(0);
				}
				foreach (var t in shuffled) _bag.Enqueue(t);
			}

			var song = _bag.Dequeue();
			_history.Add(song);
			if (_history.Count > 50) _history.RemoveAt(0);
			_cursor = _history.Count - 1;
			return song;
		}

		public string Previous()
		{
			if (_cursor <= 0) return null;
			return _history[--_cursor];
		}
	}
}
