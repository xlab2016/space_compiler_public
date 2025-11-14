using SpaceCompiler.Models;
using System.Text;

namespace SpaceCompiler.Services
{
    /// <summary>
    /// Service for computing self-attention between content blocks
    /// Implements relevance scoring and coherence probability calculations
    /// </summary>
    public class AttentionService : IAttentionService
    {
        private readonly ILogger<AttentionService> _logger;

        public AttentionService(ILogger<AttentionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AttentionMatrix> ComputeAttentionAsync(List<ParsedResource> resources)
        {
            _logger.LogInformation("Computing self-attention matrix for {ResourceCount} resources", resources.Count);

            // Collect all blocks from all resources
            var allBlocks = new List<(ParsedResource Resource, ContentBlock Block)>();

            foreach (var resource in resources)
            {
                foreach (var block in resource.Blocks)
                {
                    allBlocks.Add((resource, block));
                }
            }

            var blockCount = allBlocks.Count;

            if (blockCount == 0)
            {
                _logger.LogWarning("No blocks found for attention computation");
                return new AttentionMatrix
                {
                    Metadata = new Dictionary<string, object>
                    {
                        ["computed_at"] = DateTime.UtcNow,
                        ["block_count"] = 0
                    }
                };
            }

            _logger.LogInformation("Computing attention for {BlockCount} blocks", blockCount);

            // Create block references
            var blockReferences = allBlocks.Select((item, index) => new BlockReference
            {
                ResourceId = item.Resource.ResourceId,
                BlockOrder = item.Block.Order,
                BlockType = item.Block.Type,
                ContentPreview = item.Block.Content.Length > 100
                    ? item.Block.Content.Substring(0, 100) + "..."
                    : item.Block.Content,
                ContentLength = item.Block.Content.Length
            }).ToList();

            // Initialize matrices
            var attentionScores = new double[blockCount, blockCount];
            var coherenceProbabilities = new double[blockCount, blockCount];

            // Compute vocabulary for TF-IDF calculation
            var vocabulary = await BuildVocabularyAsync(allBlocks);
            var idfScores = ComputeIDF(allBlocks, vocabulary);

            // Compute TF-IDF vectors for all blocks
            var tfidfVectors = new Dictionary<string, double>[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                tfidfVectors[i] = ComputeTFIDF(allBlocks[i].Block.Content, vocabulary, idfScores);
            }

            // Compute attention scores using cosine similarity
            await Task.Run(() =>
            {
                for (int i = 0; i < blockCount; i++)
                {
                    for (int j = 0; j < blockCount; j++)
                    {
                        if (i == j)
                        {
                            // Self-attention is 1.0
                            attentionScores[i, j] = 1.0;
                        }
                        else
                        {
                            // Compute cosine similarity between TF-IDF vectors
                            var similarity = ComputeCosineSimilarity(tfidfVectors[i], tfidfVectors[j]);
                            attentionScores[i, j] = similarity;
                        }
                    }
                }

                // Normalize attention scores using softmax for each row
                for (int i = 0; i < blockCount; i++)
                {
                    var rowScores = new double[blockCount];
                    for (int j = 0; j < blockCount; j++)
                    {
                        rowScores[j] = attentionScores[i, j];
                    }

                    var normalizedScores = Softmax(rowScores);
                    for (int j = 0; j < blockCount; j++)
                    {
                        attentionScores[i, j] = normalizedScores[j];
                    }
                }

                // Compute coherence probabilities
                // Coherence measures how well blocks flow together (considering order)
                for (int i = 0; i < blockCount; i++)
                {
                    for (int j = 0; j < blockCount; j++)
                    {
                        var baseCoherence = attentionScores[i, j];

                        // Boost coherence for adjacent blocks
                        var sameResource = allBlocks[i].Resource.ResourceId == allBlocks[j].Resource.ResourceId;
                        var orderDiff = Math.Abs(allBlocks[i].Block.Order - allBlocks[j].Block.Order);

                        if (sameResource && orderDiff == 1)
                        {
                            // Adjacent blocks get coherence boost
                            baseCoherence *= 1.5;
                        }
                        else if (sameResource && orderDiff <= 3)
                        {
                            // Nearby blocks get smaller boost
                            baseCoherence *= 1.2;
                        }

                        // Apply distance penalty for non-adjacent blocks
                        if (orderDiff > 0)
                        {
                            var distancePenalty = 1.0 / (1.0 + Math.Log(1.0 + orderDiff));
                            baseCoherence *= distancePenalty;
                        }

                        coherenceProbabilities[i, j] = Math.Min(1.0, baseCoherence);
                    }
                }

                // Normalize coherence probabilities for each row
                for (int i = 0; i < blockCount; i++)
                {
                    var rowSum = 0.0;
                    for (int j = 0; j < blockCount; j++)
                    {
                        rowSum += coherenceProbabilities[i, j];
                    }

                    if (rowSum > 0)
                    {
                        for (int j = 0; j < blockCount; j++)
                        {
                            coherenceProbabilities[i, j] /= rowSum;
                        }
                    }
                }
            });

            _logger.LogInformation("Self-attention computation completed for {BlockCount} blocks", blockCount);

            return new AttentionMatrix
            {
                Scores = attentionScores,
                CoherenceProbabilities = coherenceProbabilities,
                Blocks = blockReferences,
                Metadata = new Dictionary<string, object>
                {
                    ["computed_at"] = DateTime.UtcNow,
                    ["block_count"] = blockCount,
                    ["resource_count"] = resources.Count,
                    ["vocabulary_size"] = vocabulary.Count,
                    ["algorithm"] = "TF-IDF + Cosine Similarity + Softmax"
                }
            };
        }

        private async Task<HashSet<string>> BuildVocabularyAsync(List<(ParsedResource Resource, ContentBlock Block)> blocks)
        {
            return await Task.Run(() =>
            {
                var vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (_, block) in blocks)
                {
                    var words = TokenizeText(block.Content);
                    foreach (var word in words)
                    {
                        vocabulary.Add(word);
                    }
                }

                return vocabulary;
            });
        }

        private Dictionary<string, double> ComputeIDF(
            List<(ParsedResource Resource, ContentBlock Block)> blocks,
            HashSet<string> vocabulary)
        {
            var idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var totalDocs = blocks.Count;

            foreach (var word in vocabulary)
            {
                var docsWithWord = 0;

                foreach (var (_, block) in blocks)
                {
                    var words = TokenizeText(block.Content);
                    if (words.Contains(word, StringComparer.OrdinalIgnoreCase))
                    {
                        docsWithWord++;
                    }
                }

                // IDF = log(N / df)
                idf[word] = Math.Log((double)totalDocs / (1.0 + docsWithWord));
            }

            return idf;
        }

        private Dictionary<string, double> ComputeTFIDF(
            string content,
            HashSet<string> vocabulary,
            Dictionary<string, double> idfScores)
        {
            var tfidf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var words = TokenizeText(content);
            var totalWords = words.Count;

            if (totalWords == 0)
            {
                return tfidf;
            }

            // Compute term frequency
            var termFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                if (termFreq.ContainsKey(word))
                {
                    termFreq[word]++;
                }
                else
                {
                    termFreq[word] = 1;
                }
            }

            // Compute TF-IDF
            foreach (var word in vocabulary)
            {
                if (termFreq.ContainsKey(word))
                {
                    var tf = (double)termFreq[word] / totalWords;
                    var idf = idfScores.GetValueOrDefault(word, 0.0);
                    tfidf[word] = tf * idf;
                }
                else
                {
                    tfidf[word] = 0.0;
                }
            }

            return tfidf;
        }

        private double ComputeCosineSimilarity(
            Dictionary<string, double> vector1,
            Dictionary<string, double> vector2)
        {
            var dotProduct = 0.0;
            var magnitude1 = 0.0;
            var magnitude2 = 0.0;

            var allKeys = vector1.Keys.Union(vector2.Keys);

            foreach (var key in allKeys)
            {
                var v1 = vector1.GetValueOrDefault(key, 0.0);
                var v2 = vector2.GetValueOrDefault(key, 0.0);

                dotProduct += v1 * v2;
                magnitude1 += v1 * v1;
                magnitude2 += v2 * v2;
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0.0 || magnitude2 == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }

        private double[] Softmax(double[] scores)
        {
            var maxScore = scores.Max();
            var expScores = scores.Select(s => Math.Exp(s - maxScore)).ToArray();
            var sumExp = expScores.Sum();

            if (sumExp == 0.0)
            {
                // Return uniform distribution
                return scores.Select(_ => 1.0 / scores.Length).ToArray();
            }

            return expScores.Select(e => e / sumExp).ToArray();
        }

        private List<string> TokenizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            // Simple word tokenization
            var words = text
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_' },
                       StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2) // Filter out very short words
                .ToList();

            return words;
        }
    }
}
