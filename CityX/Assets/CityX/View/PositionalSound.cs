using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A one-shot positional sound (windows, doors) with a pitch, a volume and an optional delay -- which
    /// AudioSource.PlayClipAtPoint cannot do (it has no pitch and no delay). The break of a window is a
    /// crack and then, a beat later, the tinkle of the pieces landing: two plays of the one sample, the
    /// second higher and quieter, so the game does not need a second recording (and its file size).
    /// </summary>
    public static class PositionalSound
    {
        public static void Play(AudioClip clip, Vector3 point, float volume, float pitch, float delaySeconds, float maxDistance)
        {
            if (clip == null)
            {
                return;
            }

            var holder = new GameObject("GlassSound");
            holder.transform.position = point;
            AudioSource source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2.0f;
            source.maxDistance = Mathf.Max(maxDistance, 3.0f);
            source.playOnAwake = false;
            source.PlayDelayed(Mathf.Max(0.0f, delaySeconds));
            Object.Destroy(holder, delaySeconds + clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.25f);
        }
    }
}
