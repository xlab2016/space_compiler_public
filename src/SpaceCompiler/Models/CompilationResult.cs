namespace SpaceCompiler.Models
{
    /// <summary>
    /// Result of file compilation
    /// </summary>
    public class CompilationResult
    {
        /// <summary>
        /// Parsed resources
        /// </summary>
        public List<ParsedResource> Resources { get; set; } = new();

        /// <summary>
        /// Self-attention matrix computed between content blocks
        /// Contains relevance scores and coherence probabilities
        /// </summary>
        public AttentionMatrix? AttentionMatrix { get; set; }

        /// <summary>
        /// Compilation metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Any errors encountered during compilation
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Any warnings encountered during compilation
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Success status
        /// </summary>
        public bool Success => Errors.Count == 0;
    }
}
