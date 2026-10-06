using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace NRRadio
{
	[BepInPlugin(Guid, "NR Radio", Version)]
	public class Plugin : BasePlugin
	{
		public const string Guid = "nrmmod.NRRadio";
		public const string Version = "0.3.0";

		internal static new ManualLogSource Log;
		internal static ConfigEntry<bool> MixEnabled;
		internal static ConfigEntry<int> CustomChancePercent;
		internal static ConfigEntry<KeyCode> PrevKey;
		internal static ConfigEntry<KeyCode> NextKey;
		internal static ConfigEntry<KeyCode> MixToggleKey;
		internal static ConfigEntry<KeyCode> FreeMemoryKey;
		internal static ConfigEntry<bool> ShowToasts;

		public override void Load()
		{
			Log = base.Log;

			MixEnabled = Config.Bind("Playlist", "MixWithGameMusic", true,
				"When on, your songs are mixed into the game's own playlist for each scene.");
			CustomChancePercent = Config.Bind("Playlist", "CustomSongChancePercent", 50,
				new ConfigDescription("Chance (0-100) that a new song is one of yours instead of the game's. 100 = only your songs when the scene folder has any.",
					new AcceptableValueRange<int>(0, 100)));
			PrevKey = Config.Bind("Keys", "PreviousCustomSong", KeyCode.O, "Play the previous custom song.");
			NextKey = Config.Bind("Keys", "NextCustomSong", KeyCode.P, "Play a custom song now (replaces what is playing).");
			MixToggleKey = Config.Bind("Keys", "ToggleMix", KeyCode.M, "Turn mixing custom songs into the game's playlist on/off.");
			FreeMemoryKey = Config.Bind("Keys", "FreeMemory", KeyCode.N, "Unload audio that is no longer used.");
			ShowToasts = Config.Bind("UI", "ShowToasts", true, "Show a small on-screen label for mod messages.");

			ClassInjector.RegisterTypeInIl2Cpp<RadioRunner>();

			var go = new GameObject("NRRadio");
			Object.DontDestroyOnLoad(go);
			go.hideFlags = HideFlags.HideAndDontSave;
			RadioRunner.Instance = go.AddComponent<RadioRunner>();

			Log.LogInfo($"NR Radio {Version} loaded. Music folder: {SongLibrary.RootPath}");
		}
	}
}
