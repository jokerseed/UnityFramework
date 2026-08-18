using System;
using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.GAS.Events;
using UnityEngine;

namespace Game
{
    /// <summary>按 Luban Cue 表把 <see cref="GameplayCueEvent"/> 映射为简易球体表现。</summary>
    public sealed class BattleCuePresenter
    {
        const float FollowHeight = 1.6f;

        readonly Dictionary<string, ActiveCue> _activeCues = new Dictionary<string, ActiveCue>(16);
        readonly List<TimedCue> _bursts = new List<TimedCue>(16);
        CfgTbCue _table;

        /// <summary>绑定 Cue 表。</summary>
        /// <param name="table">Luban Cue 表；可为 null（不播放）。</param>
        public void Bind(CfgTbCue table) => _table = table;

        /// <summary>处理一条 Cue 事件。</summary>
        /// <param name="evt">Cue 事件。</param>
        public void Handle(in GameplayCueEvent evt)
        {
            if (_table == null || string.IsNullOrEmpty(evt.CueTag) || !_table.DataMap.TryGetValue(evt.CueTag, out var def))
            {
                return;
            }

            switch (evt.Action)
            {
                case GameplayCueAction.Add:
                    ReplaceActive(MakeKey(evt), evt.Actor, Spawn(def, FollowPosition(evt.Position)));
                    break;
                case GameplayCueAction.Remove:
                    DestroyActive(MakeKey(evt));
                    break;
                default:
                    _bursts.Add(new TimedCue(Spawn(def, evt.Position), Time.time + Mathf.Max(0.05f, def.Duration)));
                    break;
            }
        }

        /// <summary>跟随 Actor 更新持续 Cue，并回收到期的瞬时 Cue。</summary>
        /// <param name="resolvePosition">按 ActorId 取世界坐标；找不到时返回 null。</param>
        public void Tick(Func<ActorId, Vector3?> resolvePosition)
        {
            if (resolvePosition != null)
            {
                foreach (var pair in _activeCues)
                {
                    var cue = pair.Value;
                    if (cue.View == null)
                    {
                        continue;
                    }

                    var pos = resolvePosition(cue.Actor);
                    if (pos.HasValue)
                    {
                        cue.View.transform.position = FollowPosition(pos.Value);
                    }
                }
            }

            var now = Time.time;
            for (var i = _bursts.Count - 1; i >= 0; i--)
            {
                if (now < _bursts[i].ExpireAt)
                {
                    continue;
                }

                DestroyGo(_bursts[i].View);
                _bursts.RemoveAt(i);
            }
        }

        /// <summary>销毁全部表现。</summary>
        public void Clear()
        {
            foreach (var pair in _activeCues)
            {
                DestroyGo(pair.Value.View);
            }

            _activeCues.Clear();
            for (var i = 0; i < _bursts.Count; i++)
            {
                DestroyGo(_bursts[i].View);
            }

            _bursts.Clear();
        }

        void ReplaceActive(string key, ActorId actor, GameObject view)
        {
            DestroyActive(key);
            if (view != null)
            {
                _activeCues[key] = new ActiveCue(actor, view);
            }
        }

        void DestroyActive(string key)
        {
            if (_activeCues.TryGetValue(key, out var cue))
            {
                DestroyGo(cue.View);
                _activeCues.Remove(key);
            }
        }

        static string MakeKey(in GameplayCueEvent evt) => evt.Actor.Value + ":" + evt.CueTag;

        static Vector3 FollowPosition(Vector3 actorPosition) => actorPosition + Vector3.up * FollowHeight;

        static GameObject Spawn(CfgCueDef def, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cue_" + def.Id;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            var scale = def.Scale > 0f ? def.Scale : 0.4f;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(def.ColorR, def.ColorG, def.ColorB, 0.85f);
            }

            return go;
        }

        static void DestroyGo(GameObject go)
        {
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }

        readonly struct ActiveCue
        {
            public readonly ActorId Actor;
            public readonly GameObject View;

            public ActiveCue(ActorId actor, GameObject view)
            {
                Actor = actor;
                View = view;
            }
        }

        readonly struct TimedCue
        {
            public readonly GameObject View;
            public readonly float ExpireAt;

            public TimedCue(GameObject view, float expireAt)
            {
                View = view;
                ExpireAt = expireAt;
            }
        }
    }
}
