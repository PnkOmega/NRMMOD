using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;

namespace NRRadio
{
	/// <summary>Hooks into the game's music system (GodConstant) so the radio takes over when tuned in.</summary>
	internal static class RadioPatches
	{
		private static bool IsRadioCategory(GodConstant.Music_State s) =>
			s == GodConstant.Music_State.main_menu || s == GodConstant.Music_State.garage ||
			s == GodConstant.Music_State.cruise || s == GodConstant.Music_State.meetspot ||
			s == GodConstant.Music_State.race;

		// The game asks for a new song whenever the scene/mode changes. If the radio is on, answer with
		// our own track and tell the game there is nothing to do.
		[HarmonyPatch(typeof(GodConstant), nameof(GodConstant.music_findNextSong))]
		[HarmonyPrefix]
		private static bool FindNextSong(GodConstant.Music_State __0, ref Il2CppSystem.Collections.IEnumerator __result)
		{
			var radio = RadioRunner.Instance;
			if (radio == null || !radio.RadioOn) return true;

			if (!IsRadioCategory(__0))
			{
				radio.StopRadio();
				return true; // loading screens, race results, etc. keep the game's own jingles
			}

			GodConstant.Instance.music_State = __0;
			if (!radio.IsBusy) radio.PlayNext();

			static System.Collections.IEnumerator Noop() { yield break; }
			__result = Noop().WrapToIl2Cpp();
			return false;
		}

		// Loading a new scene tears down audio; drop our clip so it is not left dangling.
		[HarmonyPatch(typeof(GodConstant), nameof(GodConstant.startLoad))]
		[HarmonyPrefix]
		private static void StartLoad()
		{
			RadioRunner.Instance?.StopRadio();
		}

		// Show our track title in the game's "now playing" UI / phone.
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
