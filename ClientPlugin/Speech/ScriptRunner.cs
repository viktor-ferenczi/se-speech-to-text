using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI;

namespace ClientPlugin.Speech;

// Runs the programmable blocks whose name matches one of the configured
// patterns with the transcript as their argument. Patterns match the whole
// block name, case-insensitively, with * standing for anything.
internal static class ScriptRunner
{
    // Returns the number of blocks run
    public static int Run(IEnumerable<string> patterns, ScriptScope scope, string argument)
    {
        var regexes = patterns.Select(ToRegex).ToList();
        if (regexes.Count == 0)
            return 0;

        var count = 0;
        if (scope != ScriptScope.AllAccessibleGrids)
            count = RunOn(ControlledGridGroup(), regexes, argument);

        if (scope == ScriptScope.AllAccessibleGrids || (scope == ScriptScope.ControlledGroupThenAll && count == 0))
            count = RunOn(AllGrids(), regexes, argument);

        return count;
    }

    private static int RunOn(IEnumerable<MyCubeGrid> grids, List<Regex> regexes, string argument)
    {
        var count = 0;
        foreach (var grid in grids)
        {
            foreach (var block in grid.GetFatBlocks<MyProgrammableBlock>())
            {
                if (!block.HasLocalPlayerAccess())
                    continue;

                var name = block.CustomName.ToString();
                if (!regexes.Any(regex => regex.IsMatch(name)))
                    continue;

                // Sends a request to the server when this is a client
                block.Run(argument, UpdateType.Terminal);
                count++;
            }
        }

        return count;
    }

    // The controlled grid with everything attached by rotors, pistons and hinges.
    // Seated in a cockpit or cryopod the controlled entity is that block, and
    // MySession.TopMostControlledEntity would still be the block for a station
    // (nobody "controls" the grid), so go through the block's grid instead.
    private static IEnumerable<MyCubeGrid> ControlledGridGroup()
    {
        var controlled = MySession.Static.ControlledEntity?.Entity;
        var grid = (controlled as MyCubeBlock)?.CubeGrid ?? controlled?.GetTopMostParent() as MyCubeGrid;
        if (grid == null)
            return Enumerable.Empty<MyCubeGrid>();

        var grids = grid.GetConnectedGrids(GridLinkTypeEnum.Mechanical);
        if (!grids.Contains(grid))
            grids.Add(grid);

        return grids;
    }

    private static IEnumerable<MyCubeGrid> AllGrids() => MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

    private static Regex ToRegex(string pattern)
    {
        var expression = "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$";
        return new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
