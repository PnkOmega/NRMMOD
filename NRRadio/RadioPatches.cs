using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

namespace NRRadio
{
	/// <summary>Hooks into the game's music system (GodConstant) so custom songs mix into its playlists.</summary>
	internal static class RadioPatches
	{
		// The game asks for a song whenever a scene starts, a song ends, or the player skips on the phone.
		// Sometimes answer with one of ours; otherwise let the game pick one of its own as normal.
		[HarmonyPatch(typeof(GodConstant), nameof(GodConstant.music_findNextSong))]
		[HarmonyPrefix]
		private static bool FindNextSong(GodConstant.Music_State __0, ref Il2CppSystem.Collections.IEnumerator __result)
		{
			var radio = RadioRunner.Instance;
			if (radio == null || !radio.OnGameWantsSong(__0)) return true;

			GodConstant.Instance.music_State = __0;
			static System.Collections.IEnumerator Noop() { yield break; }
			__result = Noop().WrapToIl2Cpp();
			return false;
		}

		[HarmonyPatch(typeof(GodConstant), nameof(GodConstant.startLoad))]
		[HarmonyPrefix]
		private static void StartLoad() => RadioRunner.Instance?.OnSceneLoading();

		// Show our song title in the phone / now-playing display.
		[HarmonyPatch(typeof(RCC_Settings), nameof(RCC_Settings.getSongName))]
		[HarmonyPrefix]
		private static bool GetSongName(RCC_Settings.SongTrack_ID __0, ref string __result)
		{
			var name = RadioRunner.Instance?.CurrentTrackName;
			if (__0 == RCC_Settings.SongTrack_ID.null_song && !string.IsNullOrEmpty(name))
			{
				__result = name;
				return false;
			}
			return true;
		}
	}
}
