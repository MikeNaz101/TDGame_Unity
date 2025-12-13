using UnityEngine;
using System.Collections.Generic;
using System;

namespace SubliminalSarcasm.Core
{
    // A simple mailbox to pass actions from the Background Thread -> Main Thread
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        public void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
        
        // Auto-create the object if it doesn't exist
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (FindObjectOfType<MainThreadDispatcher>() == null)
            {
                GameObject go = new GameObject("MainThreadDispatcher");
                go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }
    }
}