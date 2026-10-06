using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NRRadio
{
	/// <summary>
	/// Lives on its own GameObject. Reads hotkeys, loads tracks and feeds them to the game's music
	/// AudioSource so the game's own effects (menus, pause, etc.) still apply to the music.
	/// </summary>
	public class RadioRunner : MonoBehaviour
	{
		public RadioRunner(IntPtr ptr) : base(ptr) { }

		internal static RadioRunner Instance;

		private enum State { Idle, Loading, Playing }

		private List<Station> _stations;
		private int _stationIndex = -1;       // -1 = radio off, game plays its own music
		private State _state = State.Idle;
		private UnityWebRequest _request;
		private string _loadingPath;
		private AudioClip _clip;
		private string _toast;
		private float _toastUntil;

		internal bool RadioOn => _stationIndex >= 0;
		internal string CurrentTrackName { get; private set; }

		private void Start()
		{
			_stations = StationLibrary.Load();
			Plugin.Log.LogInfo($"Found {_stations.Count} station(s).");
		}

		private void Update()
		{
			if (Input.GetKeyDown(Plugin.NextStationKey.Value)) Tune(+1);
			else if (Input.GetKeyDown(Plugin.PrevStationKey.Value)) Tune(-1);
			else if (Input.GetKeyDown(Plugin.NextTrackKey.Value) && RadioOn) PlayNext();

			if (_state == State.Loading && _request != null && _request.isDone)
			{
				FinishLoad();
			}
			else if (_state == State.Playing)
			{
				var src = GodConstant.Instance?.musicSource;
				// A paused source keeps its time; a finished one is stopped and rewound to 0.
				if (src != null && !src.isPlaying && src.time <= 0f)
				{
					PlayNext();
				}
			}
		}

		private void OnGUI()
		{
			if (!Plugin.ShowToasts.Value || Time.unscaledTime > _toastUntil || string.IsNullOrEmpty(_toast)) return;
			var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.LowerLeft };
			var rect = new Rect(24f, Screen.height - 84f, Screen.width - 48f, 60f);
			style.normal.textColor = Color.black;
			GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), _toast, style);
			style.normal.textColor = Color.white;
			GUI.Label(rect, _toast, style);
		}

		internal void Tune(int direction)
		{
			if (_stations == null || _stations.Count == 0)
			{
				Toast($"No stations found. Put music in {StationLibrary.RootPath}");
				return;
			}

			// Cycle: off -> station 0 -> ... -> station N-1 -> off
			var slots = _stations.Count + 1;
			var slot = (_stationIndex + 1 + direction + slots) % slots;
			_stationIndex = slot - 1;

			if (_stationIndex < 0)
			{
				StopRadio();
				Toast("Radio: off (game music)");
				var god = GodConstant.Instance;
				if (god != null) god.music_refreshStateSettings();
				return;
			}

			Toast($"Radio: {_stations[_stationIndex].Name}");
			PlayNext();
		}

		internal void PlayNext()
		{
			if (!RadioOn) return;
			var path = _stations[_stationIndex].NextTrack();
			if (path == null)
			{
				Toast($"{_stations[_stationIndex].Name} has no tracks");
				return;
			}

			ReleaseClip();
			_loadingPath = path;
			_state = State.Loading;

			var uri = new Uri(path).AbsoluteUri;
			_request = UnityWebRequestMultimedia.GetAudioClip(uri, StationLibrary.TypeOf(path));
			_request.SendWebRequest();
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
				// Skip to another track, but never loop forever on a station with only broken files.
				if (_stations[_stationIndex].Tracks().Count > 1) PlayNext();
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

			god.musicSource.Stop();
			god.musicSource.clip = _clip;
			god.musicSource.Play();
			_state = State.Playing;

			god.nowPlaying = new RCC_Settings.SoundtrackSong { song_ID = RCC_Settings.SongTrack_ID.null_song };
			god.UI_Data?.start_fade_nowPlaying(false);
			Toast($"{_stations[_stationIndex].Name}  -  {title}");
		}

		/// <summary>Stop our playback (scene change, radio switched off).</summary>
		internal void StopRadio()
		{
			if (_request != null)
			{
				_request.Dispose();
				_request = null;
			}
			_state = State.Idle;
			var src = GodConstant.Instance?.musicSource;
			if (src != null && _clip != null && src.clip == _clip) src.Stop();
			ReleaseClip();
			CurrentTrackName = null;
		}

		private void ReleaseClip()
		{
			if (_clip != null)
			{
				var src = GodConstant.Instance?.musicSource;
				if (src != null && src.clip == _clip) src.clip = null;
				Destroy(_clip);
				_clip = null;
			}
		}

		internal bool IsBusy => _state != State.Idle;

		private void Toast(string text)
		{
			_toast = text;
			_toastUntil = Time.unscaledTime + 4f;
		}
	}
}
