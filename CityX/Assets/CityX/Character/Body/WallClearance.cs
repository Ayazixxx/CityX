using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The one move that takes a body out of every wall its parts are in (Ragdoll.OutOfWalls): each contact says which way out it is and how
    /// far, and the move goes at least that far along each of those ways. Two contacts pushing nearly opposite ways are a gap narrower than
    /// the body: no move gets it out of both, and pushing each way in turn would shake it, so it is left where it is.
    /// </summary>
    public static class WallClearance
    {
        /// <summary>How many times every contact is looked at again, a move along one having changed what another still needs.</summary>
        private const int Passes = 3;

        public static Vector3 Combined(Vector3[] outs, int count, float squeezeDot)
        {
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (Vector3.Dot(outs[i].normalized, outs[j].normalized) < squeezeDot)
                    {
                        return Vector3.zero;
                    }
                }
            }
            Vector3 push = Vector3.zero;
            for (int pass = 0; pass < Passes; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 way = outs[i].normalized;
                    float need = outs[i].magnitude - Vector3.Dot(push, way);
                    if (need > 0.0f)
                    {
                        push += way * need;
                    }
                }
            }
            return push;
        }
    }
}
