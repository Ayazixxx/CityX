using System.Collections.Generic;
using CityX.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// Another player's body in a crowd (owner, 2026-11-25: "something that will not tank performance in large crowds of people remember there
    /// is a hub"; docs/CROWD_TEST.md): put on every copy of another player (NetworkedPlayerIdentity), never on this machine's own body. The
    /// crowd is ranked by distance from the camera a few times a second and each body given a tier (CrowdTiers): in full near the camera; past
    /// that, or past the nearest few, no shadow and its costliest posing details left out (ICrowdDetailed parts: a coat's skirt kept off the
    /// legs; PlayerLocomotion: the feet and fingers); off the screen, no shadow and nothing posed that only shows. Its animator stops moving the
    /// bones while none of it is seen. Its skinned meshes keep no last-frame shape for motion vectors: nothing in the game draws with them (the
    /// cameras smooth edges with SMAA, the motion blur is at nothing), and keeping them doubled the bending of every body (measured: 3.9 ms a
    /// frame for 50). With no game camera, or nothing drawn at all (a server, a run with no graphics: nothing could be seen), every body stays
    /// in full.
    /// </summary>
    public sealed class CrowdDetail : MonoBehaviour
    {
        /// <summary>How often a body's renderers are gathered again (clothes and things held come and go), seconds.</summary>
        private const float RegatherEvery = 1.0f;

        private static readonly List<CrowdDetail> Crowd = new List<CrowdDetail>();
        private static readonly List<CrowdDetail> Ranking = new List<CrowdDetail>();
        private static float _rankedAt = float.NegativeInfinity;
        private static int _rankedFrame = -1;

        public CrowdTier Tier { get; private set; } = CrowdTier.Full;

        /// <summary>Nothing is drawn on this machine (no graphics device): no body could be seen, so none is judged by it.</summary>
        private static bool NothingDrawn => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        private Renderer _skin;
        private Animator _animator;
        private AnimatorCullingMode _cullingWas;
        private float _metres;
        private int _nearer;
        private float _gatheredAt = float.NegativeInfinity;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly Dictionary<Renderer, ShadowCastingMode> _shadowsWere = new Dictionary<Renderer, ShadowCastingMode>();
        private readonly List<ICrowdDetailed> _parts = new List<ICrowdDetailed>();
        private readonly List<Renderer> _gone = new List<Renderer>();
        private readonly List<SkinnedMeshRenderer> _bending = new List<SkinnedMeshRenderer>();

        private void OnEnable()
        {
            Crowd.Add(this);
            Transform skin = TransformSearch.FindDeep(transform, BodySurface.SkinName);
            _skin = skin != null ? skin.GetComponent<Renderer>() : null;
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _cullingWas = _animator.cullingMode;
                _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // unseen, its bones are not moved
            }
        }

        private void OnDisable()
        {
            Crowd.Remove(this);
            Show(CrowdTier.Full);
            if (_animator != null)
            {
                _animator.cullingMode = _cullingWas;
            }
        }

        private void Update()
        {
            RankTheCrowdIfDue();
            bool seen = _skin == null || _skin.isVisible;
            CrowdTier tier = NothingDrawn || Camera.main == null ? CrowdTier.Full : CrowdTiers.For(_metres, _nearer, seen);
            if (tier != Tier || Time.time - _gatheredAt >= RegatherEvery)
            {
                Show(tier);
            }
        }

        /// <summary>Every body's distance from the camera and how many are nearer, once for the whole crowd every <see cref="CrowdTiers.RankEvery"/>.</summary>
        private static void RankTheCrowdIfDue()
        {
            if (_rankedFrame == Time.frameCount || Time.time - _rankedAt < CrowdTiers.RankEvery)
            {
                return;
            }
            _rankedFrame = Time.frameCount;
            _rankedAt = Time.time;
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            Vector3 eye = camera.transform.position;
            Ranking.Clear();
            Ranking.AddRange(Crowd);
            foreach (CrowdDetail body in Ranking)
            {
                body._metres = Vector3.Distance(eye, body.transform.position);
            }
            Ranking.Sort((a, b) => a._metres.CompareTo(b._metres));
            for (int i = 0; i < Ranking.Count; i++)
            {
                Ranking[i]._nearer = i;
            }
        }

        /// <summary>This body at <paramref name="tier"/>: its shadows, and every part that does less further down.</summary>
        private void Show(CrowdTier tier)
        {
            Tier = tier;
            _gatheredAt = Time.time;
            _renderers.Clear();
            GetComponentsInChildren(_renderers);
            bool shadows = tier == CrowdTier.Full;
            foreach (Renderer r in _renderers)
            {
                if (shadows)
                {
                    if (_shadowsWere.TryGetValue(r, out ShadowCastingMode was) && r.shadowCastingMode == ShadowCastingMode.Off)
                    {
                        r.shadowCastingMode = was;
                    }
                }
                else if (r.shadowCastingMode != ShadowCastingMode.Off)
                {
                    _shadowsWere[r] = r.shadowCastingMode;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            _bending.Clear();
            GetComponentsInChildren(true, _bending);
            foreach (SkinnedMeshRenderer skinned in _bending)
            {
                skinned.skinnedMotionVectors = false;
            }
            if (shadows)
            {
                _shadowsWere.Clear();
            }
            else
            {
                _gone.Clear();
                foreach (Renderer r in _shadowsWere.Keys)
                {
                    if (r == null) _gone.Add(r);
                }
                foreach (Renderer r in _gone) _shadowsWere.Remove(r);
            }
            _parts.Clear();
            GetComponentsInChildren(true, _parts);
            foreach (ICrowdDetailed part in _parts)
            {
                part.ShowAt(tier);
            }
        }
    }
}
