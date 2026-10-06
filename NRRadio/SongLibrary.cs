using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlanetJem.Audio;
using UnityEngine;

namespace NRRadio
{
	/// <summary>
	/// Scene-specific music folders: Music/&lt;scene&gt;/song.ogg. A folder name may list several scenes
	/// separated by spaces (e.g. "garage cruise"), and a folder called "all" is used in garage, meetspot,
	/// cruise and race.
	/// </summary>
	internal static class SongLibrary
	{
		public static readonly string RootPath = Path.Combine(BepInEx.Paths.GameRootPath, "Music");

		// Game music states we can add songs to, and the folder name that targets each.
		private static readonly Dictionary<MusicPlayerState, string> FolderNames = new()
		{
			[MusicPlayerState.Garage] = "garage",
			[MusicPlayerState.Meetspot] = "meetspot",
			[MusicPlayerState.Cruise] = "cruise",
			[MusicPlayerState.Race] = "race",
			[MusicPlayerState.RaceWin] = "race_win",
			[MusicPlayerState.RaceLoss] = "race_loss",
		};

		private static readonly Dictionary<string, AudioType> Types = new(StringComparer.OrdinalIgnoreCase)
		{
			[".wav"] = AudioType.WAV,
			[".ogg"] = AudioType.OGGVORBIS,
			[".mp3"] = AudioType.MPEG,
			[".flac"] = AudioType.FLAC,
		};

		public static bool IsSupported(MusicPlayerState s) => FolderNames.ContainsKey(s);

		public static string NameOf(MusicPlayerState s) => FolderNames.TryGetValue(s, out var n) ? n : s.ToString();

		public static AudioType TypeOf(string path) => Types[Path.GetExtension(path)];

		private static string Norm(string s) => s.Replace("_", "").Replace("-", "").ToLowerInvariant();

		public static void EnsureFolders()
		{
			foreach (var name in FolderNames.Values)
				Directory.CreateDirectory(Path.Combine(RootPath, name));
			Directory.CreateDirectory(Path.Combine(RootPath, "all"));

			var readme = Path.Combine(RootPath, "README.txt");
			if (!File.Exists(readme))
			{
				File.WriteAllText(readme,
					"Put songs (.ogg .wav .mp3 .flac) in the folder for the scene they should play in:\r\n" +
					"  garage, meetspot, cruise, race, race_win, race_loss\r\n" +
					"Songs in 'all' play in garage, meetspot, cruise and race.\r\n" +
					"To use a song in several scenes, make a folder named after them separated by spaces,\r\n" +
					"for example: 'garage cruise'.\r\n");
			}
		}

		/// <summary>Every supported audio file that applies to this music state.</summary>
		public static List<string> TracksFor(MusicPlayerState state)
		{
			var result = new List<string>();
			if (!FolderNames.TryGetValue(state, out var name) || !Directory.Exists(RootPath)) return result;
			var wanted = Norm(name);
			var allApplies = state is MusicPlayerState.Garage or MusicPlayerState.Meetspot
				or MusicPlayerState.Cruise or MusicPlayerState.Race;

			foreach (var dir in Directory.GetDirectories(RootPath))
			{
				var parts = Path.GetFileName(dir).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Norm).ToList();
				if (!parts.Contains(wanted) && !(allApplies && parts.Contains("all"))) continue;

				result.AddRange(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
					.Where(p => Types.ContainsKey(Path.GetExtension(p))));
			}
			return result.Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
		}
	}

	/// <summary>Shuffle bag for one music state, so every song plays once before repeats.</summary>
	internal class Playlist
	{
		private readonly System.Random _rng = new();
		private readonly Queue<string> _bag = new();
		private string _last;

		public string Next(MusicPlayerState state)
		{
			if (_bag.Count == 0)
			{
				var all = SongLibrary.TracksFor(state);
				if (all.Count == 0) return null;
				var shuffled = all.OrderBy(_ => _rng.Next()).ToList();
				if (shuffled.Count > 1 && shuffled[0] == _last)
				{
					shuffled.Add(shuffled[0]);
					shuffled.RemoveAt(0);
				}
				foreach (var t in shuffled) _bag.Enqueue(t);
			}
			_last = _bag.Dequeue();
			return _last;
		}
	}
}
