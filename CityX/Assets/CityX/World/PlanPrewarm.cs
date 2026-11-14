using System.Threading;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Starts growing the city on a worker thread the moment the application starts, so the title screen hides the cost.
    ///
    /// The plan (roads, blocks, lots, twelve thousand buildings) costs about a quarter of a second warm and over a second the first
    /// time code runs; asked for cold by the world scene's streamer it would be a one-second hitch right as the player enters the
    /// game. <see cref="PlanWorld.For"/> is cached per seed and takes a lock, so if the world scene asks while this is still running it
    /// simply waits for it -- the work is never done twice -- and if it asks for a different seed it generates that one itself.
    /// </summary>
    public static class PlanPrewarm
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    PlanWorld.For(CitySeed.Default);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            });
        }
    }
}
