using FluentAssertions;
using SpaceCompiler.Services;
using Xunit;

namespace SpaceCompiler.Tests.Services
{
    public class DocumentStructureDetectorTests
    {
        private readonly DocumentStructureDetector _detector;

        public DocumentStructureDetectorTests()
        {
            _detector = new DocumentStructureDetector();
        }

        [Theory]
        [InlineData("Содержание", DocumentStructureType.TableOfContents)]
        [InlineData("Оглавление", DocumentStructureType.TableOfContents)]
        [InlineData("Contents", DocumentStructureType.TableOfContents)]
        [InlineData("Table of Contents", DocumentStructureType.TableOfContents)]
        [InlineData("СОДЕРЖАНИЕ", DocumentStructureType.TableOfContents)]
        [InlineData("Содержание:", DocumentStructureType.TableOfContents)]
        public void DetectStructureType_WithTableOfContentsMarker_ShouldReturnTableOfContents(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Глава 1", DocumentStructureType.Chapter)]
        [InlineData("Глава 1. Введение", DocumentStructureType.Chapter)]
        [InlineData("Раздел 2", DocumentStructureType.Chapter)]
        [InlineData("Часть I", DocumentStructureType.Chapter)]
        [InlineData("Chapter 3", DocumentStructureType.Chapter)]
        [InlineData("Section 5", DocumentStructureType.Chapter)]
        [InlineData("Part II", DocumentStructureType.Chapter)]
        public void DetectStructureType_WithChapterMarker_ShouldReturnChapter(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("I. Введение", DocumentStructureType.RomanNumberedHeading)]
        [InlineData("II. Основная часть", DocumentStructureType.RomanNumberedHeading)]
        [InlineData("III. Заключение", DocumentStructureType.RomanNumberedHeading)]
        [InlineData("IV. Список литературы", DocumentStructureType.RomanNumberedHeading)]
        [InlineData("X. Final Section", DocumentStructureType.RomanNumberedHeading)]
        public void DetectStructureType_WithRomanNumbering_ShouldReturnRomanNumberedHeading(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("1. ОБЩАЯ ХАРАКТЕРИСТИКА ВИДА РАБОТЫ", DocumentStructureType.NumberedHeadingLevel1)]
        [InlineData("2. Методология исследования", DocumentStructureType.NumberedHeadingLevel1)]
        [InlineData("3. Результаты", DocumentStructureType.NumberedHeadingLevel1)]
        [InlineData("10. Conclusion", DocumentStructureType.NumberedHeadingLevel1)]
        public void DetectStructureType_WithLevel1Numbering_ShouldReturnNumberedHeadingLevel1(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("1.1. Цели и задачи", DocumentStructureType.NumberedHeadingLevel2)]
        [InlineData("2.1. Материалы", DocumentStructureType.NumberedHeadingLevel2)]
        [InlineData("3.2. Обсуждение", DocumentStructureType.NumberedHeadingLevel2)]
        [InlineData("10.5. Final Subsection", DocumentStructureType.NumberedHeadingLevel2)]
        public void DetectStructureType_WithLevel2Numbering_ShouldReturnNumberedHeadingLevel2(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("1.1.1. Подробности", DocumentStructureType.NumberedHeadingLevel3)]
        [InlineData("2.3.4. Детали методики", DocumentStructureType.NumberedHeadingLevel3)]
        [InlineData("5.2.1. Subsection details", DocumentStructureType.NumberedHeadingLevel3)]
        public void DetectStructureType_WithLevel3Numbering_ShouldReturnNumberedHeadingLevel3(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("1.1.1.1. Очень глубокий раздел", DocumentStructureType.NumberedHeadingDeep)]
        [InlineData("2.3.4.5.6. Super deep section", DocumentStructureType.NumberedHeadingDeep)]
        public void DetectStructureType_WithDeepNumbering_ShouldReturnNumberedHeadingDeep(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("ВВЕДЕНИЕ", DocumentStructureType.UppercaseHeading)]
        [InlineData("ОБЩАЯ ХАРАКТЕРИСТИКА ВИДА РАБОТЫ", DocumentStructureType.UppercaseHeading)]
        [InlineData("ЗАКЛЮЧЕНИЕ", DocumentStructureType.UppercaseHeading)]
        [InlineData("СПИСОК ЛИТЕРАТУРЫ", DocumentStructureType.UppercaseHeading)]
        [InlineData("INTRODUCTION", DocumentStructureType.UppercaseHeading)]
        [InlineData("METHODOLOGY AND RESULTS", DocumentStructureType.UppercaseHeading)]
        public void DetectStructureType_WithUppercaseHeading_ShouldReturnUppercaseHeading(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("  Indented paragraph", DocumentStructureType.Indented)]
        [InlineData("    More indented", DocumentStructureType.Indented)]
        [InlineData("\tTab indented", DocumentStructureType.Indented)]
        [InlineData("      Six spaces", DocumentStructureType.Indented)]
        public void DetectStructureType_WithIndentation_ShouldReturnIndented(
            string line, DocumentStructureType expected)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("Regular paragraph text")]
        [InlineData("Just some normal content without special structure")]
        [InlineData("This is a sentence with normal capitalization.")]
        public void DetectStructureType_WithRegularText_ShouldReturnRegular(string line)
        {
            // Act
            var result = _detector.DetectStructureType(line);

            // Assert
            result.Should().Be(DocumentStructureType.Regular);
        }

        [Fact]
        public void ExtractNumbering_FromArabicNumberedHeading_ShouldReturnNumber()
        {
            // Arrange
            var line = "1.2.3. Some heading";

            // Act
            var result = _detector.ExtractNumbering(line);

            // Assert
            result.Should().Be("1.2.3");
        }

        [Fact]
        public void ExtractNumbering_FromRomanNumberedHeading_ShouldReturnRomanNumeral()
        {
            // Arrange
            var line = "III. Some heading";

            // Act
            var result = _detector.ExtractNumbering(line);

            // Assert
            result.Should().Be("III");
        }

        [Fact]
        public void ExtractNumbering_FromChapter_ShouldReturnChapterNumber()
        {
            // Arrange
            var line = "Глава 5. Заключение";

            // Act
            var result = _detector.ExtractNumbering(line);

            // Assert
            result.Should().Be("5");
        }

        [Fact]
        public void ExtractNumbering_FromRegularText_ShouldReturnNull()
        {
            // Arrange
            var line = "Regular text without numbering";

            // Act
            var result = _detector.ExtractNumbering(line);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ExtractHeadingText_FromNumberedHeading_ShouldReturnTextWithoutNumber()
        {
            // Arrange
            var line = "1.2. Методология исследования";

            // Act
            var result = _detector.ExtractHeadingText(line);

            // Assert
            result.Should().Be("Методология исследования");
        }

        [Fact]
        public void ExtractHeadingText_FromChapter_ShouldReturnTitle()
        {
            // Arrange
            var line = "Глава 3. Результаты работы";

            // Act
            var result = _detector.ExtractHeadingText(line);

            // Assert
            result.Should().Be("Результаты работы");
        }

        [Fact]
        public void ExtractHeadingText_FromChapterWithoutTitle_ShouldReturnChapterWithNumber()
        {
            // Arrange
            var line = "Глава 3";

            // Act
            var result = _detector.ExtractHeadingText(line);

            // Assert
            result.Should().Be("Глава 3");
        }

        [Fact]
        public void GetIndentationLevel_FromSpacesIndented_ShouldReturnSpaceCount()
        {
            // Arrange
            var line = "    Indented with 4 spaces";

            // Act
            var result = _detector.GetIndentationLevel(line);

            // Assert
            result.Should().Be(4);
        }

        [Fact]
        public void GetIndentationLevel_FromTabIndented_ShouldReturnEquivalentSpaces()
        {
            // Arrange
            var line = "\tIndented with tab";

            // Act
            var result = _detector.GetIndentationLevel(line);

            // Assert
            result.Should().Be(4); // Tab counts as 4 spaces
        }

        [Fact]
        public void GetIndentationLevel_FromNonIndented_ShouldReturnZero()
        {
            // Arrange
            var line = "Not indented";

            // Act
            var result = _detector.GetIndentationLevel(line);

            // Assert
            result.Should().Be(0);
        }

        [Theory]
        [InlineData("1. Введение .......................... 5", DocumentStructureType.NumberedHeadingLevel1)]
        [InlineData("1.1. Цели работы ..................... 10", DocumentStructureType.NumberedHeadingLevel2)]
        [InlineData("  Раздел первый ....................... 15", DocumentStructureType.Indented)]
        [InlineData("2. Методология 25", DocumentStructureType.NumberedHeadingLevel1)]
        public void IsPossibleTocEntry_WithTocPatterns_ShouldReturnTrue(
            string line, DocumentStructureType structureType)
        {
            // Act
            var result = _detector.IsPossibleTocEntry(line, structureType);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("Regular paragraph text", DocumentStructureType.Regular)]
        [InlineData("Just some content", DocumentStructureType.Regular)]
        public void IsPossibleTocEntry_WithoutTocPatterns_ShouldReturnFalse(
            string line, DocumentStructureType structureType)
        {
            // Act
            var result = _detector.IsPossibleTocEntry(line, structureType);

            // Assert
            result.Should().BeFalse();
        }
    }
}
