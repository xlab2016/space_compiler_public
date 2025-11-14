using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SpaceCompiler.Models;
using SpaceCompiler.Services;
using Xunit;

namespace SpaceCompiler.Tests.Services
{
    public class AttentionServiceTests
    {
        private readonly AttentionService _service;
        private readonly Mock<ILogger<AttentionService>> _mockLogger;

        public AttentionServiceTests()
        {
            _mockLogger = new Mock<ILogger<AttentionService>>();
            _service = new AttentionService(_mockLogger.Object);
        }

        [Fact]
        public async Task ComputeAttentionAsync_EmptyResources_ReturnsEmptyMatrix()
        {
            // Arrange
            var resources = new List<ParsedResource>();

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            result.Should().NotBeNull();
            result.Blocks.Should().BeEmpty();
            result.Scores.GetLength(0).Should().Be(0);
            result.Scores.GetLength(1).Should().Be(0);
            result.Metadata.Should().ContainKey("block_count");
            result.Metadata["block_count"].Should().Be(0);
        }

        [Fact]
        public async Task ComputeAttentionAsync_SingleBlock_ReturnsSelfAttention()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "test.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "Hello world this is a test",
                            Type = "block",
                            Order = 0
                        }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            result.Should().NotBeNull();
            result.Blocks.Should().HaveCount(1);
            result.Scores.GetLength(0).Should().Be(1);
            result.Scores.GetLength(1).Should().Be(1);
            result.Scores[0, 0].Should().Be(1.0); // Self-attention after softmax normalization
            result.CoherenceProbabilities[0, 0].Should().Be(1.0);
            result.Metadata["block_count"].Should().Be(1);
        }

        [Fact]
        public async Task ComputeAttentionAsync_MultipleBlocks_ComputesAttentionScores()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "test.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "Machine learning and artificial intelligence",
                            Type = "block",
                            Order = 0
                        },
                        new ContentBlock
                        {
                            Content = "Deep learning neural networks",
                            Type = "block",
                            Order = 1
                        },
                        new ContentBlock
                        {
                            Content = "The weather is nice today",
                            Type = "block",
                            Order = 2
                        }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            result.Should().NotBeNull();
            result.Blocks.Should().HaveCount(3);
            result.Scores.GetLength(0).Should().Be(3);
            result.Scores.GetLength(1).Should().Be(3);

            // Check that attention scores sum to 1 for each row (softmax property)
            for (int i = 0; i < 3; i++)
            {
                var rowSum = 0.0;
                for (int j = 0; j < 3; j++)
                {
                    rowSum += result.Scores[i, j];
                }
                rowSum.Should().BeApproximately(1.0, 0.0001);
            }

            // Blocks 0 and 1 should have similar or higher attention to each other (similar content about ML/AI)
            // than to block 2 (different topic about weather)
            // Note: Due to softmax normalization and small vocabulary overlap, differences might be minimal
            result.Scores[0, 1].Should().BeGreaterThanOrEqualTo(result.Scores[0, 2]);
            result.Scores[1, 0].Should().BeGreaterThanOrEqualTo(result.Scores[1, 2]);

            // Check coherence probabilities
            result.CoherenceProbabilities.GetLength(0).Should().Be(3);
            result.CoherenceProbabilities.GetLength(1).Should().Be(3);

            // Coherence probabilities should sum to 1 for each row
            for (int i = 0; i < 3; i++)
            {
                var rowSum = 0.0;
                for (int j = 0; j < 3; j++)
                {
                    rowSum += result.CoherenceProbabilities[i, j];
                }
                rowSum.Should().BeApproximately(1.0, 0.0001);
            }
        }

        [Fact]
        public async Task ComputeAttentionAsync_AdjacentBlocks_HaveHigherCoherence()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "story.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "Once upon a time in a kingdom",
                            Type = "block",
                            Order = 0
                        },
                        new ContentBlock
                        {
                            Content = "There lived a brave knight",
                            Type = "block",
                            Order = 1
                        },
                        new ContentBlock
                        {
                            Content = "Who saved the princess from danger",
                            Type = "block",
                            Order = 2
                        }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            // Adjacent blocks should have higher coherence than non-adjacent blocks
            // Block 0 -> Block 1 (adjacent) should have higher coherence than Block 0 -> Block 2 (non-adjacent)
            result.CoherenceProbabilities[0, 1].Should().BeGreaterThan(result.CoherenceProbabilities[0, 2]);
        }

        [Fact]
        public async Task ComputeAttentionAsync_MultipleResources_ComputesUnifiedAttention()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "file1.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "Python programming language",
                            Type = "block",
                            Order = 0
                        }
                    }
                },
                new ParsedResource
                {
                    ResourceId = "file2.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "JavaScript coding examples",
                            Type = "block",
                            Order = 0
                        }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            result.Should().NotBeNull();
            result.Blocks.Should().HaveCount(2);
            result.Blocks[0].ResourceId.Should().Be("file1.txt");
            result.Blocks[1].ResourceId.Should().Be("file2.txt");

            // Check that cross-resource attention is computed
            result.Scores[0, 1].Should().BeGreaterThan(0);
            result.Scores[1, 0].Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ComputeAttentionAsync_SetsMetadata()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "test.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock { Content = "Test content", Order = 0 }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            result.Metadata.Should().ContainKey("computed_at");
            result.Metadata.Should().ContainKey("block_count");
            result.Metadata.Should().ContainKey("resource_count");
            result.Metadata.Should().ContainKey("vocabulary_size");
            result.Metadata.Should().ContainKey("algorithm");

            result.Metadata["block_count"].Should().Be(1);
            result.Metadata["resource_count"].Should().Be(1);
            result.Metadata["algorithm"].Should().Be("TF-IDF + Cosine Similarity + Softmax");
        }

        [Fact]
        public async Task ComputeAttentionAsync_PopulatesBlockReferences()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "document.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "This is a very long content that should be truncated in the preview because it exceeds one hundred characters in length",
                            Type = "paragraph",
                            Order = 5
                        }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            result.Blocks.Should().HaveCount(1);
            var blockRef = result.Blocks[0];

            blockRef.ResourceId.Should().Be("document.txt");
            blockRef.BlockOrder.Should().Be(5);
            blockRef.BlockType.Should().Be("paragraph");
            blockRef.ContentPreview.Should().EndWith("...");
            blockRef.ContentPreview.Length.Should().Be(103); // 100 chars + "..."
            blockRef.ContentLength.Should().BeGreaterThan(100);
        }

        [Fact]
        public async Task ComputeAttentionAsync_IdenticalContent_HasMaximumAttention()
        {
            // Arrange
            var resources = new List<ParsedResource>
            {
                new ParsedResource
                {
                    ResourceId = "test.txt",
                    ResourceType = "text",
                    Blocks = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Content = "Identical content for testing",
                            Type = "block",
                            Order = 0
                        },
                        new ContentBlock
                        {
                            Content = "Identical content for testing",
                            Type = "block",
                            Order = 1
                        }
                    }
                }
            };

            // Act
            var result = await _service.ComputeAttentionAsync(resources);

            // Assert
            // Identical content should have high attention scores
            result.Scores[0, 1].Should().BeGreaterThan(0.4); // Should be high after softmax
            result.Scores[1, 0].Should().BeGreaterThan(0.4);
        }
    }
}
