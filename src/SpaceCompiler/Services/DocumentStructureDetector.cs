using System.Text.RegularExpressions;

namespace SpaceCompiler.Services
{
    /// <summary>
    /// Helper class for detecting document structural elements
    /// Identifies headings, numbered sections, table of contents, etc.
    /// </summary>
    public class DocumentStructureDetector
    {
        // Patterns for numbered headings
        // Matches: "1.", "1.1.", "1.1.1.", etc.
        private static readonly Regex ArabicNumberedPattern = new Regex(
            @"^(\d+\.)+\s+(.+)$",
            RegexOptions.Compiled);

        // Matches: "I.", "II.", "III.", "IV.", etc. (Roman numerals)
        private static readonly Regex RomanNumberedPattern = new Regex(
            @"^([IVXLCDM]+)\.\s+(.+)$",
            RegexOptions.Compiled);

        // Matches: "Глава 1", "Раздел 2", "Часть I", etc.
        private static readonly Regex ChapterPattern = new Regex(
            @"^(Глава|Раздел|Часть|Chapter|Section|Part)\s+([IVXLCDM\d]+)[\.\:]?\s*(.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches uppercase headings (at least 5 characters, mostly uppercase)
        private static readonly Regex UppercaseHeadingPattern = new Regex(
            @"^[А-ЯЁA-Z][А-ЯЁA-Z\s\d\.\,\-]{4,}[А-ЯЁA-Z\.]$",
            RegexOptions.Compiled);

        // Matches table of contents keywords
        private static readonly Regex TocPattern = new Regex(
            @"^(Содержание|Оглавление|Contents|Table of Contents)[\.\:]?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches indented lines (2+ spaces or tabs at the start)
        private static readonly Regex IndentedPattern = new Regex(
            @"^(\s{2,}|\t+)(.+)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Detects the structural type of a text line
        /// </summary>
        public DocumentStructureType DetectStructureType(string line, string? previousLine = null)
        {
            if (string.IsNullOrWhiteSpace(line))
                return DocumentStructureType.None;

            var trimmedLine = line.Trim();

            // Check for table of contents marker
            if (TocPattern.IsMatch(trimmedLine))
                return DocumentStructureType.TableOfContents;

            // Check for chapter/section markers
            if (ChapterPattern.IsMatch(trimmedLine))
                return DocumentStructureType.Chapter;

            // Check for Roman numeral headings
            if (RomanNumberedPattern.IsMatch(trimmedLine))
                return DocumentStructureType.RomanNumberedHeading;

            // Check for Arabic numbered headings
            var arabicMatch = ArabicNumberedPattern.Match(trimmedLine);
            if (arabicMatch.Success)
            {
                // Determine nesting level by counting dots in the full number
                // For "1.2.3. Text", we want to count the separating dots (2 in this case)
                var fullNumbering = trimmedLine.Substring(0, trimmedLine.IndexOf(' '));
                int level = fullNumbering.Count(c => c == '.') - 1; // Subtract 1 for the trailing dot

                return level switch
                {
                    0 => DocumentStructureType.NumberedHeadingLevel1,
                    1 => DocumentStructureType.NumberedHeadingLevel2,
                    2 => DocumentStructureType.NumberedHeadingLevel3,
                    _ => DocumentStructureType.NumberedHeadingDeep
                };
            }

            // Check for uppercase headings (but not too long - likely not a heading if > 100 chars)
            if (trimmedLine.Length <= 100 && UppercaseHeadingPattern.IsMatch(trimmedLine))
                return DocumentStructureType.UppercaseHeading;

            // Check for indentation
            if (IndentedPattern.IsMatch(line))
                return DocumentStructureType.Indented;

            return DocumentStructureType.Regular;
        }

        /// <summary>
        /// Extracts numbering from a numbered heading
        /// </summary>
        public string? ExtractNumbering(string line)
        {
            var trimmedLine = line.Trim();

            // Try Arabic numbering
            var arabicMatch = ArabicNumberedPattern.Match(trimmedLine);
            if (arabicMatch.Success)
            {
                // Extract the full numbering up to the last space
                var spaceIndex = trimmedLine.IndexOf(' ');
                if (spaceIndex > 0)
                {
                    return trimmedLine.Substring(0, spaceIndex).TrimEnd('.');
                }
                return trimmedLine.TrimEnd('.');
            }

            // Try Roman numbering
            var romanMatch = RomanNumberedPattern.Match(trimmedLine);
            if (romanMatch.Success)
                return romanMatch.Groups[1].Value;

            // Try chapter pattern
            var chapterMatch = ChapterPattern.Match(trimmedLine);
            if (chapterMatch.Success)
                return chapterMatch.Groups[2].Value;

            return null;
        }

        /// <summary>
        /// Extracts the heading text without numbering
        /// </summary>
        public string ExtractHeadingText(string line)
        {
            var trimmedLine = line.Trim();

            // Try Arabic numbering
            var arabicMatch = ArabicNumberedPattern.Match(trimmedLine);
            if (arabicMatch.Success)
            {
                // Find the first space after the numbering
                var spaceIndex = trimmedLine.IndexOf(' ');
                if (spaceIndex > 0 && spaceIndex < trimmedLine.Length - 1)
                {
                    return trimmedLine.Substring(spaceIndex + 1).Trim();
                }
                return trimmedLine;
            }

            // Try Roman numbering
            var romanMatch = RomanNumberedPattern.Match(trimmedLine);
            if (romanMatch.Success)
                return romanMatch.Groups[2].Value.Trim();

            // Try chapter pattern
            var chapterMatch = ChapterPattern.Match(trimmedLine);
            if (chapterMatch.Success)
            {
                var prefix = chapterMatch.Groups[1].Value;
                var number = chapterMatch.Groups[2].Value;
                var title = chapterMatch.Groups[3].Value.Trim();
                return string.IsNullOrEmpty(title) ? $"{prefix} {number}" : title;
            }

            return trimmedLine;
        }

        /// <summary>
        /// Gets the indentation level of a line (number of leading spaces/tabs)
        /// </summary>
        public int GetIndentationLevel(string line)
        {
            var match = IndentedPattern.Match(line);
            if (!match.Success)
                return 0;

            var indent = match.Groups[1].Value;
            // Count tabs as 4 spaces
            return indent.Count(c => c == ' ') + (indent.Count(c => c == '\t') * 4);
        }

        /// <summary>
        /// Checks if a line could be a table of contents entry
        /// (often has page numbers at the end or dots leading to page numbers)
        /// </summary>
        public bool IsPossibleTocEntry(string line, DocumentStructureType structureType)
        {
            // TOC entries are often numbered or indented
            if (structureType == DocumentStructureType.NumberedHeadingLevel1 ||
                structureType == DocumentStructureType.NumberedHeadingLevel2 ||
                structureType == DocumentStructureType.Indented)
            {
                // Check for page numbers at the end: ".... 15" or "... 15"
                var trimmedLine = line.Trim();
                if (Regex.IsMatch(trimmedLine, @"\.{2,}\s*\d+\s*$"))
                    return true;

                // Check for just a number at the end
                if (Regex.IsMatch(trimmedLine, @"\s+\d+\s*$"))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Types of document structural elements
    /// </summary>
    public enum DocumentStructureType
    {
        None,
        Regular,
        TableOfContents,
        Chapter,
        RomanNumberedHeading,
        NumberedHeadingLevel1,
        NumberedHeadingLevel2,
        NumberedHeadingLevel3,
        NumberedHeadingDeep,
        UppercaseHeading,
        Indented
    }
}
