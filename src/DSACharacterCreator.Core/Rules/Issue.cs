namespace DSACharacterCreator.Core.Rules;

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>Geltungsbereich einer Regel (PLAN.md Abschnitt 5.1).</summary>
public enum RuleScope
{
    Always,
    CreationOnly,
}

/// <summary>Ein Hinweis der Validierung. Validierung warnt, sie sperrt nicht.</summary>
/// <param name="Code">Stabile Kennung der Regel, z. B. für Overrides.</param>
/// <param name="TargetId">Betroffener Wert (Eigenschaft, Talent …), falls vorhanden.</param>
public sealed record Issue(IssueSeverity Severity, string Code, RuleScope Scope, string Message, string? TargetId = null);
