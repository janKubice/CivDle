namespace CivDle.Core.Save;

/// <summary>
/// Save nejde načíst: špatný formát, nepodporovaná verze, nebo odkazuje na obsah
/// (ID), který v aktuálních datech neexistuje. UI ji chytá a hráči ukáže
/// srozumitelnou hlášku — poškozený save nesmí shodit hru.
/// </summary>
public sealed class SaveLoadException : Exception
{
    public SaveLoadException(string message)
        : base(message)
    {
    }

    public SaveLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
