using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Glazecs.Modules.FileChunker.Rules
{
    /// <summary>
    /// Правило удаления комментариев из C# кода.
    /// </summary>
    public sealed class CSharpCommentRemovalRule(
        ILogger<CSharpCommentRemovalRule>? logger,
        IStringLocalizer<FileChunkerResources> localizer) : CSharpTriviaRemovalRuleBase(logger)
    {
        public override string Name => localizer["Rule_Comment_Name"];

        public override string Description => localizer["Rule_Comment_Desc"];

        protected override bool ShouldRemove(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                   trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
        }
    }
}