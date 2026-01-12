using System.Collections.Generic;
using SubliminalSarcasm.InputSystem;

// CHANGED: From .System to .Tools to avoid conflict with Microsoft System
namespace SubliminalSarcasm.Tools 
{
    [System.Serializable]
    public class ReplayData
    {
        public string SessionName;
        public string Timestamp;
        public List<NormalizedInputFrame> RecordedFrames;
    }
}