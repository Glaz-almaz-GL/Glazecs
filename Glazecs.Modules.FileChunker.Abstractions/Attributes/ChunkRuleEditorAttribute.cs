namespace Glazecs.Modules.FileChunker.Abstractions.Attributes
{
    /// <summary>
    /// Атрибут для связывания правила с его UI-редактором.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ChunkRuleEditorAttribute(Type editorComponentType) : Attribute
    {
        public Type EditorComponentType { get; } = editorComponentType;
    }
}
