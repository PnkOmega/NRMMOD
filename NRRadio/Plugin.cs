using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace NRRadio
{
	[BepInPlugin(Guid, "NR Radio", Version)]
	public class Plugin : BasePlugin
	{
		public const string Guid = "nrmmod.NRRadio";
		public const string Version = "0.1.0";

		internal static new ManualLogSource Log;
		internal static ConfigEntry<KeyCode> NextStationKey;
		internal static ConfigEntry<KeyCode> PrevStationKey;
		internal static ConfigEntry<KeyCode> NextTrackKey;
		internal static ConfigEntry<bool> ShowToasts;

		public override void Load()
		{
			Log = base.Log;

			NextStationKey = Config.Bind("Keys", "NextStation", KeyCode.F3, "Tune to the next radio station (after the last one the radio goes back to the game's own music).");
			PrevStationKey = Config.Bind("Keys", "PreviousStation", KeyCode.F4, "Tune to the previous radio station.");
			NextTrackKey = Config.Bind("Keys", "NextTrack", KeyCode.F2, "Skip to another track on the current station.");
			ShowToasts = Config.Bind("UI", "ShowToasts", true, "Show a small on-screen label when the station or track changes.");

			ClassInjector.RegisterTypeInIl2Cpp<RadioRunner>();

			var go = new GameObject("NRRadio");
			Object.DontDestroyOnLoad(go);
			go.hideFlags = HideFlags.HideAndDontSave;
			RadioRunner.Instance = go.AddComponent<RadioRunner>();

			Harmony.CreateAndPatchAll(typeof(RadioPatches));
			Log.LogInfo($"NR Radio {Version} loaded. Stations folder: {StationLibrary.RootPath}");
		}
	}
}
