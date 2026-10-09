using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Aus den Entscheidungen berechneter Stand eines Helden.</summary>
public sealed record CharacterState(
    ExperienceLevel ExperienceLevel,
    IReadOnlyList<AttributeState> Attributes,
    IReadOnlyList<SkillState> Skills,
    IReadOnlyList<CombatTechniqueState> CombatTechniques,
    ApAccount Ap,
    DerivedValues Derived,
    IReadOnlyList<Issue> Issues)
{
    public int Attribute(string id) => Attributes.First(a => a.Definition.Id == id).Value;

    public IEnumerable<Issue> IssuesFor(string targetId) => Issues.Where(i => i.TargetId == targetId);
}

/// <param name="Max">Höchstwert, der gerade gilt (in der Erschaffung durch den Erfahrungsgrad begrenzt); <c>null</c> = unbegrenzt.</param>
/// <param name="Min">Mindestwert (Startwert).</param>
public sealed record AttributeState(AttributeDefinition Definition, int Value, int Min, int? Max, int ApSpent);

/// <param name="Available">Mindestens eine Publikation ist für den Helden aktiviert.</param>
public sealed record SkillState(SkillDefinition Definition, int Value, int Max, int ApSpent, bool Available);

/// <param name="Parry"><c>null</c> bei Fernkampftechniken und Techniken ohne Parade.</param>
/// <param name="RangedAttack">Fernkampfwert, nur bei Fernkampftechniken.</param>
public sealed record CombatTechniqueState(
    CombatTechniqueDefinition Definition,
    int Value,
    int Min,
    int Max,
    int ApSpent,
    bool Available,
    int? Attack,
    int? Parry,
    int? RangedAttack);

public sealed record ApAccount(int Total, int Attributes, int Skills, int CombatTechniques)
{
    public int Spent => Attributes + Skills + CombatTechniques;

    public int Remaining => Total - Spent;
}

/// <summary>Basiswerte, die nur von Eigenschaften abhängen. Werte mit Spezies-Grundwert folgen mit Meilenstein 3.</summary>
public sealed record DerivedValues(int Dodge, int Initiative, int FatePoints);
