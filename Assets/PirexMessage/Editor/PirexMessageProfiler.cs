using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PirexMessage;

namespace PirexMessage.Editor
{
    public class RecordedEvent
    {
        public float Time;
        public Type EventType;
        public string PayloadJson;
        public int Frame;
    }

    public static class PirexMessageProfiler
    {
        public static bool IsRecording { get; set; } = false;
        
        public static readonly List<RecordedEvent> Events = new List<RecordedEvent>();
        public static event Action OnEventRecorded;
        
        [InitializeOnLoadMethod]
        private static void Init()
        {
            PirexPipe.OnEditorMessagePublished += HandleMessagePublished;
            EditorApplication.playModeStateChanged += state => 
            {
                if (state == PlayModeStateChange.ExitingEditMode) 
                    Clear();
            };
        }

        private static void HandleMessagePublished(Type type, object payload)
        {
            if (!IsRecording || !Application.isPlaying) return;
            
            string json = string.Empty;
            try 
            { 
                json = JsonUtility.ToJson(payload, true); 
                if (string.IsNullOrEmpty(json) || json == "{}")
                    json = payload?.ToString();
            } 
            catch 
            { 
                json = payload?.ToString(); 
            }
            
            Events.Add(new RecordedEvent
            {
                Time = Time.time,
                EventType = type,
                PayloadJson = json,
                Frame = Time.frameCount
            });
            
            // Limit memory
            if (Events.Count > 10000) Events.RemoveAt(0);
            
            OnEventRecorded?.Invoke();
        }
        
        public static void Clear()
        {
            Events.Clear();
            OnEventRecorded?.Invoke();
        }
    }
}
