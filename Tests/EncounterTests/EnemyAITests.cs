using RuinGamePDT.Combat;
using RuinGamePDT.Creatures;
using RuinGamePDT.Encounter;
using RuinGamePDT.World;

namespace RuinGamePDT.Tests;

public class EnemyAITests
{
    private static Func<int, int, int> Rolls(params int[] values)
    {
        var queue = new Queue<int>(values);
        return (_, _) => queue.Dequeue();
    }

    // Always-hit rolls for an arbitrary number of rounds. Hit and crit rolls
    // (max == 101) return 100; everything else (hit count, damage) returns the
    // bottom of the range. Distinguishing by `max` is the only way to tell the
    // roll types apart without state.
    private static Func<int, int, int> AlwaysHit() =>
        (min, max) => max == 101 ? 100 : min;

    private static EncounterState MakeState(int width = 30, int height = 30)
        => new(new EncounterMap(width, height));

    private static Creature PlaceGoblin(EncounterState state, int x, int y)
    {
        var g = new PricklebackGoblin();
        state.Enemies.Add(g);
        state.PlaceCreature(g, x, y);
        return g;
    }

    private static Creature PlaceMerc(EncounterState state, int x, int y, int stamina = 10)
    {
        var m = new TestCreature("M", 1, 1, 1, 1, stamina);
        state.Mercenaries.Add(m);
        state.PlaceCreature(m, x, y);
        return m;
    }

    private static EnemyAI Ai(Func<int, int, int>? rng = null) =>
        new(new CombatResolver(rng ?? AlwaysHit()));

    [Fact]
    public void TakeTurn_DoesNothing_WhenGoblinNotPlaced()
    {
        var state = MakeState();
        var g = new PricklebackGoblin();
        // Not placed
        Ai().TakeTurn(g, state); // should not throw
    }

    [Fact]
    public void TakeTurn_DoesNothing_WhenNoMercs()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var startPos = state.GetPosition(g);
        Ai().TakeTurn(g, state);
        Assert.Equal(startPos, state.GetPosition(g));
    }

    [Fact]
    public void TakeTurn_MovesTowardNearestMerc()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 0, 0);
        var farMerc = PlaceMerc(state, 25, 25);
        var nearMerc = PlaceMerc(state, 10, 0);

        Ai().TakeTurn(g, state);

        // Goblin moves toward nearMerc (positive X direction), not toward farMerc.
        var endPos = state.GetPosition(g);
        Assert.True(endPos.X > 0, $"Expected goblin to move right toward near merc, ended at {endPos}");
        // Confirm goblin ended up closer to nearMerc than to farMerc.
        int distToNear = Math.Max(Math.Abs(endPos.X - 10), Math.Abs(endPos.Y - 0));
        int distToFar  = Math.Max(Math.Abs(endPos.X - 25), Math.Abs(endPos.Y - 25));
        Assert.True(distToNear < distToFar);
    }

    [Fact]
    public void TakeTurn_DoesNotMove_WhenAlreadyAtBestTile()
    {
        // Goblin already adjacent to merc; movement won't improve Chebyshev distance.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5);

        var startPos = state.GetPosition(g);
        // The attack loop terminates after 1 attack (MaxAttacksPerTurn default), not AP exhaustion.
        Ai().TakeTurn(g, state);

        // Goblin shouldn't have moved away from the merc — Chebyshev distance
        // stays at 1 either way, but moving wouldn't strictly decrease it.
        Assert.Equal(startPos, state.GetPosition(g));
    }

    [Fact]
    public void TakeTurn_AttacksWhenMercInRange()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 1); // adjacent — Scratch and Skewer both reach

        float hpBefore = m.CurrentHp;
        Ai().TakeTurn(g, state);

        Assert.True(m.CurrentHp < hpBefore);
    }

    [Fact]
    public void TakeTurn_StopsAfterOneAttack_ByDefault()
    {
        // Prickleback (Agility 6) → AP = 4, MaxAttacksPerTurn defaults to 1.
        // Even with AP remaining, the goblin should stop after its one allowed attack.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100); // 200 HP, survives

        int apBefore = state.GetRemainingActionPoints(g);
        Ai().TakeTurn(g, state);

        Assert.Equal(4, apBefore);
        // Only 1 attack (AP cost 1) should have been spent, leaving 3 AP.
        Assert.Equal(3, state.GetRemainingActionPoints(g));
    }

    [Fact]
    public void TakeTurn_StopsIfAllMercsDie()
    {
        // Low-HP merc adjacent to goblin; first attack kills, second iteration should bail.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 1); // 2 HP — Scratch (1-3) kills

        // Use a high-damage roll to ensure the kill.
        Func<int, int, int> roll = (min, max) =>
        {
            // hitCount=1, hit=100, crit=0 (no crit), dmg=max-1 (top of range)
            if (min == 1 && max == 2) return 1;       // hit count
            if (min == 1 && max == 101) return 100;   // hit roll
            return max - 1;                            // damage / crit roll
        };
        Ai(roll).TakeTurn(g, state);

        Assert.Empty(state.Mercenaries);
        // Goblin should not still be looping — AP may or may not be drained, just no crash.
    }

    [Fact]
    public void TakeTurn_PicksHighestAverageDamageAttack()
    {
        // Goblin attacks: Scratch (1-3 avg 2), Skewer (2-4 avg 3), Quill Spray (0-1 avg 0.5).
        // All reach an adjacent target. Goblin should pick Skewer (highest avg) as its
        // one attack this turn. Skewer applies Bleed on hit; Scratch does not — so a
        // Bleed status effect after exactly one attack confirms Skewer was chosen.
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100); // survive the hit

        int oneToHundredOneCalls = 0;
        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;     // hit count
            if (min == 1 && max == 101)
            {
                oneToHundredOneCalls++;
                // 1st call: hit roll (need >= threshold, use 100). 2nd: crit roll (100, doesn't matter).
                // 3rd: OnHit proc roll (need <= 25, use 1).
                return oneToHundredOneCalls == 3 ? 1 : 100;
            }
            return max - 1;                          // damage = max - 1 (top of range)
        };

        Ai(roll).TakeTurn(g, state);

        Assert.NotEmpty(m.StatusEffects);
    }

    [Fact]
    public void TakeTurn_SingleTarget_PicksLowestHpMerc()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var healthyMerc = PlaceMerc(state, 6, 5, stamina: 2); // lower HP but still higher than wounded
        var woundedMerc = PlaceMerc(state, 6, 6, stamina: 1);
        woundedMerc.CurrentHp = 5;

        float healthyHpBefore = healthyMerc.CurrentHp;
        float woundedHpBefore = woundedMerc.CurrentHp;

        // Single AI call — goblin makes its one allowed attack this turn, both mercs in
        // range. Focus-fire should target the wounded one.
        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return min; // minimum damage to keep wounded merc alive past first hit
        };
        Ai(roll).TakeTurn(g, state);

        Assert.True(woundedMerc.CurrentHp < woundedHpBefore, "wounded merc should have taken damage first");
    }

    [Fact]
    public void ChooseBestAttack_SkipsAttackOnCooldown_FallsBackToNextBest()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100);

        var skewer = g.Attacks.First(a => a.Name == "Skewer");
        g.StartCooldown(skewer); // Skewer (highest avg dmg) is now unavailable

        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return max - 1;
        };
        Ai(roll).TakeTurn(g, state);

        // Scratch (next-best) has no OnHit effect; Quill Spray does apply Bleed.
        // Since Scratch has higher avg damage (2) than Quill Spray (0.5), Scratch
        // should be chosen — no Bleed should be applied.
        Assert.Empty(m.StatusEffects);
    }

    [Fact]
    public void TakeTurn_UsingAttackWithCooldown_StartsItsCooldown()
    {
        var state = MakeState();
        var g = PlaceGoblin(state, 5, 5);
        var m = PlaceMerc(state, 6, 5, stamina: 100);

        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101) return 100;
            return max - 1;
        };
        Ai(roll).TakeTurn(g, state);

        var skewer = g.Attacks.First(a => a.Name == "Skewer");
        Assert.True(g.IsOnCooldown(skewer));
    }

    [Fact]
    public void ChooseBestAttack_PrefersAoeAttack_WhenItHitsMultipleMercs()
    {
        // Scratch avg = 2 (single-target, score = 2). Quill Spray avg = 0.5 but its
        // radius-2 self-centered burst can hit multiple adjacent mercs. With 5 mercs
        // clustered around the goblin, Quill Spray's score (0.5 * 5 = 2.5) exceeds
        // Scratch's (2 * 1 = 2), so the goblin should prefer the AOE attack.
        // Skewer (avg=3) is cooldown-blocked so it can't win the comparison outright
        // and mask the AOE-scoring behavior under test.
        var state = MakeState();
        var g = PlaceGoblin(state, 10, 10);

        // Cooldown-block Skewer so it can't win the comparison outright (Skewer avg=3
        // would otherwise dominate regardless of this test's AOE-scoring concern).
        var skewer = g.Attacks.First(a => a.Name == "Skewer");
        g.StartCooldown(skewer);

        var m1 = PlaceMerc(state, 9, 10, stamina: 100);
        var m2 = PlaceMerc(state, 11, 10, stamina: 100);
        var m3 = PlaceMerc(state, 10, 9, stamina: 100);
        var m4 = PlaceMerc(state, 10, 11, stamina: 100);
        var m5 = PlaceMerc(state, 9, 9, stamina: 100);

        int oneToHundredOneCalls = 0;
        Func<int, int, int> roll = (min, max) =>
        {
            if (min == 1 && max == 2) return 1;
            if (min == 1 && max == 101)
            {
                oneToHundredOneCalls++;
                // Cycle of 3 per hit: hit roll (100), crit roll (100), OnHit proc roll (1, succeeds).
                return oneToHundredOneCalls % 3 == 0 ? 1 : 100;
            }
            return max - 1;
        };
        Ai(roll).TakeTurn(g, state);

        // Quill Spray applies Bleed on hit; Scratch does not. If the goblin picked
        // Quill Spray (correctly valuing the multi-hit burst over single-target
        // Scratch), every surrounded merc should show a Bleed status effect —
        // pinning the burst's actual coverage across all 5 placed mercs.
        Assert.NotEmpty(m1.StatusEffects);
        Assert.NotEmpty(m2.StatusEffects);
        Assert.NotEmpty(m3.StatusEffects);
        Assert.NotEmpty(m4.StatusEffects);
        Assert.NotEmpty(m5.StatusEffects);
    }
}
