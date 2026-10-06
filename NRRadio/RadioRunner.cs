using System;
using System.Collections.Generic;
using System.IO;
using PlanetJem.Audio;
using UnityEngine;
using UnityEngine.Networking;

namespace NRRadio
{
	/// <summary>
	/// Adds custom songs to the game's own playlist. When the game starts one of its tracks, there is a chance
	/// we swap the clip on the same AudioSource for one of the player's songs. Because it stays on the game's
	/// music source/mixer group, the game's own music handling (speed ducking, filters, fades) still applies.
	/// The game's player also notices when the source finishes and moves on to its next track by itself.
	/// </summary>
	public class RadioRunner : MonoBehaviour
	{
		public RadioRunner(IntPtr ptr) : base(ptr) { }

		internal static RadioRunner Instance;

		private AudioManager _am;
		private float _nextFind;
		private MusicPlayerState _state = MusicPlayerState.Off;
		private readonly Dictionary<MusicPlayerState, Playlist> _playlists = new();

		// Detecting that the game started a track, and which AudioSource it put it on.
		private IntPtr _lastPtr, _ownPtr, _pa, _pb;
		private AudioSource _lastChanged;
		private float _lastChangeTime;
		private bool _wantSwap;
		private float _wantDeadline;
		private AudioSource _swapTarget;
		private int _swapFrame;

		// Song loading: one request at a time, one song kept ready.
		private UnityWebRequest _req;
		private string _reqPath;
		private MusicPlayerState _reqState;
		private bool _reqPlayNow, _reqRecord;
		private AudioClip _readyClip;
		private string _readyPath;
		private MusicPlayerState _readyState;
		private bool _readyRecord;

		private AudioClip _custom;
		private readonly List<(AudioClip clip, float time)> _old = new();
		private readonly List<string> _played = new();
		private int _cursor = -1;

		private string _toast;
		private float _toastUntil;

		private void Start() => SongLibrary.EnsureFolders();

		private MusicPlayer Player => _am != null ? _am.Player : null;

		private void Update()
		{
			HandleKeys();

			if (_am == null && Time.unscaledTime >= _nextFind)
			{
				_nextFind = Time.unscaledTime + 1f;
				_am = FindObjectOfType<AudioManager>();
			}

			var mp = Player;
			if (mp == null) return;

			PollRequest(mp);
			TrackClips(mp);

			var st = mp.State;
			if (st != _state)
			{
				_state = st;
				if (_readyClip != null && _readyState != st) DestroyReady();
			}

			var np = mp.NowPlaying;
			var ptr = np is null ? IntPtr.Zero : np.Pointer;
			if (ptr != _lastPtr)
			{
				_lastPtr = ptr;
				if (ptr != _ownPtr) OnGameTrackStarted();
			}

			if (_wantSwap && Time.unscaledTime > _wantDeadline)
			{
				// No clip change seen after the track started: assume it was assigned just before.
				_wantSwap = false;
				_swapTarget = _lastChanged != null ? _lastChanged : mp.currentSource_;
				_swapFrame = Time.frameCount + 1;
			}
			if (_swapFrame > 0 && Time.frameCount >= _swapFrame)
			{
				_swapFrame = 0;
				ApplyReady(mp, _swapTarget);
			}

			EnsurePreload();
			ReapOld(mp);
		}

		private void HandleKeys()
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
		}

		private static IntPtr ClipPtr(AudioSource s) => s == null || s.clip == null ? IntPtr.Zero : s.clip.Pointer;

		/// <summary>Watch both of the game's music sources so we can tell which one got the new track.</summary>
		private void TrackClips(MusicPlayer mp)
		{
			var a = mp.sourceA_;
			var b = mp.sourceB_;
			var now = Time.unscaledTime;
			var pa = ClipPtr(a);
			var pb = ClipPtr(b);
			if (pa != _pa) { _pa = pa; OnClipChanged(a, now); }
			if (pb != _pb) { _pb = pb; OnClipChanged(b, now); }
		}

		private void OnClipChanged(AudioSource src, float now)
		{
			_lastChanged = src;
			_lastChangeTime = now;
			if (_wantSwap)
			{
				_wantSwap = false;
				_swapTarget = src;
				_swapFrame = Time.frameCount + 1;
			}
		}

		private void OnGameTrackStarted()
		{
			if (!Plugin.MixEnabled.Value || !SongLibrary.IsSupported(_state)) return;
			if (_readyClip == null || _readyState != _state) return;
			var chance = _state == MusicPlayerState.Garage ? Plugin.GarageChancePercent.Value : Plugin.CustomChancePercent.Value;
			if (UnityEngine.Random.Range(0, 100) >= chance) return;

			if (_lastChanged != null && Time.unscaledTime - _lastChangeTime < 0.3f)
			{
				_swapTarget = _lastChanged;
				_swapFrame = Time.frameCount + 1;
			}
			else
			{
				_wantSwap = true;
				_wantDeadline = Time.unscaledTime + 0.75f;
			}
		}

		private void PlayNow(bool previous)
		{
			var mp = Player;
			if (mp == null) return;
			if (!SongLibrary.IsSupported(mp.State))
			{
				Toast("No custom songs for this scene");
				return;
			}

			string path;
			var record = true;
			if (previous)
			{
				if (_cursor <= 0) { Toast("No previous custom song"); return; }
				path = _played[--_cursor];
				record = false;
			}
			else
			{
				path = PlaylistFor(mp.State).Next(mp.State);
			}

			if (path == null)
			{
				Toast($"No songs in Music/{SongLibrary.NameOf(mp.State)}");
				return;
			}
			StartLoad(path, mp.State, playNow: true, record);
		}

		private void StartLoad(string path, MusicPlayerState st, bool playNow, bool record)
		{
			DisposeRequest();
			_reqPath = path;
			_reqState = st;
			_reqPlayNow = playNow;
			_reqRecord = record;
			_req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, SongLibrary.TypeOf(path));
			_req.SendWebRequest();
		}

		private void PollRequest(MusicPlayer mp)
		{
			if (_req == null || !_req.isDone) return;
			var req = _req;
			_req = null;

			if (req.result != UnityWebRequest.Result.Success)
			{
				Plugin.Log.LogWarning($"Could not load {_reqPath}: {req.error}");
				req.Dispose();
				return;
			}

			var clip = DownloadHandlerAudioClip.GetContent(req);
			req.Dispose();
			if (clip == null) return;
			clip.name = Path.GetFileNameWithoutExtension(_reqPath);

			DestroyReady();
			_readyClip = clip;
			_readyPath = _reqPath;
			_readyState = _reqState;
			_readyRecord = _reqRecord;

			if (_reqPlayNow) ApplyReady(mp, mp.currentSource_);
		}

		private void EnsurePreload()
		{
			if (_req != null || _readyClip != null) return;
			if (!Plugin.MixEnabled.Value || !SongLibrary.IsSupported(_state)) return;
			var path = PlaylistFor(_state).Next(_state);
			if (path != null) StartLoad(path, _state, playNow: false, record: true);
		}

		/// <summary>Put the ready song on the given music source and show its name.</summary>
		private void ApplyReady(MusicPlayer mp, AudioSource src)
		{
			if (_readyClip == null) return;
			if (src == null) src = mp.currentSource_;
			if (src == null) return;

			if (_custom != null) _old.Add((_custom, Time.unscaledTime));
			src.clip = _readyClip;
			src.time = 0f;
			src.Play();
			_custom = _readyClip;
			_readyClip = null;

			// Don't let the clip swap be mistaken for the game starting another track.
			_pa = ClipPtr(mp.sourceA_);
			_pb = ClipPtr(mp.sourceB_);

			if (_readyRecord)
			{
				_played.Add(_readyPath);
				if (_played.Count > 50) _played.RemoveAt(0);
				_cursor = _played.Count - 1;
			}
			ShowNowPlaying(mp, _custom.name);
			Toast(_custom.name);
		}

		private void ShowNowPlaying(MusicPlayer mp, string title)
		{
			try
			{
				var entry = new MusicTrackEntry();
				entry.TrackName = title;
				mp._NowPlaying_k__BackingField = entry;
				_ownPtr = entry.Pointer;
				_lastPtr = entry.Pointer;
				mp.RefreshNowPlayingOverlay(true);
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Could not update the now-playing display: " + e.Message);
			}
		}

		/// <summary>Destroy clips we replaced once no source uses them and the game's fade has finished.</summary>
		private void ReapOld(MusicPlayer mp)
		{
			for (var i = _old.Count - 1; i >= 0; i--)
			{
				var (clip, time) = _old[i];
				if (Time.unscaledTime - time < 10f) continue;
				if (clip != null && (mp.sourceA_.clip == clip || mp.sourceB_.clip == clip)) continue;
				if (clip != null) Destroy(clip);
				_old.RemoveAt(i);
			}
		}

		private Playlist PlaylistFor(MusicPlayerState st)
		{
			if (!_playlists.TryGetValue(st, out var p)) _playlists[st] = p = new Playlist();
			return p;
		}

		private void DestroyReady()
		{
			if (_readyClip != null) Destroy(_readyClip);
			_readyClip = null;
		}

		private void DisposeRequest()
		{
			if (_req == null) return;
			_req.Dispose();
			_req = null;
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

		private void Toast(string text)
		{
			_toast = text;
			_toastUntil = Time.unscaledTime + 3f;
		}
	}
}
