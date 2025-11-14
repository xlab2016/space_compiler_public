using SpaceCompiler.Models;
using System.IO.Compression;

namespace SpaceCompiler.Services
{
    /// <summary>
    /// Main compilation service orchestrating tokenization, parsing, and analysis
    /// </summary>
    public class CompilationService : ICompilationService
    {
        private readonly ITokenizerService _tokenizerService;
        private readonly IParserService _parserService;
        private readonly IAnalyzerService _analyzerService;
        private readonly IAttentionService _attentionService;
        private readonly ISpaceProjParser _spaceProjParser;
        private readonly ILogger<CompilationService> _logger;

        public CompilationService(
            ITokenizerService tokenizerService,
            IParserService parserService,
            IAnalyzerService analyzerService,
            IAttentionService attentionService,
            ISpaceProjParser spaceProjParser,
            ILogger<CompilationService> logger)
        {
            _tokenizerService = tokenizerService ?? throw new ArgumentNullException(nameof(tokenizerService));
            _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
            _analyzerService = analyzerService ?? throw new ArgumentNullException(nameof(analyzerService));
            _attentionService = attentionService ?? throw new ArgumentNullException(nameof(attentionService));
            _spaceProjParser = spaceProjParser ?? throw new ArgumentNullException(nameof(spaceProjParser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CompilationResult> CompileFileAsync(
            string content,
            string fileName,
            string? contentType = null)
        {
            _logger.LogInformation("Compiling file: {FileName}", fileName);

            var result = new CompilationResult
            {
                Metadata = new Dictionary<string, object>
                {
                    ["compiled_at"] = DateTime.UtcNow,
                    ["file_name"] = fileName
                }
            };

            try
            {
                // Auto-detect content type from file extension if not provided
                if (string.IsNullOrEmpty(contentType))
                {
                    contentType = DetectContentType(fileName);
                }

                // Step 1: Tokenization
                _logger.LogInformation("Tokenizing file {FileName} as {ContentType}", fileName, contentType);
                var fragments = await _tokenizerService.TokenizeAsync(content, contentType);

                if (fragments.Count == 0)
                {
                    result.Warnings.Add($"No fragments extracted from {fileName}");
                    return result;
                }

                // Step 2: Build AST
                _logger.LogInformation("Building AST for {FileName}", fileName);
                var parsedResource = await _parserService.BuildAstAsync(
                    fragments,
                    fileName,
                    contentType);

                // Step 3: Semantic Analysis
                _logger.LogInformation("Analyzing semantics for {FileName}", fileName);
                parsedResource = await _analyzerService.AnalyzeAsync(parsedResource);

                result.Resources.Add(parsedResource);

                // Step 4: Compute Self-Attention
                _logger.LogInformation("Computing self-attention matrix for {FileName}", fileName);
                result.AttentionMatrix = await _attentionService.ComputeAttentionAsync(result.Resources);

                _logger.LogInformation(
                    "Successfully compiled {FileName}: {BlockCount} blocks, {FragmentCount} fragments",
                    fileName,
                    parsedResource.Blocks.Count,
                    parsedResource.Blocks.Sum(b => b.Fragments.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling file {FileName}", fileName);
                result.Errors.Add($"Error compiling {fileName}: {ex.Message}");
            }

            return result;
        }

        public async Task<CompilationResult> CompileFilesAsync(Dictionary<string, string> files)
        {
            _logger.LogInformation("Compiling {Count} files as unified tree", files.Count);

            var result = new CompilationResult
            {
                Metadata = new Dictionary<string, object>
                {
                    ["compiled_at"] = DateTime.UtcNow,
                    ["file_count"] = files.Count
                }
            };

            try
            {
                // Compile each file (without individual attention computation)
                foreach (var (fileName, content) in files)
                {
                    // Use individual compilation steps but skip attention
                    var contentType = DetectContentType(fileName);
                    var fragments = await _tokenizerService.TokenizeAsync(content, contentType);

                    if (fragments.Count > 0)
                    {
                        var parsedResource = await _parserService.BuildAstAsync(fragments, fileName, contentType);
                        parsedResource = await _analyzerService.AnalyzeAsync(parsedResource);
                        result.Resources.Add(parsedResource);
                    }
                    else
                    {
                        result.Warnings.Add($"No fragments extracted from {fileName}");
                    }
                }

                // Compute unified self-attention for all resources together
                if (result.Resources.Count > 0)
                {
                    _logger.LogInformation("Computing unified self-attention matrix for all files");
                    result.AttentionMatrix = await _attentionService.ComputeAttentionAsync(result.Resources);
                }

                _logger.LogInformation(
                    "Successfully compiled {FileCount} files: {ResourceCount} resources, {ErrorCount} errors",
                    files.Count,
                    result.Resources.Count,
                    result.Errors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling files");
                result.Errors.Add($"Error compiling files: {ex.Message}");
            }

            return result;
        }

        public async Task<CompilationResult> CompileProjectAsync(Stream zipStream)
        {
            _logger.LogInformation("Compiling project from zip archive");

            var result = new CompilationResult
            {
                Metadata = new Dictionary<string, object>
                {
                    ["compiled_at"] = DateTime.UtcNow,
                    ["type"] = "project"
                }
            };

            try
            {
                var files = new Dictionary<string, string>();
                string? spaceProjContent = null;
                string? spaceProjFileName = null;

                // Extract files from zip
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/") || string.IsNullOrEmpty(entry.Name))
                        {
                            continue; // Skip directories
                        }

                        using var reader = new StreamReader(entry.Open());
                        var content = await reader.ReadToEndAsync();

                        if (entry.Name.EndsWith(".spaceproj", StringComparison.OrdinalIgnoreCase))
                        {
                            spaceProjContent = content;
                            spaceProjFileName = entry.Name;
                            _logger.LogInformation("Found .spaceproj file: {FileName}", entry.Name);
                        }
                        else
                        {
                            files[entry.FullName] = content;
                        }
                    }
                }

                if (string.IsNullOrEmpty(spaceProjContent))
                {
                    result.Errors.Add("No .spaceproj file found in archive");
                    return result;
                }

                // Parse .spaceproj file
                _logger.LogInformation("Parsing .spaceproj file");
                var projectGraph = await _spaceProjParser.ParseAsync(spaceProjContent);

                result.Metadata["project_graph"] = projectGraph;
                result.Metadata["spaceproj_file"] = spaceProjFileName ?? "unknown";

                // Compile files referenced in the project graph
                await CompileProjectGraphAsync(projectGraph, files, result);

                // Compute self-attention for entire project
                if (result.Resources.Count > 0)
                {
                    _logger.LogInformation("Computing self-attention matrix for project");
                    result.AttentionMatrix = await _attentionService.ComputeAttentionAsync(result.Resources);
                }

                _logger.LogInformation(
                    "Successfully compiled project: {ResourceCount} resources, {ErrorCount} errors",
                    result.Resources.Count,
                    result.Errors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling project");
                result.Errors.Add($"Error compiling project: {ex.Message}");
            }

            return result;
        }

        private async Task CompileProjectGraphAsync(
            ProjectGraph projectGraph,
            Dictionary<string, string> files,
            CompilationResult result)
        {
            // Compile each file referenced in the graph
            foreach (var root in projectGraph.Roots)
            {
                await CompileGraphNodeAsync(root, files, result);
            }
        }

        private async Task CompileGraphNodeAsync(
            GraphNode node,
            Dictionary<string, string> files,
            CompilationResult result)
        {
            // If node has a file path, compile it
            if (!string.IsNullOrEmpty(node.FilePath))
            {
                // Try to find the file in the extracted files
                var fileEntry = files.FirstOrDefault(kvp =>
                    kvp.Key.EndsWith(node.FilePath, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals(node.FilePath, StringComparison.OrdinalIgnoreCase));

                if (!fileEntry.Equals(default(KeyValuePair<string, string>)))
                {
                    _logger.LogInformation("Compiling graph node file: {FilePath}", node.FilePath);

                    var fileResult = await CompileFileAsync(fileEntry.Value, node.FilePath);

                    // Store the parsed content in the graph node
                    if (fileResult.Resources.Count > 0)
                    {
                        node.ParsedContent = fileResult.Resources[0];
                    }

                    result.Resources.AddRange(fileResult.Resources);
                    result.Errors.AddRange(fileResult.Errors);
                    result.Warnings.AddRange(fileResult.Warnings);
                }
                else
                {
                    result.Warnings.Add($"File not found in archive: {node.FilePath}");
                }
            }

            // Recursively compile child nodes
            foreach (var child in node.Children)
            {
                await CompileGraphNodeAsync(child, files, result);
            }
        }

        private string DetectContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".txt" => "text",
                ".json" => "json",
                ".md" => "text",
                ".doc" => "text",
                ".docx" => "text",
                _ => "text" // Default to text
            };
        }
    }
}
