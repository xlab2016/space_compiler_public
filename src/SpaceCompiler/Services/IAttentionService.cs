using SpaceCompiler.Models;

namespace SpaceCompiler.Services
{
    /// <summary>
    /// Service for computing self-attention between content blocks
    /// Implements the attention mechanism for the Space Compiler ontology
    /// </summary>
    public interface IAttentionService
    {
        /// <summary>
        /// Computes self-attention matrix for all blocks across all resources
        /// This calculates relevance scores and coherence probabilities between blocks
        /// </summary>
        /// <param name="resources">Parsed resources containing content blocks</param>
        /// <returns>Attention matrix with scores and coherence probabilities</returns>
        Task<AttentionMatrix> ComputeAttentionAsync(List<ParsedResource> resources);
    }
}
