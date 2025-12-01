using SpaceCompiler.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpaceCompiler.Services
{
    /// <summary>
    /// Service for tokenizing text into fragments
    /// Migrated from space_db_public parsers
    /// </summary>
    public class TokenizerService : ITokenizerService
    {
        private readonly ILogger<TokenizerService> _logger;
        private readonly int _minParagraphLength;
        private readonly int _maxParagraphLength;
        private readonly DocumentStructureDetector _structureDetector;

        public TokenizerService(
            ILogger<TokenizerService> logger,
            int minParagraphLength = 50,
            int maxParagraphLength = 2000)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _minParagraphLength = minParagraphLength;
            _maxParagraphLength = maxParagraphLength;
            _structureDetector = new DocumentStructureDetector();
        }

        public async Task<List<ContentFragment>> TokenizeAsync(string content, string contentType = "text")
        {
            _logger.LogInformation("Tokenizing content of type: {ContentType}, length: {Length}",
                contentType, content.Length);

            return contentType.ToLowerInvariant() switch
            {
                "text" or "txt" => await TokenizeTextAsync(content),
                "json" => await TokenizeJsonAsync(content),
                _ => await TokenizeTextAsync(content) // Default to text
            };
        }

        public IEnumerable<string> GetSupportedContentTypes()
        {
            return new[] { "text", "txt", "json" };
        }

        /// <summary>
        /// Tokenize plain text into paragraphs/sentences with structure detection
        /// </summary>
        private async Task<List<ContentFragment>> TokenizeTextAsync(string content)
        {
            var fragments = new List<ContentFragment>();

            // Split by double newlines (paragraph separator)
            var rawParagraphs = Regex.Split(content, @"\n\s*\n|\r\n\s*\r\n")
                .Select(p => p.TrimEnd())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            int order = 0;
            var paragraphBuffer = new List<string>();
            string? previousParagraph = null;
            bool inTableOfContents = false;

            for (int i = 0; i < rawParagraphs.Count; i++)
            {
                var paragraph = rawParagraphs[i];
                var normalizedParagraph = NormalizeText(paragraph);

                // Detect structure type for this paragraph
                var structureType = _structureDetector.DetectStructureType(paragraph, previousParagraph);

                // Check if this is a TOC marker
                if (structureType == DocumentStructureType.TableOfContents)
                {
                    // Flush buffer before TOC
                    FlushParagraphBuffer(paragraphBuffer, fragments, ref order);

                    fragments.Add(CreateStructuredFragment(paragraph, order++, structureType));
                    inTableOfContents = true;
                    previousParagraph = paragraph;
                    continue;
                }

                // Check if we're in TOC section
                bool isTocEntry = inTableOfContents &&
                    _structureDetector.IsPossibleTocEntry(paragraph, structureType);

                if (isTocEntry)
                {
                    fragments.Add(CreateStructuredFragment(paragraph, order++, DocumentStructureType.Indented,
                        isTocEntry: true));
                    previousParagraph = paragraph;
                    continue;
                }

                // If we get a regular paragraph after TOC entries, we've left the TOC
                if (inTableOfContents && structureType == DocumentStructureType.Regular)
                {
                    inTableOfContents = false;
                }

                // Handle structural elements (headings, chapters, etc.)
                if (IsStructuralElement(structureType))
                {
                    // Flush buffer before structural element
                    FlushParagraphBuffer(paragraphBuffer, fragments, ref order);

                    fragments.Add(CreateStructuredFragment(paragraph, order++, structureType));
                    previousParagraph = paragraph;
                    continue;
                }

                // Handle indented content - preserve original formatting
                if (ShouldPreserveFormatting(structureType))
                {
                    // Flush buffer before indented content
                    FlushParagraphBuffer(paragraphBuffer, fragments, ref order);

                    // Use original paragraph to preserve indentation
                    fragments.Add(CreateStructuredFragment(paragraph, order++, structureType));
                    previousParagraph = paragraph;
                    continue;
                }

                // Handle regular paragraphs with merging/splitting logic
                if (normalizedParagraph.Length < _minParagraphLength)
                {
                    _logger.LogDebug("Adding short paragraph to buffer: {Length} chars", normalizedParagraph.Length);
                    paragraphBuffer.Add(normalizedParagraph);

                    // Check if buffered content is now long enough
                    var bufferedContent = string.Join("\n\n", paragraphBuffer);
                    if (bufferedContent.Length >= _minParagraphLength)
                    {
                        ProcessParagraphContent(bufferedContent, fragments, ref order);
                        paragraphBuffer.Clear();
                    }
                }
                else
                {
                    // Flush buffer before processing this paragraph
                    FlushParagraphBuffer(paragraphBuffer, fragments, ref order);

                    ProcessParagraphContent(normalizedParagraph, fragments, ref order);
                }

                previousParagraph = paragraph;
            }

            // Flush any remaining buffered content
            FlushParagraphBuffer(paragraphBuffer, fragments, ref order);

            _logger.LogInformation("Tokenized text into {Count} fragments", fragments.Count);
            return fragments;
        }

        /// <summary>
        /// Checks if a structure type represents a structural element (heading, chapter, etc.)
        /// Note: Indented is NOT considered structural here since it needs different handling
        /// </summary>
        private bool IsStructuralElement(DocumentStructureType structureType)
        {
            return structureType switch
            {
                DocumentStructureType.Chapter or
                DocumentStructureType.RomanNumberedHeading or
                DocumentStructureType.NumberedHeadingLevel1 or
                DocumentStructureType.NumberedHeadingLevel2 or
                DocumentStructureType.NumberedHeadingLevel3 or
                DocumentStructureType.NumberedHeadingDeep or
                DocumentStructureType.UppercaseHeading => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if a structure type should preserve original formatting (like indentation)
        /// </summary>
        private bool ShouldPreserveFormatting(DocumentStructureType structureType)
        {
            return structureType == DocumentStructureType.Indented;
        }

        /// <summary>
        /// Flushes the paragraph buffer to fragments
        /// </summary>
        private void FlushParagraphBuffer(List<string> buffer, List<ContentFragment> fragments, ref int order)
        {
            if (buffer.Count > 0)
            {
                var bufferedContent = string.Join("\n\n", buffer);
                ProcessParagraphContent(bufferedContent, fragments, ref order);
                buffer.Clear();
                _logger.LogDebug("Flushed paragraph buffer with {Count} paragraphs", buffer.Count);
            }
        }

        /// <summary>
        /// Processes paragraph content, splitting if necessary
        /// </summary>
        private void ProcessParagraphContent(string content, List<ContentFragment> fragments, ref int order)
        {
            if (content.Length > _maxParagraphLength)
            {
                var chunks = SplitLongParagraph(content);
                foreach (var chunk in chunks)
                {
                    fragments.Add(CreateFragment(chunk, order++));
                }
            }
            else
            {
                fragments.Add(CreateFragment(content, order++));
            }
        }

        /// <summary>
        /// Tokenize JSON into hierarchical fragments
        /// </summary>
        private async Task<List<ContentFragment>> TokenizeJsonAsync(string content)
        {
            var fragments = new List<ContentFragment>();
            int order = 0;

            try
            {
                using var document = JsonDocument.Parse(content);
                TokenizeJsonElement(document.RootElement, "root", fragments, ref order, 0, null);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON content");
                // Fall back to treating as text
                return await TokenizeTextAsync(content);
            }

            _logger.LogInformation("Tokenized JSON into {Count} fragments", fragments.Count);
            return fragments;
        }

        private void TokenizeJsonElement(
            JsonElement element,
            string path,
            List<ContentFragment> fragments,
            ref int order,
            int depth,
            string? parentKey)
        {
            if (depth > 10) // Max depth limit
            {
                _logger.LogWarning("Max depth reached at path {Path}", path);
                return;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    TokenizeJsonObject(element, path, fragments, ref order, depth, parentKey);
                    break;

                case JsonValueKind.Array:
                    TokenizeJsonArray(element, path, fragments, ref order, depth, parentKey);
                    break;

                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    var value = GetValueString(element);
                    if (element.ValueKind == JsonValueKind.String && value.Length > 20)
                    {
                        fragments.Add(new ContentFragment
                        {
                            Content = value,
                            Type = "json_value",
                            Order = order++,
                            ParentKey = parentKey,
                            Metadata = new Dictionary<string, object>
                            {
                                ["path"] = path,
                                ["value_type"] = "string",
                                ["length"] = value.Length
                            }
                        });
                    }
                    break;
            }
        }

        private void TokenizeJsonObject(
            JsonElement element,
            string path,
            List<ContentFragment> fragments,
            ref int order,
            int depth,
            string? parentKey)
        {
            var properties = new List<string>();

            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                properties.Add($"{property.Name}: {GetValuePreview(property.Value)}");

                if (ShouldTokenize(property.Value))
                {
                    TokenizeJsonElement(property.Value, propertyPath, fragments, ref order, depth + 1, path);
                }
            }

            if (properties.Count > 0)
            {
                var objectSummary = $"Object with {properties.Count} properties: " +
                    string.Join(", ", properties.Take(5));

                if (properties.Count > 5)
                {
                    objectSummary += $", ... ({properties.Count - 5} more)";
                }

                fragments.Add(new ContentFragment
                {
                    Content = objectSummary,
                    Type = "json_object",
                    Order = order++,
                    ParentKey = parentKey,
                    Metadata = new Dictionary<string, object>
                    {
                        ["path"] = path,
                        ["property_count"] = properties.Count,
                        ["depth"] = depth
                    }
                });
            }
        }

        private void TokenizeJsonArray(
            JsonElement element,
            string path,
            List<ContentFragment> fragments,
            ref int order,
            int depth,
            string? parentKey)
        {
            var arrayLength = element.GetArrayLength();
            var items = new List<string>();

            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemPath = $"{path}[{index}]";
                items.Add(GetValuePreview(item));

                if (ShouldTokenize(item))
                {
                    TokenizeJsonElement(item, itemPath, fragments, ref order, depth + 1, path);
                }

                index++;
            }

            var arraySummary = $"Array with {arrayLength} items";
            if (items.Count > 0)
            {
                arraySummary += ": " + string.Join(", ", items.Take(3));
                if (items.Count > 3)
                {
                    arraySummary += $", ... ({items.Count - 3} more)";
                }
            }

            fragments.Add(new ContentFragment
            {
                Content = arraySummary,
                Type = "json_array",
                Order = order++,
                ParentKey = parentKey,
                Metadata = new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["array_length"] = arrayLength,
                    ["depth"] = depth
                }
            });
        }

        private ContentFragment CreateFragment(string content, int order)
        {
            return new ContentFragment
            {
                Content = content,
                Type = "paragraph",
                Order = order,
                Metadata = new Dictionary<string, object>
                {
                    ["length"] = content.Length,
                    ["word_count"] = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
                }
            };
        }

        /// <summary>
        /// Creates a fragment with structural information
        /// </summary>
        private ContentFragment CreateStructuredFragment(
            string content,
            int order,
            DocumentStructureType structureType,
            bool isTocEntry = false)
        {
            var fragment = new ContentFragment
            {
                Content = content.Trim(),
                Type = GetFragmentType(structureType, isTocEntry),
                Order = order,
                Metadata = new Dictionary<string, object>
                {
                    ["length"] = content.Length,
                    ["word_count"] = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    ["structure_type"] = structureType.ToString()
                }
            };

            // Extract numbering if present
            var numbering = _structureDetector.ExtractNumbering(content);
            if (!string.IsNullOrEmpty(numbering))
            {
                fragment.Metadata["numbering"] = numbering;
                fragment.Metadata["heading_text"] = _structureDetector.ExtractHeadingText(content);
            }

            // Add indentation level if applicable
            if (structureType == DocumentStructureType.Indented)
            {
                var indentLevel = _structureDetector.GetIndentationLevel(content);
                fragment.Metadata["indent_level"] = indentLevel;
            }

            if (isTocEntry)
            {
                fragment.Metadata["is_toc_entry"] = true;
            }

            return fragment;
        }

        /// <summary>
        /// Maps document structure type to fragment type string
        /// </summary>
        private string GetFragmentType(DocumentStructureType structureType, bool isTocEntry)
        {
            if (isTocEntry)
                return "toc_entry";

            return structureType switch
            {
                DocumentStructureType.TableOfContents => "toc_header",
                DocumentStructureType.Chapter => "chapter",
                DocumentStructureType.RomanNumberedHeading => "heading_roman",
                DocumentStructureType.NumberedHeadingLevel1 => "heading_1",
                DocumentStructureType.NumberedHeadingLevel2 => "heading_2",
                DocumentStructureType.NumberedHeadingLevel3 => "heading_3",
                DocumentStructureType.NumberedHeadingDeep => "heading_deep",
                DocumentStructureType.UppercaseHeading => "heading_uppercase",
                DocumentStructureType.Indented => "indented",
                _ => "paragraph"
            };
        }

        private List<string> SplitLongParagraph(string paragraph)
        {
            var chunks = new List<string>();

            // Try to split by sentences first
            var sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var currentChunk = new List<string>();
            int currentLength = 0;

            foreach (var sentence in sentences)
            {
                if (currentLength + sentence.Length > _maxParagraphLength && currentChunk.Count > 0)
                {
                    chunks.Add(string.Join(" ", currentChunk));
                    currentChunk.Clear();
                    currentLength = 0;
                }

                currentChunk.Add(sentence);
                currentLength += sentence.Length;
            }

            if (currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return chunks;
        }

        private async Task<List<string>> SplitLongParagraphAsync(string paragraph)
        {
            var chunks = new List<string>();

            // Try to split by sentences first
            var sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var currentChunk = new List<string>();
            int currentLength = 0;

            foreach (var sentence in sentences)
            {
                if (currentLength + sentence.Length > _maxParagraphLength && currentChunk.Count > 0)
                {
                    chunks.Add(string.Join(" ", currentChunk));
                    currentChunk.Clear();
                    currentLength = 0;
                }

                currentChunk.Add(sentence);
                currentLength += sentence.Length;
            }

            if (currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return await Task.FromResult(chunks);
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            return text;
        }

        private bool ShouldTokenize(JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Object ||
                   element.ValueKind == JsonValueKind.Array ||
                   (element.ValueKind == JsonValueKind.String && element.GetString()?.Length > 20);
        }

        private string GetValuePreview(JsonElement element, int maxLength = 50)
        {
            var value = GetValueString(element);
            if (value.Length > maxLength)
            {
                return value.Substring(0, maxLength) + "...";
            }
            return value;
        }

        private string GetValueString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? "",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Object => $"{{...}}",
                JsonValueKind.Array => $"[{element.GetArrayLength()} items]",
                _ => element.GetRawText()
            };
        }
    }
}
