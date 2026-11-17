using System.Numerics;
using Meitou.Data.Gameplay.Ai;
using Meitou.Simulation;
using Meitou.Simulation.Ai;
using Meitou.Simulation.Movement;

namespace Husbandry;

/// <summary>
/// The pen round a post (docs/husbandry.md "Pen"): a hitched animal standing idle outside the radius walks back in; one inside strolls about now and
/// then. Anything else it is doing goes first: a player's order (riding a mount is one), a fight, a food run to a feeder or food on the ground, a
/// job. So it leaves the pen only to eat, or when the player sends it, and comes back once it stands idle again.
/// </summary>
public static class Pens
{
    /// <summary>
    /// The share of the radius a walk back aims within, so the animal stops well inside the fence.
    /// </summary>
    const float ReturnShare = 0.5f;

    /// <summary>
    /// The share of the radius a stroll may reach.
    /// </summary>
    const float GrazeShare = 0.85f;

    /// <summary>
    /// The chance at each check that an idle animal inside the pen strolls to another spot.
    /// </summary>
    const float GrazeChance = 0.15f;

    /// <summary>
    /// Spots tried for a walkable one.
    /// </summary>
    const int SpotTries = 6;

    /// <summary>
    /// The rows of the food the animal may leave the pen for: 243 food from storage (a feeder), its take 244, and 259 food on the ground.
    /// </summary>
    static readonly HashSet<TaskType> FoodRows = [TaskType.AquireFoodAtHomebase, TaskType.GrabOneFood, TaskType.EatFoodOnGround];

    /// <summary>
    /// Keeps one hitched animal in its pen. Serial step.
    /// </summary>
    public static void Keep(World world, Hitched h, HusbandrySettings settings, RngPurpose purpose)
    {
        if (!Idle(world, h)) return;

        var at = world.Characters.Next[h.Slot].Position;
        var post = new Vector2(h.Post.Position.X, h.Post.Position.Z);
        float away = Vector2.Distance(new Vector2(at.X, at.Z), post);
        ulong roll = Rng.Hash(world.Seed, h.Cold.PortraitSerial ?? (ulong)h.Slot, purpose, (ulong)world.Tick);

        if (away > settings.PenRadius) Walk(world, h.Slot, post, settings.PenRadius * ReturnShare, roll);
        else if (settings.Grazing && Rng.Float(roll) < GrazeChance) Walk(world, h.Slot, post, settings.PenRadius * GrazeShare, Rng.Mix(roll));
    }

    /// <summary>
    /// Whether the animal stands idle with nothing of its own to do: up, not on a food run, not fetching food.
    /// </summary>
    static bool Idle(World world, Hitched h)
    {
        if (Hitches.Down(h.Cold) || h.Cold.FetchingFood) return false;
        if (h.Cold.Running is { } run && FoodRows.Contains(run.Row) && run.Runs((CharacterTask)world.Characters.Next[h.Slot].Task)) return false;

        return (CharacterTask)world.Characters.Next[h.Slot].Task == CharacterTask.Idle;
    }

    /// <summary>
    /// Walks the animal to a walkable spot within <paramref name="radius"/> of the post (the post itself when none is found).
    /// </summary>
    static void Walk(World world, int slot, Vector2 post, float radius, ulong roll)
    {
        var goal = post;
        for (int k = 0; k < SpotTries; k++)
        {
            float angle = Rng.Float(Rng.Hash(roll, (ulong)k, 0, 1)) * MathF.Tau;
            float r = MathF.Sqrt(Rng.Float(Rng.Hash(roll, (ulong)k, 0, 2))) * radius;
            var spot = post + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            if (!world.Walkability.IsWalkable(spot.X, spot.Y)) continue;

            goal = spot;
            break;
        }

        TaskStart.Walk(world, slot, goal, SpeedMode.Walk, TaskType.MoveOnFreeWill, TaskSource.Npc);
    }
}
