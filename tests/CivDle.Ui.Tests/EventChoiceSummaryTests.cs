using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Řádek pod volbou v události: hráč musí před kliknutím vědět, co volba
/// stojí, co dá a jaký dozvuk spustí — ve všech jazycích.
/// </summary>
public class EventChoiceSummaryTests
{
    [Theory]
    [InlineData(0.75, "−25")]
    [InlineData(1.3, "+30")]
    [InlineData(1.0, "+0")]
    [InlineData(0.5, "−50")]
    public void MultiplierReadsAsASignedChange(double multiplier, string expected)
    {
        Assert.Equal(expected, EventChoiceSummary.SignedPercent(multiplier));
    }

    [Fact]
    public void EveryRealChoiceWithSomethingToSay_GetsALineInEveryLanguage()
    {
        // Chybějící klíč by se v bublině ukázal jako syrové ID — a u dozvuku,
        // který hráč neviděl předem, by přišel o celý smysl řádku.
        var content = Content();
        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);
            foreach (var gameEvent in content.Events.All)
            {
                foreach (var choice in gameEvent.Choices)
                {
                    string line = EventChoiceSummary.Line(content, loc, choice);
                    bool saysSomething = choice.Cost.Count + choice.Gain.Count > 0 || choice.Effect is not null;

                    Assert.Equal(saysSomething, line.Length > 0);
                    Assert.DoesNotContain("event.effect", line);
                }
            }
        }
    }

    [Fact]
    public void TheFloodSaysWhatIgnoringItCosts()
    {
        var content = Content();
        var loc = new Localization(content.Languages, "cs");
        var flood = content.Events[content.Events.IndexOf("river_flood")];
        var moveOn = flood.Choices.Single(c => c.Effect is not null);

        string line = EventChoiceSummary.Line(content, loc, moveOn);

        Assert.Equal("Jídlo −25 % na 3 min", line);
    }

    [Fact]
    public void RunningEffectsShowTheTimeLeft()
    {
        var content = Content();
        var loc = new Localization(content.Languages, "cs");
        int food = content.Resources.IndexOf("food");
        var active = new[]
        {
            new ActiveEventEffect(EventEffectKind.Production, food, 0.75, EndsAtTick: 1500),
            new ActiveEventEffect(EventEffectKind.Growth, -1, 1.3, EndsAtTick: 1045),
        };

        string line = EventChoiceSummary.ActiveLine(content, loc, active, tick: 600);

        Assert.Equal("Jídlo −25 % (1:30)   Růst +30 % (0:45)", line);
        Assert.True(EventChoiceSummary.AnyPenalty(active));
        Assert.Equal(string.Empty, EventChoiceSummary.ActiveLine(content, loc, Array.Empty<ActiveEventEffect>(), 0));
    }

    private static GameContent Content() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
