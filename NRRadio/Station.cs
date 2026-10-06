using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NRRadio
{
	/// <summary>A radio station is a sub-folder of the Radio folder; every audio file inside is a track.</summary>
	internal class Station
	{
		public readonly string Name;
		public readonly string Folder;

		private readonly System.Random _rng = new();
		private readonly Queue<string> _bag = new();
		private string _last;

		public Station(string folder)
		{
			Folder = folder;
			Name = Path.GetFileName(folder);
		}

		public List<string> Tracks() =>
			Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories)
				.Where(StationLibrary.IsSupported)
				.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
				.ToList();

		/// <summary>Shuffled playback: every track plays once before any repeats.</summary>
		public string NextTrack()
		{
			if (_bag.Count == 0)
			{
				var all = Tracks();
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

	internal static class StationLibrary
	{
		public static readonly string RootPath = Path.Combine(BepInEx.Paths.GameRootPath, "Radio");

		private static readonly Dictionary<string, AudioType> Types = new(StringComparer.OrdinalIgnoreCase)
		{
			[".wav"] = AudioType.WAV,
			[".ogg"] = AudioType.OGGVORBIS,
			[".mp3"] = AudioType.MPEG,
			[".flac"] = AudioType.FLAC,
		};

		public static bool IsSupported(string path) => Types.ContainsKey(Path.GetExtension(path));

		public static AudioType TypeOf(string path) => Types[Path.GetExtension(path)];

		public static List<Station> Load()
		{
			Directory.CreateDirectory(RootPath);
			var stations = Directory.GetDirectories(RootPath)
				.OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
				.Select(d => new Station(d))
				.Where(s => s.Tracks().Count > 0)
				.ToList();

			if (stations.Count == 0)
			{
				// Make it obvious where to put music on first run.
				Directory.CreateDirectory(Path.Combine(RootPath, "My Station"));
				File.WriteAllText(Path.Combine(RootPath, "README.txt"),
					"Each folder in here is a radio station. Drop .ogg / .wav / .mp3 / .flac files into a folder\r\n" +
					"(for example \"My Station\") and restart the game. Folder name = station name.\r\n");
			}
			return stations;
		}
	}
}
