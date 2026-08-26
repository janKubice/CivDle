namespace CivDle.Core.Sim;

/// <summary>Jak scénář dopadl.</summary>
public enum ScenarioOutcome
{
    /// <summary>Ještě se hraje.</summary>
    Running,

    /// <summary>Zadání splněno.</summary>
    Won,

    /// <summary>Došel čas, nebo se město rozpadlo.</summary>
    Lost,
}
