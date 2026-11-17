using System.Collections;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The first story NPC, who runs the tutorial (docs/TUTORIAL.md): he watches you land, offers to show you the ropes, sends you into his
    /// house after the swordsman, waits in his den once it is clear and teaches the rest there, until the bullets come through the windows.
    /// What he says is TutorialScript's; where the player has got to is <see cref="Progress"/>, which the tutorial's director in the UI moves
    /// on as it watches the world. Nothing of it is saved partway: quitting puts him back to his opening line.
    /// </summary>
    public sealed class OldGuy : UniqueNpc
    {
        private const string OptionAccept = "accept";
        private const string OptionDecline = "decline";
        private const string OptionFactionYes = "faction_yes";
        private const string OptionFactionNo = "faction_no";

        /// <summary>How long his fall takes when he is shot, seconds.</summary>
        public const float CollapseSeconds = 0.45f;

        /// <summary>Lying down, how high his middle stays off the floor, metres.</summary>
        private const float LyingLift = 0.25f;

        /// <summary>Where he is seen from, for whether he can be moved unseen: his chest.</summary>
        private const float ChestHeight = 1.6f;

        public TutorialProgress Progress { get; } = new TutorialProgress();

        /// <summary>Shot: lying on the floor.</summary>
        public bool Collapsed { get; private set; }

        private bool _declined;

        protected override void Update()
        {
            if (!Collapsed)
            {
                base.Update();
            }
        }

        // --- what he says ---

        protected override DialogueNode[] DialogueNodes()
        {
            switch (Progress.Step)
            {
                case TutorialStep.Offered:
                    return _declined ? new[] { OfferNode() } : new[] { new DialogueNode(TutorialScript.FallLine), OfferNode() };
                case TutorialStep.GetIn:
                case TutorialStep.FindWeapon:
                case TutorialStep.Intruder:
                    return Nodes(TutorialScript.WhileOutside);
                case TutorialStep.ReportBack:
                    return Nodes(TutorialScript.HouseCleared);
                case TutorialStep.Power:
                    return Nodes(TutorialScript.PowerReminder);
                case TutorialStep.BackToOldGuy:
                    return Progress.OpenedTheBoxes
                        ? Nodes(TutorialScript.PowerBack, TutorialScript.SawTheBoxes)
                        : Nodes(TutorialScript.PowerBack);
                case TutorialStep.Stats:
                    return Nodes(TutorialScript.StatsReminder);
                case TutorialStep.Skill:
                    return Nodes(TutorialScript.SkillReminder);
                case TutorialStep.LastTalk:
                    return FactionTalk();
                default:
                    return System.Array.Empty<DialogueNode>();
            }
        }

        private static DialogueNode OfferNode()
        {
            return new DialogueNode(TutorialScript.Offer, new[]
            {
                new DialogueOption(TutorialScript.OfferYes, OptionAccept),
                new DialogueOption(TutorialScript.OfferNo, OptionDecline),
            });
        }

        private static DialogueNode[] FactionTalk()
        {
            DialogueNode[] before = Nodes(TutorialScript.BeforeTheFaction);
            var nodes = new DialogueNode[before.Length + 1];
            before.CopyTo(nodes, 0);
            nodes[before.Length] = new DialogueNode(TutorialScript.FactionAsk, new[]
            {
                new DialogueOption(TutorialScript.FactionYes, OptionFactionYes),
                new DialogueOption(TutorialScript.FactionNo, OptionFactionNo),
            });
            return nodes;
        }

        /// <summary>One node per line, with <paramref name="first"/> (when given) before them.</summary>
        private static DialogueNode[] Nodes(string[] lines, string first = null)
        {
            int offset = first != null ? 1 : 0;
            var nodes = new DialogueNode[lines.Length + offset];
            if (first != null)
            {
                nodes[0] = new DialogueNode(first);
            }
            for (int i = 0; i < lines.Length; i++)
            {
                nodes[i + offset] = new DialogueNode(lines[i]);
            }
            return nodes;
        }

        protected override void OnDialogueOptionChosen(string id)
        {
            switch (id)
            {
                case OptionAccept:
                    Progress.Accept();
                    ContinueTalking(Nodes(TutorialScript.Accepted));
                    return;
                case OptionDecline:
                    _declined = true;
                    ContinueTalking(new[] { new DialogueNode(TutorialScript.TurnedDown) });
                    return;
                case OptionFactionYes:
                case OptionFactionNo:
                    bool yes = id == OptionFactionYes;
                    Progress.StartedAFaction = yes;
                    ContinueTalking(Nodes(TutorialScript.Farewell, yes ? TutorialScript.FactionYesAnswer : TutorialScript.FactionNoAnswer));
                    return;
            }
        }

        /// <summary>
        /// A talk step is passed when its conversation ends, walked away from or not (the objective says what is next either way); the last
        /// one only once the faction question has an answer, so walking off before it asks again.
        /// </summary>
        protected override void OnDialogueFinished()
        {
            if (Progress.Step == TutorialStep.LastTalk && Progress.StartedAFaction == null)
            {
                return;
            }
            Progress.Talked();
        }

        // --- where he stands ---

        /// <summary>
        /// Moves him to <paramref name="position"/> if nobody would see it happen: neither where he stands nor where he goes is in view of
        /// <paramref name="camera"/> (a wall or floor between counts as out of view). True when he moved.
        /// </summary>
        public bool TryMoveUnseen(Vector3 position, Quaternion rotation, Camera camera)
        {
            if (camera != null && (IsSeenBy(camera, transform.position + Vector3.up * ChestHeight) || IsSeenBy(camera, position + Vector3.up * ChestHeight)))
            {
                return false;
            }
            bool wasEnabled = Controller.enabled;
            Controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            Controller.enabled = wasEnabled;
            return true;
        }

        /// <summary>A point on screen with nothing solid between it and the camera.</summary>
        public static bool IsSeenBy(Camera camera, Vector3 point)
        {
            return IsInCameraFrustum(camera, point) && !Sight.SolidBetween(camera.transform.position, point, null, null);
        }

        /// <summary>Whether a world point falls inside the camera's view; a point behind the camera never does.</summary>
        public static bool IsInCameraFrustum(Camera camera, Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > 0.0f &&
                   viewport.x >= 0.0f && viewport.x <= 1.0f &&
                   viewport.y >= 0.0f && viewport.y <= 1.0f;
        }

        // --- shot ---

        /// <summary>
        /// Shot: he falls over stiff, away from <paramref name="shotsFrom"/>, and stays down. A stand-in until he has a body that can go
        /// limp (he has no ragdoll: UniqueNpc holds his hips and spine still every frame).
        /// </summary>
        public void Collapse(Vector3 shotsFrom)
        {
            if (Collapsed)
            {
                return;
            }
            Collapsed = true;
            StartCoroutine(Fall(shotsFrom));
        }

        private IEnumerator Fall(Vector3 shotsFrom)
        {
            Controller.enabled = false;
            if (BodyAnimator != null)
            {
                BodyAnimator.speed = 0.0f;
            }
            Vector3 away = transform.position - shotsFrom;
            away.y = 0.0f;
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -transform.forward;
            }
            Vector3 axis = Vector3.Cross(Vector3.up, away.normalized);
            Vector3 feet = transform.position;
            Quaternion standing = transform.rotation;
            for (float t = 0.0f; t < CollapseSeconds; t += Time.deltaTime)
            {
                float k = t / CollapseSeconds;
                k *= k;
                transform.SetPositionAndRotation(feet + Vector3.up * (LyingLift * k), Quaternion.AngleAxis(90.0f * k, axis) * standing);
                yield return null;
            }
            transform.SetPositionAndRotation(feet + Vector3.up * LyingLift, Quaternion.AngleAxis(90.0f, axis) * standing);
        }
    }
}
