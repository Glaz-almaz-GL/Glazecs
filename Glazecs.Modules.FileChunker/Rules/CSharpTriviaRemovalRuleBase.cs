using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Glazecs.Modules.FileChunker.Rules
{
    /// <summary>
    /// Базовый абстрактный класс для правил, удаляющих определенные виды trivia (пробельных символов и комментариев) из кода C#.
    /// </summary>
    public abstract class CSharpTriviaRemovalRuleBase : IChunkRule
    {
        public abstract string Name { get; }
        public abstract string Description { get; }

        public string Apply(string content)
        {
            ArgumentNullException.ThrowIfNull(content);

            // 1. Парсим исходный текст в синтаксическое дерево
            SyntaxTree tree = CSharpSyntaxTree.ParseText(content);
            SyntaxNode root = tree.GetRoot();

            // 2. Применяем переписчик, передавая ему логику фильтрации из наследника
            TriviaRemovalRewriter rewriter = new(ShouldRemove);
            SyntaxNode newRoot = rewriter.Visit(root);

            // 3. Возвращаем модифицированный код
            return newRoot.ToFullString();
        }

        /// <summary>
        /// Определяет, должен ли конкретный элемент trivia быть удален.
        /// Реализуется в классах-наследниках.
        /// </summary>
        protected abstract bool ShouldRemove(SyntaxTrivia trivia);

        /// <summary>
        /// Инкапсулированный переписчик, который фильтрует trivia на основе переданного предиката.
        /// </summary>
        private sealed class TriviaRemovalRewriter(Func<SyntaxTrivia, bool> predicate) : CSharpSyntaxRewriter
        {
            private readonly Func<SyntaxTrivia, bool> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

            public override SyntaxToken VisitToken(SyntaxToken token)
            {
                SyntaxToken newToken = base.VisitToken(token);

                SyntaxTriviaList newLeading = SyntaxFactory.TriviaList(
                    newToken.LeadingTrivia.Where(t => !_predicate(t))
                );

                SyntaxTriviaList newTrailing = SyntaxFactory.TriviaList(
                    newToken.TrailingTrivia.Where(t => !_predicate(t))
                );

                return newToken.WithLeadingTrivia(newLeading).WithTrailingTrivia(newTrailing);
            }
        }
    }
}