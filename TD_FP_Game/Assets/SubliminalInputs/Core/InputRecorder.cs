using UnityEngine;
using System.Collections.Generic;
using System.IO;
using SubliminalSarcasm.InputSystem;
using SubliminalSarcasm.Tools; 

namespace SubliminalSarcasm.Core
{
    public class InputRecorder : MonoBehaviour
    {
        [Header("Wiring")]
        public InputInterceptor SourceInterceptor; 
        public InputConsumer TargetConsumer;       

        [Header("Settings")]
        public bool AutoPlayOnStart = false; // NEW: Instant gratification
        public bool LoopPlayback = false;    // NEW: Repeat forever
        public bool ShowInputOverlay = true; // NEW: Visual debugger

        [Header("Controls")]
        public KeyCode ToggleRecordKey = KeyCode.R;
        public KeyCode SaveKey = KeyCode.S;
        public KeyCode LoadKey = KeyCode.L;
        public KeyCode PauseKey = KeyCode.P;
        public KeyCode SpeedUpKey = KeyCode.RightBracket; 
        public KeyCode SlowDownKey = KeyCode.LeftBracket;

        [Header("Status")]
        public bool IsRecording = false;
        public bool IsReplaying = false;
        public bool IsPaused = false;
        [Range(0.25f, 4.0f)] public float PlaybackSpeed = 1.0f;
        public int CurrentFrameIndex = 0;

        private ReplayData _currentSession = new ReplayData();
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _frameAccumulator = 0f;
        
        // NEW: For the Overlay
        private NormalizedInputFrame _currentFrameData;

        void Start()
        {
            _currentSession.RecordedFrames = new List<NormalizedInputFrame>();
            _startPosition = TargetConsumer.transform.position;
            _startRotation = TargetConsumer.transform.rotation;

            // NEW: Auto-load if requested
            if (AutoPlayOnStart)
            {
                LoadAndRestart();
            }
        }

        void FixedUpdate()
        {
            if (IsRecording)
            {
                NormalizedInputFrame frame = SourceInterceptor.CurrentInput;
                _currentSession.RecordedFrames.Add(frame);
                _currentFrameData = frame; // Update for UI
            }

            if (IsReplaying && !IsPaused)
            {
                HandlePlayback();
            }
        }

        private void HandlePlayback()
        {
            _frameAccumulator += PlaybackSpeed;

            while (_frameAccumulator >= 1.0f)
            {
                if (CurrentFrameIndex < _currentSession.RecordedFrames.Count)
                {
                    NormalizedInputFrame frame = _currentSession.RecordedFrames[CurrentFrameIndex];
                    TargetConsumer.InjectRemoteFrame(frame);
                    _currentFrameData = frame; // Update for UI
                    CurrentFrameIndex++;
                }
                else
                {
                    // NEW: Loop Logic
                    if (LoopPlayback)
                    {
                        LoadAndRestart(); 
                        break; 
                    }
                    else
                    {
                        Debug.Log("End of Tape.");
                        IsReplaying = false;
                        IsPaused = false;
                        _frameAccumulator = 0;
                        break;
                    }
                }
                _frameAccumulator -= 1.0f;
            }
        }

        void Update()
        {
            // (Controls are the same as before...)
            if (Input.GetKeyDown(ToggleRecordKey)) ToggleRecording();
            if (Input.GetKeyDown(SaveKey) && !IsRecording) SaveRecording();
            if (Input.GetKeyDown(LoadKey)) LoadAndRestart();
            if (Input.GetKeyDown(PauseKey) && IsReplaying) IsPaused = !IsPaused;
            if (Input.GetKeyDown(SpeedUpKey)) PlaybackSpeed *= 2f;
            if (Input.GetKeyDown(SlowDownKey)) PlaybackSpeed /= 2f;
        }

        private void ToggleRecording()
        {
            IsRecording = !IsRecording;
            if (IsRecording)
            {
                IsReplaying = false;
                _currentSession.RecordedFrames.Clear();
                _startPosition = TargetConsumer.transform.position;
                _startRotation = TargetConsumer.transform.rotation;
            }
        }

        private void LoadAndRestart()
        {
            if (_currentSession.RecordedFrames.Count == 0)
            {
                string path = Path.Combine(Application.dataPath, "ReplaySession.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _currentSession = JsonUtility.FromJson<ReplayData>(json);
                }
                else return;
            }

            CurrentFrameIndex = 0;
            IsReplaying = true;
            IsPaused = false;
            PlaybackSpeed = 1.0f;
            _frameAccumulator = 0f;

            CharacterController cc = TargetConsumer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            TargetConsumer.transform.position = _startPosition;
            TargetConsumer.transform.rotation = _startRotation;
            if (cc != null) cc.enabled = true;
        }

        private void SaveRecording()
        {
            _currentSession.SessionName = "Dev_Replay";
            _currentSession.Timestamp = System.DateTime.Now.ToString();
            string json = JsonUtility.ToJson(_currentSession, true);
            string path = Path.Combine(Application.dataPath, "ReplaySession.json");
            File.WriteAllText(path, json);
            Debug.Log($"💾 SAVED: {path}");
        }

        // --- NEW: THE VISUAL DEBUGGER ---
        void OnGUI()
        {
            GUIStyle textStyle = new GUIStyle();
            textStyle.fontSize = 20;
            textStyle.normal.textColor = Color.white;
            textStyle.fontStyle = FontStyle.Bold;

            // 1. Status Label
            if (IsRecording) 
            {
                textStyle.normal.textColor = Color.red;
                GUI.Label(new Rect(20, 20, 300, 50), "🔴 REC", textStyle);
            }
            else if (IsReplaying)
            {
                string icon = IsPaused ? "⏸" : "▶";
                string loop = LoopPlayback ? " (Loop)" : "";
                string speed = PlaybackSpeed != 1.0f ? $" ({PlaybackSpeed}x)" : "";
                textStyle.normal.textColor = Color.green;
                GUI.Label(new Rect(20, 20, 500, 50), $"{icon} REPLAY {CurrentFrameIndex}/{_currentSession.RecordedFrames.Count}{speed}{loop}", textStyle);
            }

            // 2. Input Overlay (Only show if active)
            if (ShowInputOverlay && (IsRecording || IsReplaying))
            {
                DrawInputOverlay(new Rect(20, 60, 200, 100));
            }
        }

        private void DrawInputOverlay(Rect area)
        {
            // Simple box background
            GUI.Box(area, "Inputs");

            // Check bits
            bool jump = (_currentFrameData.ButtonMask & (ushort)InputButtons.Jump) != 0;
            bool fire = (_currentFrameData.ButtonMask & (ushort)InputButtons.Fire) != 0;
            
            // Draw Indicators
            GUIStyle onStyle = new GUIStyle(GUI.skin.label);
            onStyle.normal.textColor = Color.green;
            
            GUIStyle offStyle = new GUIStyle(GUI.skin.label);
            offStyle.normal.textColor = Color.gray;

            GUI.Label(new Rect(area.x + 10, area.y + 20, 80, 20), "JUMP", jump ? onStyle : offStyle);
            GUI.Label(new Rect(area.x + 10, area.y + 40, 80, 20), "FIRE", fire ? onStyle : offStyle);

            // Draw Axes
            float x = InputQuantizer.Dequantize(_currentFrameData.MoveX);
            float y = InputQuantizer.Dequantize(_currentFrameData.MoveY);
            GUI.Label(new Rect(area.x + 10, area.y + 70, 150, 20), $"Stick: [{x:F2}, {y:F2}]");
        }
    }
}