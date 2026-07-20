using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Localization;

namespace Glazecs.Modules.FileChunker.Rules
{
    public sealed class CSharpCommentRemovalRule(IStringLocalizer<FileChunkerResources> localizer) : CSharpTriviaRemovalRuleBase
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