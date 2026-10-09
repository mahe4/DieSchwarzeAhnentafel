using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Tests;

/// <summary>Die echten, eingebetteten Regeldaten.</summary>
public static class TestRules
{
    public static RuleData Data { get; } = RuleData.Load(EmbeddedContent.Open, EmbeddedContent.List());
}
