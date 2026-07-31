using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Ohňostroj nad městem. Testuje se to, co by v běhu bylo vidět jako chyba:
/// světlice musí opravdu prasknout (jinak by jen vyletěly a zmizely), vše
/// musí do pár vteřin dohasnout, pooly nesmí přetéct — a přístupnostní volba
/// „omezit pohyb" musí ohňostroj skutečně vypnout.
///
/// <para>Běží headless: <c>Burst</c> ani <c>Update</c> na grafiku nesahají.</para>
/// </summary>
public sealed class FireworksTests
{
    private static void Run(FireworksRenderer fireworks, float seconds, float dt = 1f / 60f)
    {
        for (float t = 0; t < seconds; t += dt)
        {
            fireworks.Update(dt);
        }
    }

    [Fact]
    public void NothingIsPlayingUntilSomethingIsCelebrated()
    {
        Assert.False(new FireworksRenderer().IsPlaying);
    }

    [Fact]
    public void ABurstSendsShellsUp()
    {
        var fireworks = new FireworksRenderer();

        fireworks.Burst(Vector2.Zero, seed: 1);

        Assert.True(fireworks.ShellCount > 0);
        Assert.True(fireworks.IsPlaying);
    }

    [Fact]
    public void ShellsActuallyExplode()
    {
        // Kdyby světlice jen vyletěla a zmizela, byl by z ohňostroje jen ohon.
        var fireworks = new FireworksRenderer();
        fireworks.Burst(Vector2.Zero, seed: 7);

        Run(fireworks, 1.6f);

        Assert.True(fireworks.SparkCount > 0, "Ze světlic se nerozletěly žádné jiskry.");
    }

    [Fact]
    public void TheSkyClearsOnItsOwn()
    {
        // Oslava má trvat pár vteřin a nic po ní nezbýt.
        var fireworks = new FireworksRenderer();
        fireworks.Burst(Vector2.Zero, seed: 3);

        Run(fireworks, 8f);

        Assert.False(fireworks.IsPlaying);
        Assert.Equal(0, fireworks.ShellCount);
        Assert.Equal(0, fireworks.SparkCount);
    }

    [Fact]
    public void ManyCelebrationsAtOnceDoNotOverflowThePools()
    {
        // Deset milníků naráz musí stát stejně jako jeden.
        var fireworks = new FireworksRenderer();
        for (int i = 0; i < 50; i++)
        {
            fireworks.Burst(Vector2.Zero, seed: i);
            fireworks.Update(1f / 60f);
        }

        Run(fireworks, 3f);

        Assert.True(fireworks.ShellCount <= 12);
        Assert.True(fireworks.SparkCount <= 420);
    }

    [Fact]
    public void ReducedMotionSilencesTheFireworks()
    {
        // Přístupnostní volba musí platit i pro tu nejefektnější věc ve hře.
        var fireworks = new FireworksRenderer { Enabled = false };

        fireworks.Burst(Vector2.Zero, seed: 1);

        Assert.False(fireworks.IsPlaying);
    }

    [Fact]
    public void ClearWipesTheSkyImmediately()
    {
        // Po Vzestupu se starý svět nemá dosvítit nad novým.
        var fireworks = new FireworksRenderer();
        fireworks.Burst(Vector2.Zero, seed: 5);
        Run(fireworks, 1.5f);
        Assert.True(fireworks.IsPlaying);

        fireworks.Clear();

        Assert.False(fireworks.IsPlaying);
    }

    [Fact]
    public void TheSameSeedGivesTheSameShow()
    {
        // Seed se odvozuje z místa a času, ne z Random.Shared — jinak by se
        // salva mezi snímky mihotala.
        var first = new FireworksRenderer();
        var second = new FireworksRenderer();

        first.Burst(new Vector2(100, 100), seed: 42);
        second.Burst(new Vector2(100, 100), seed: 42);
        Run(first, 1.2f);
        Run(second, 1.2f);

        Assert.Equal(first.ShellCount, second.ShellCount);
        Assert.Equal(first.SparkCount, second.SparkCount);
    }
}
