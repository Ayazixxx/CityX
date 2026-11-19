using System;
using System.Collections.Generic;

namespace CityX.Core
{
    /// <summary>
    /// How a server judges the bag a player on it reports (docs/SERVER_SAVING.md, step 3b). The rules themselves (which items exist, where each
    /// may be, how many make a stack, how many rounds a gun can hold) live with the items, in the Character assembly, which installs them as
    /// <see cref="CharacterService.BagRules"/>.
    /// </summary>
    public interface IBagRules
    {
        /// <summary>
        /// The bag to keep from <paramref name="reported"/>: what can be carried where it is said to be, and of each item no more than
        /// <paramref name="stored"/> held plus what the server has <paramref name="handed"/> since. What is left out is described in
        /// <paramref name="takenOut"/>.
        /// </summary>
        List<BagEntry> Keep(List<BagEntry> stored, List<BagEntry> reported, Func<string, int> handed, List<string> takenOut);
    }
}
