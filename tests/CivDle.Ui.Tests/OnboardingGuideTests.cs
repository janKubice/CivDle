using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Průvodce prvními pěti minutami: ukazuje na strom a na místo pro stavbu
/// a ohlásí velké okamžiky — každý jen jednou za profil hráče.
/// </summary>
public class OnboardingGuideTests
{
    private static readonly GameContent Content =
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));

    private static Simulation NewGame(long seed = 20260728)
    {
        var preset = Content.WorldGen.Presets[Content.WorldGen.DefaultPresetIndex];
        return new Simulation(Content, new ProceduralTerrain(Content.Biomes, preset, seed), seed);
    }

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void TheFirstStepPointsAtATreeNearTheStart()
    {
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());

        guide.Update(1f, nightFactor: 0);

        var target = guide.Target;
        Assert.Equal(GuidePointer.Harvest, target.Kind);
        Assert.Equal(Content.Resources.IndexOf("wood"), sim.NodeResourceAt(target.X, target.Y));
        var (startX, startY) = guide.StartTile;
        Assert.InRange(Math.Max(Math.Abs(target.X - startX), Math.Abs(target.Y - startY)), 0, 8); // na první obrazovce
    }

    [Fact]
    public void AFelledTreeMovesThePointerToTheNextOne()
    {
        // Šipka nad pařezem by hráče poslala klikat do prázdna — přesně to,
        // co průvodce má vyřešit.
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());
        guide.Update(1f, 0);
        var first = guide.Target;

        while (sim.NodeResourceAt(first.X, first.Y) >= 0)
        {
            Assert.True(sim.TryHarvest(first.X, first.Y, out _, out _));
            if (sim.GetResource(Content.Resources.IndexOf("wood")) >= sim.GetStorageCap(Content.Resources.IndexOf("wood")) - 5)
            {
                sim.DebugSetResource(Content.Resources.IndexOf("wood"), 0); // ať se sklad nezaplní dřív než strom
            }
        }

        guide.Update(0.01f, 0);

        Assert.NotEqual((first.X, first.Y), (guide.Target.X, guide.Target.Y));
        Assert.True(sim.NodeResourceAt(guide.Target.X, guide.Target.Y) >= 0);
    }

    [Fact]
    public void TheShelterStepMarksAPlaceForTheHouseNextToTheCampfire()
    {
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());
        sim.SetTutorialStepForTest(StepIndex("shelter"));

        guide.Update(1f, 0);

        var target = guide.Target;
        int house = Content.Buildings.IndexOf("house");
        Assert.Equal(GuidePointer.Build, target.Kind);
        Assert.Equal(house, target.DefIndex);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(house, target.X, target.Y));
        Assert.NotEqual(guide.StartTile, (target.X, target.Y)); // u ohně, ne na něm
    }

    [Fact]
    public void TheLumberCampIsSuggestedByTheForest()
    {
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());
        sim.SetTutorialStepForTest(StepIndex("automate"));

        guide.Update(1f, 0);

        var target = guide.Target;
        var camp = Content.Buildings[Content.Buildings.IndexOf("lumber_camp")];
        Assert.Equal(GuidePointer.Build, target.Kind);
        int trees = 0;
        for (int y = target.Y - camp.TerrainHarvestRadius; y <= target.Y + camp.TerrainHarvestRadius; y++)
        {
            for (int x = target.X - camp.TerrainHarvestRadius; x <= target.X + camp.TerrainHarvestRadius; x++)
            {
                trees += sim.NodeResourceAt(x, y) >= 0 ? 1 : 0;
            }
        }

        Assert.True(trees >= 6, $"dřevorubec by u ({target.X},{target.Y}) měl jen {trees} uzlů");
    }

    [Fact]
    public void TheFirstProductionIsCelebratedOncePerPlayer()
    {
        var profile = new PlayerProfile();
        var guide = new OnboardingGuide(Content, NewGame(), profile);

        guide.OnProduced(Content.Resources.IndexOf("wood"));
        guide.OnProduced(Content.Resources.IndexOf("wood"));

        Assert.True(guide.TryTakeMoment(out var moment));
        Assert.Equal(OnboardingMoment.WorksAlone, moment);
        Assert.False(guide.TryTakeMoment(out _)); // druhá výroba už není novinka

        // Nová hra téhož hráče: oslava se neopakuje.
        var again = new OnboardingGuide(Content, NewGame(), profile);
        again.OnProduced(Content.Resources.IndexOf("wood"));
        Assert.False(again.TryTakeMoment(out _));
    }

    [Fact]
    public void SafeToCloseComesOnlyAfterTheTownGrowsByItself()
    {
        // „Klidně zavři, vesnice poroste dál" je slib — dřív, než hráč
        // uvidí růst na vlastní oči, by mu nevěřil.
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());

        sim.SetTutorialStepForTest(StepIndex("grow"));
        guide.Update(400f, 0);
        Assert.False(guide.TryTakeMoment(out _));

        sim.SetTutorialStepForTest(StepIndex("grow") + 1);
        guide.Update(1f, 0);
        Assert.True(guide.TryTakeMoment(out var moment));
        Assert.Equal(OnboardingMoment.SafeToClose, moment);
    }

    [Fact]
    public void SafeToCloseNeverComesInTheFirstFiveMinutes()
    {
        // Změřeno: vesnice doroste za necelou minutu. „Klidně zavři" ve 40.
        // vteřině by hráče poslalo pryč dřív, než hra začala.
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());
        sim.SetTutorialStepForTest(StepIndex("grow") + 1);

        guide.Update(60f, 0);
        Assert.False(guide.TryTakeMoment(out _));

        guide.Update(250f, 0);
        Assert.True(guide.TryTakeMoment(out var moment));
        Assert.Equal(OnboardingMoment.SafeToClose, moment);
    }

    [Fact]
    public void TheQuietStartLastsUntilTheTownGrowsByItself()
    {
        // Zakázky a volby počkají, dokud hráč nezvládne první kroky.
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());
        Assert.True(guide.IsQuietStart);

        sim.SetTutorialStepForTest(StepIndex("grow") + 1);
        Assert.False(guide.IsQuietStart);

        var skipped = NewGame();
        skipped.SkipTutorial();
        Assert.False(new OnboardingGuide(Content, skipped, new PlayerProfile()).IsQuietStart);
    }

    [Theory]
    [InlineData(NotificationKind.Milestone, true)]        // krok průvodce
    [InlineData(NotificationKind.QuestCompleted, true)]
    [InlineData(NotificationKind.AchievementUnlocked, true)]
    [InlineData(NotificationKind.WorldEvent, false)]      // „výsledek voleb" ve třetí vteřině
    [InlineData(NotificationKind.ContractOffered, false)]
    [InlineData(NotificationKind.ContractReady, false)]
    [InlineData(NotificationKind.GovernorStuck, false)]
    public void OnlyTheFirstStepsSpeakDuringTheQuietStart(NotificationKind kind, bool shown)
    {
        Assert.Equal(shown, OnboardingGuide.BelongsToStart(kind));
    }

    [Fact]
    public void TheFirstNightNeedsSomethingToLightUp()
    {
        // Tma nad táborákem není oslava. Až když stojí pár domů, je co rozsvítit.
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());

        guide.Update(1f, nightFactor: 1);

        Assert.False(guide.TryTakeMoment(out _));
    }

    [Fact]
    public void AStepChangeIsReportedOnce()
    {
        var sim = NewGame();
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());

        Assert.True(guide.TryTakeStepChange(out var first));
        Assert.Equal("gather", first.Id);
        Assert.False(guide.TryTakeStepChange(out _));

        sim.SetTutorialStepForTest(StepIndex("shelter"));
        Assert.True(guide.TryTakeStepChange(out var next));
        Assert.Equal("shelter", next.Id);
    }

    [Fact]
    public void TheIntroFlightLandsExactlyOnTheCampfire()
    {
        var target = new Vector2(100, 200);
        var flight = new IntroFlight(target, new Vector2(-300, 250), fromZoom: 0.7f, toZoom: 2.2f, seconds: 3f);

        Assert.Equal(0.7f, flight.Zoom, 3);
        Assert.Equal(target + new Vector2(-300, 250), flight.Position);

        flight.Update(1.5f);
        Assert.InRange(flight.Zoom, 0.8f, 2.1f);
        Assert.False(flight.IsDone);

        flight.Update(2f);
        Assert.True(flight.IsDone);
        Assert.Equal(2.2f, flight.Zoom, 3);
        Assert.Equal(target, flight.Position);
    }

    [Fact]
    public void TheIntroCanBeSkipped()
    {
        var flight = new IntroFlight(Vector2.Zero, new Vector2(50, 50), 0.7f, 2.2f, 3f);

        flight.Finish();

        Assert.True(flight.IsDone);
        Assert.Equal(Vector2.Zero, flight.Position);
    }

    private static int StepIndex(string id)
    {
        for (int i = 0; i < Content.Tutorial.Count; i++)
        {
            if (Content.Tutorial[i].Id == id)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"krok '{id}' v datech není");
    }
}
