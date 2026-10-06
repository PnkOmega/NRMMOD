using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NRRadio
{
	/// <summary>
	/// Plays custom songs through the game's own music AudioSource (GodConstant.musicSource) and resets the same
	/// music parameters the game does for its songs, so the game's normal music behaviour (ducking while driving
	/// fast, reverb zones, menu/pause handling) keeps applying to your songs.
	/// </summary>
	public class RadioRunner : MonoBehaviour
	{
		public RadioRunner(IntPtr ptr) : base(ptr) { }

		internal static RadioRunner Instance;

		private enum State { Idle, Loading, Playing }

		private readonly Dictionary<GodConstant.Music_State, Playlist> _playlists = new();
		private State _state = State.Idle;
		private GodConstant.Music_State _scene = GodConstant.Music_State.off;
		private UnityWebRequest _request;
		private string _loadingPath;
		private AudioClip _clip;
		private bool _sawPlaying;
		private bool _advancing;
		private string _toast;
		private float _toastUntil;

		internal string CurrentTrackName { get; private set; }
		internal bool IsCustomActive => _state != State.Idle;

		private void Start() => SongLibrary.EnsureFolders();

		private void Update()
		{
			if (Input.GetKeyDown(Plugin.MixToggleKey.Value))
			{
				Plugin.MixEnabled.Value = !Plugin.MixEnabled.Value;
				Toast("Custom songs in playlist: " + (Plugin.MixEnabled.Value ? "ON" : "OFF"));
			}
			if (Input.GetKeyDown(Plugin.NextKey.Value)) PlayNow(false);
			if (Input.GetKeyDown(Plugin.PrevKey.Value)) PlayNow(true);
			if (Input.GetKeyDown(Plugin.FreeMemoryKey.Value))
			{
				Resources.UnloadUnusedAssets();
				Toast("Unloaded unused audio");
			}

			if (_state == State.Loading && _request != null && _request.isDone)
			{
				FinishLoad();
			}
			else if (_state == State.Playing)
			{
				CheckForEnd();
			}
		}

		private void OnGUI()
		{
			if (!Plugin.ShowToasts.Value || Time.unscaledTime > _toastUntil || string.IsNullOrEmpty(_toast)) return;
			var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperRight };
			var rect = new Rect(24f, 16f, Screen.width - 48f, 40f);
			style.normal.textColor = Color.black;
			GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), _toast, style);
			style.normal.textColor = new Color(0.4f, 1f, 0.4f);
			GUI.Label(rect, _toast, style);
		}

		/// <summary>
		/// Called by the game-hook whenever the game is about to choose its next song for a scene.
		/// Returns true if one of our songs took over (the game's own pick is then skipped).
		/// </summary>
		internal bool OnGameWantsSong(GodConstant.Music_State scene)
		{
			_scene = scene;
			if (!SongLibrary.IsSceneSupported(scene) || !Plugin.MixEnabled.Value)
			{
				ReleaseCustom();
				return false;
			}

			var roll = UnityEngine.Random.Range(0, 100);
			if (roll < Plugin.CustomChancePercent.Value && Play(PlaylistFor(scene).Next(scene)))
			{
				return true;
			}

			ReleaseCustom();
			return false;
		}

		/// <summary>Scene change / loading: drop our clip so the game's audio setup is left clean.</summary>
		internal void OnSceneLoading() => ReleaseCustom();

		private void PlayNow(bool previous)
		{
			var god = GodConstant.Instance;
			if (god == null) return;
			if (!SongLibrary.IsSceneSupported(god.music_State))
			{
				Toast("No custom songs for this scene");
				return;
			}

			_scene = god.music_State;
			var list = PlaylistFor(_scene);
			var path = previous ? list.Previous() : list.Next(_scene);
			if (path == null)
			{
				Toast($"No songs in Music/{_scene}");
				return;
			}
			Play(path);
		}

		private bool Play(string path)
		{
			if (path == null) return false;

			ReleaseClip();
			_loadingPath = path;
			_state = State.Loading;
			_sawPlaying = false;

			_request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, SongLibrary.TypeOf(path));
			_request.SendWebRequest();
			return true;
		}

		private void FinishLoad()
		{
			var req = _request;
			_request = null;
			var path = _loadingPath;

			if (req.result != UnityWebRequest.Result.Success)
			{
				Plugin.Log.LogWarning($"Could not load {path}: {req.error}");
				req.Dispose();
				_state = State.Idle;
				HandOverToGame();
				return;
			}

			_clip = DownloadHandlerAudioClip.GetContent(req);
			req.Dispose();

			var god = GodConstant.Instance;
			if (_clip == null || god == null || god.musicSource == null)
			{
				_state = State.Idle;
				return;
			}

			var title = Path.GetFileNameWithoutExtension(path);
			_clip.name = title;
			CurrentTrackName = title;

			// Same reset the game applies when one of its own songs starts, so its dynamic music handling
			// (volume target driven by speed etc.) takes over from a known state.
			god.musicSource.Stop();
			god.musicSource.clip = _clip;
			god.musicSource.reverbZoneMix = 1f;
			god.musicSource.pitch = 1f;
			god.musicVol_Target = 0f;
			god.musicSource.Play();
			_state = State.Playing;

			god.nowPlaying = new RCC_Settings.SoundtrackSong { song_ID = RCC_Settings.SongTrack_ID.null_song };
			var ui = god.UI_Data;
			if (ui != null)
			{
				ui.start_fade_nowPlaying(false);
				if (ui.ui_nowPlayingText != null) ui.ui_nowPlayingText.text = title;
			}
		}

		private void CheckForEnd()
		{
			var god = GodConstant.Instance;
			var src = god?.musicSource;
			if (src == null) return;

			if (src.isPlaying) { _sawPlaying = true; return; }

			// Finished = the source stopped and rewound after actually playing. A paused source keeps its time,
			// and loading screens / music-off are the game's business, not a track ending.
			if (!_sawPlaying || src.time > 0f) return;
			if (god.loading_StayInLoadingScreen || !SongLibrary.IsSceneSupported(god.music_State)) return;

			_scene = god.music_State;
			_state = State.Idle;
			HandOverToGame();
		}

		/// <summary>Our song is over: let the game choose again (it may pick its own song or call us back).</summary>
		private void HandOverToGame()
		{
			var god = GodConstant.Instance;
			if (god == null || _advancing) return;
			_advancing = true;
			try
			{
				god.StartCoroutine(god.music_findNextSong(_scene, false));
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Could not ask the game for its next song, repeating custom playlist: " + e.Message);
				Play(PlaylistFor(_scene).Next(_scene));
			}
			finally
			{
				_advancing = false;
			}
		}

		/// <summary>Stop managing the music source (the game is about to use it itself).</summary>
		internal void ReleaseCustom()
		{
			if (_request != null)
			{
				_request.Dispose();
				_request = null;
			}
			_state = State.Idle;
			CurrentTrackName = null;
			ReleaseClip();
		}

		private void ReleaseClip()
		{
			if (_clip == null) return;
			var src = GodConstant.Instance?.musicSource;
			if (src != null && src.clip == _clip)
			{
				src.Stop();
				src.clip = null;
			}
			Destroy(_clip);
			_clip = null;
		}

		private Playlist PlaylistFor(GodConstant.Music_State scene)
		{
			if (!_playlists.TryGetValue(scene, out var p)) _playlists[scene] = p = new Playlist();
			return p;
		}

		private void Toast(string text)
		{
			_toast = text;
			_toastUntil = Time.unscaledTime + 3f;
		}
	}
}
