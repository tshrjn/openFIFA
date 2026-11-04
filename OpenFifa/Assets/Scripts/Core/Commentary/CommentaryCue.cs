namespace OpenFifa.Core
{
    /// <summary>Instruction from the engine to the audio layer.</summary>
    public readonly struct CommentaryCue
    {
        public CommentaryCueKind Kind { get; }
        public CommentaryUtterance Utterance { get; }

        public CommentaryCue(CommentaryCueKind kind, CommentaryUtterance utterance)
        {
            Kind = kind;
            Utterance = utterance;
        }

        public override string ToString() => Kind + " " + Utterance;
    }
}
