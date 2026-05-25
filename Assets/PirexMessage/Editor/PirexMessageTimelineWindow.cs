using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PirexMessage.Editor
{
    public class PirexMessageTimelineWindow : EditorWindow
    {
        [MenuItem("Window/PirexGames/PirexMessage Timeline")]
        public static void ShowWindow()
        {
            GetWindow<PirexMessageTimelineWindow>("Message Timeline");
        }
        
        private Vector2 _scrollPos;
        private Vector2 _detailScrollPos;
        private float _zoom = 100f;
        private RecordedEvent _selectedEvent;
        private bool _autoScroll = true;
        
        private List<Type> _rowTypes = new List<Type>();
        private Dictionary<Type, int> _rowMap = new Dictionary<Type, int>();
        
        private void OnEnable()
        {
            PirexMessageProfiler.OnEventRecorded += Repaint;
        }

        private void OnDisable()
        {
            PirexMessageProfiler.OnEventRecorded -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();
            
            EditorGUILayout.BeginHorizontal();
            DrawTimeline();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            PirexMessageProfiler.IsRecording = GUILayout.Toggle(PirexMessageProfiler.IsRecording, "Record", EditorStyles.toolbarButton, GUILayout.Width(60));
            
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                PirexMessageProfiler.Clear();
                _selectedEvent = null;
                _rowTypes.Clear();
                _rowMap.Clear();
            }
            
            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto Scroll", EditorStyles.toolbarButton, GUILayout.Width(80));
            
            GUILayout.FlexibleSpace();
            
            GUILayout.Label("Zoom:");
            _zoom = GUILayout.HorizontalSlider(_zoom, 10f, 1000f, GUILayout.Width(100));
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimeline()
        {
            var events = PirexMessageProfiler.Events;
            UpdateRows(events);
            
            float rowHeight = 35f;
            float totalHeight = Mathf.Max(position.height, _rowTypes.Count * rowHeight + 80f);
            
            float minT = events.Count > 0 ? events[0].Time : 0f;
            float maxT = events.Count > 0 ? events[events.Count - 1].Time : 10f;
            float duration = Mathf.Max(10f, maxT - minT + 2f);
            float totalWidth = Mathf.Max(position.width * 0.7f, duration * _zoom + 250f);
            
            if (events.Count > 0 && _autoScroll && Event.current.type == EventType.Repaint)
            {
                _scrollPos.x = Mathf.Max(0, (maxT - minT) * _zoom - position.width * 0.7f + 100);
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Width(position.width * 0.7f));
            Rect canvasRect = GUILayoutUtility.GetRect(totalWidth, totalHeight);
            
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Box(canvasRect, GUIContent.none, EditorStyles.helpBox);
                
                for (int i = 0; i < _rowTypes.Count; i++)
                {
                    Rect rowRect = new Rect(canvasRect.x, canvasRect.y + i * rowHeight + 30, totalWidth, rowHeight);
                    if (i % 2 == 0)
                        EditorGUI.DrawRect(rowRect, new Color(0.1f, 0.1f, 0.1f, 0.05f));
                        
                    EditorGUI.DrawRect(new Rect(canvasRect.x, rowRect.y + rowHeight, totalWidth, 1), new Color(0.5f, 0.5f, 0.5f, 0.2f));
                }
            }
            
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (!_rowMap.TryGetValue(evt.EventType, out int rowIndex)) continue;
                
                float x = canvasRect.x + 200 + (evt.Time - minT) * _zoom;
                float y = canvasRect.y + rowIndex * rowHeight + 30;
                
                Rect dotRect = new Rect(x - 4, y + 13, 8, 8);
                
                if (Event.current.type == EventType.Repaint)
                {
                    Color dotColor = new Color(0.3f, 0.8f, 1f);
                    if (evt == _selectedEvent) dotColor = Color.yellow;
                    else if (evt.ExecutionTimeMs > 2.0) dotColor = Color.red;
                    else if (evt.ExecutionTimeMs > 0.5) dotColor = new Color(1f, 0.5f, 0f);
                    
                    EditorGUI.DrawRect(dotRect, dotColor);
                }
                else if (Event.current.type == EventType.MouseDown && dotRect.Contains(Event.current.mousePosition))
                {
                    _selectedEvent = evt;
                    _autoScroll = false;
                    Event.current.Use();
                    Repaint();
                }
            }
            
            if (Event.current.type == EventType.Repaint)
            {
                Color bgColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.76f, 0.76f, 0.76f, 1f);
                for (int i = 0; i < _rowTypes.Count; i++)
                {
                    Rect rowRect = new Rect(canvasRect.x, canvasRect.y + i * rowHeight + 30, totalWidth, rowHeight);
                    EditorGUI.DrawRect(new Rect(canvasRect.x + _scrollPos.x, rowRect.y, 200, rowHeight), bgColor);
                    GUI.Label(new Rect(canvasRect.x + 5 + _scrollPos.x, rowRect.y + 8, 190, 20), _rowTypes[i].Name, EditorStyles.boldLabel);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.3f - 5));
            
            GUILayout.Label("Event Details", EditorStyles.boldLabel);
            
            if (_selectedEvent == null)
            {
                GUILayout.Label("Select an event on the timeline.", EditorStyles.wordWrappedLabel);
            }
            else
            {
                _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
                
                GUILayout.Label($"Type: {_selectedEvent.EventType.Name}", EditorStyles.boldLabel);
                GUILayout.Label($"Time: {_selectedEvent.Time:F2}s");
                GUILayout.Label($"Frame: {_selectedEvent.Frame}");
                
                GUIStyle richTextLabel = new GUIStyle(EditorStyles.label) { richText = true };
                string color = _selectedEvent.ExecutionTimeMs > 2.0 ? "red" : (_selectedEvent.ExecutionTimeMs > 0.5 ? "orange" : "white");
                GUILayout.Label($"<color={color}>Execution Time: {_selectedEvent.ExecutionTimeMs:F3} ms</color>", richTextLabel);
                
                EditorGUILayout.Space();
                GUILayout.Label("Publisher:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_selectedEvent.Publisher ?? "Unknown", EditorStyles.wordWrappedLabel, GUILayout.Height(35));
                
                GUILayout.Label("Subscribers:", EditorStyles.boldLabel);
                if (_selectedEvent.Subscribers != null && _selectedEvent.Subscribers.Length > 0)
                {
                    foreach (var sub in _selectedEvent.Subscribers)
                    {
                        GUILayout.Label($"- {sub}");
                    }
                }
                else
                {
                    GUILayout.Label("No subscribers.");
                }
                
                EditorGUILayout.Space();
                GUILayout.Label("Payload (JSON):", EditorStyles.boldLabel);
                
                EditorGUILayout.TextArea(_selectedEvent.PayloadJson, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void UpdateRows(List<RecordedEvent> events)
        {
            foreach (var e in events)
            {
                if (!_rowMap.ContainsKey(e.EventType))
                {
                    _rowTypes.Add(e.EventType);
                    _rowMap[e.EventType] = _rowTypes.Count - 1;
                }
            }
        }
    }
}
