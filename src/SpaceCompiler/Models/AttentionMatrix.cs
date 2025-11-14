namespace SpaceCompiler.Models
{
    /// <summary>
    /// Represents the self-attention matrix computed between content blocks
    /// This matrix captures the relevance and coherence relationships between all blocks
    /// </summary>
    public class AttentionMatrix
    {
        /// <summary>
        /// The attention scores matrix
        /// Each entry [i,j] represents the attention score from block i to block j
        /// </summary>
        public double[,] Scores { get; set; } = new double[0, 0];

        /// <summary>
        /// Coherence probabilities between blocks
        /// Each entry [i,j] represents the coherence probability from block i to block j
        /// </summary>
        public double[,] CoherenceProbabilities { get; set; } = new double[0, 0];

        /// <summary>
        /// Block references corresponding to matrix indices
        /// </summary>
        public List<BlockReference> Blocks { get; set; } = new();

        /// <summary>
        /// Metadata about the attention calculation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Reference to a content block in the attention matrix
    /// </summary>
    public class BlockReference
    {
        /// <summary>
        /// Resource identifier the block belongs to
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Block order/index within the resource
        /// </summary>
        public int BlockOrder { get; set; }

        /// <summary>
        /// Block type
        /// </summary>
        public string BlockType { get; set; } = "block";

        /// <summary>
        /// Preview of block content (first N characters)
        /// </summary>
        public string ContentPreview { get; set; } = string.Empty;

        /// <summary>
        /// Full content length
        /// </summary>
        public int ContentLength { get; set; }
    }
}
